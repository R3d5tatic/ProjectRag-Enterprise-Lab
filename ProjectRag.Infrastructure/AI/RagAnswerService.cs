using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using ProjectRag.Application.Abstractions;
using ProjectRag.Application.Models;
using ProjectRag.Application.Telemetry;
using ProjectRag.Infrastructure.Options;
using System.Diagnostics;

namespace ProjectRag.Infrastructure.AI;

internal sealed class RagAnswerService : IRagAnswerService
{
    private readonly ChatRuntimeOptions _chatOptions;
    private readonly EmbeddingRuntimeOptions _embeddingOptions;
    private readonly RetrievalOptions _retrievalOptions;

    private readonly IRetrievalSearchService _retrievalSearchService;
    private readonly IChatClient _chatClient;
    private readonly IQueryRewriteService _queryRewriteService;
    public RagAnswerService(
        IOptions<ChatRuntimeOptions> chatOptions,
        IOptions<EmbeddingRuntimeOptions> embeddingOptions,
        IOptions<RetrievalOptions> retrievalOptions,
        IRetrievalSearchService retrievalSearchService,
        IChatClient chatClient,
        IQueryRewriteService queryRewriteService)
    {
        _chatOptions = chatOptions.Value;
        _embeddingOptions = embeddingOptions.Value;
        _retrievalOptions = retrievalOptions.Value;
        _retrievalSearchService = retrievalSearchService;
        _chatClient = chatClient;
        _queryRewriteService = queryRewriteService;
    }
    public async Task<RagAnswer> AnswerAsync(
        string question,
        int topK,
        SearchFilters? filters,
        CancellationToken cancellationToken)
    {
        using var activity = ProjectRagTelemetry.ActivitySource.StartActivity("rag.answer");
        activity?.SetTag("rag.question.length", question.Length);
        activity?.SetTag("rag.top_k", topK);
        activity?.SetTag("rag.filters.source_type", filters?.SourceType);

        var queryRewrite = await _queryRewriteService.RewriteAsync(question, cancellationToken);

        if (string.IsNullOrWhiteSpace(question))
        {
            activity?.SetTag("rag.answer.status", "insufficientContext");
            activity?.SetTag("rag.context.count", 0);

            var diagnostics = BuildRetrievalDiagnostics(topK, []);
            SetRetrievalDiagnosticTags(activity, diagnostics);

            return new RagAnswer(
                Answer: "Please provide a question",
                AnswerStatus: "insufficientContext",
                QueryRewrite: queryRewrite,
                Claims: [],
                Citations: [],
                RetrievalDiagnostics: diagnostics,
                ModelInfo: BuildModelInfo());
        }

        var retrievalQuery = new RetrievalQuery(
            OriginalQuery: queryRewrite.OriginalQuery,
            SemanticQuery: queryRewrite.SemanticQuery,
            KeywordQuery: queryRewrite.KeywordQuery);

        var hits = await _retrievalSearchService.SearchAsync(
            retrievalQuery,
            topK,
            filters,
            cancellationToken);

        activity?.SetTag("rag.context.count", hits.Count);

        if (hits.Count == 0)
        {
            activity?.SetTag("rag.answer.status", "insufficientContext");
            var diagnostics = BuildRetrievalDiagnostics(topK, hits);
            SetRetrievalDiagnosticTags(activity, diagnostics);

            return new RagAnswer(
                Answer: "I do not have enough information in the available documents to answer that question.",
                AnswerStatus: "insufficientContext",
                QueryRewrite: queryRewrite,
                Claims: [],
                Citations: [],
                RetrievalDiagnostics: diagnostics,
                ModelInfo: BuildModelInfo());
        }

        var context = BuildContext(hits);

        var prompt = $$"""
            You are a grounded RAG assistant.

            Use only the provided sources to answer the question.
            If the sources do not contain enough information, return insufficientContext.
            Do not use prior knowledge.
            Do not cite a source unless it directly supports the claim.

            Return JSON only. Do not include markdown fences, commentary, or explanations.

            The JSON object must have this shape:
            {
                "answerStatus": "answered",
                "answer": "concise answer text",
                "claims": [
                    {
                        "text": "specific claim from the answer",
                        "sourceIndexes": [1]
                    }
                ]
            }

            Rules:
            - answerStatus must be "answered" or "insufficientContext".
            - If answerStatus is "insufficientContext", answer must briefly say there is not enough information.
            - Each answered claim must include at least one source index.
            - Source indexes must refer to the [Source N] entries below.
            - Do not include source indexes that are not in the context.

            Context:
            {{context}}

            Question:
            {{question}}
            """;

        using var generationActivity = ProjectRagTelemetry.ActivitySource.StartActivity("rag.answer_generation");
        generationActivity?.SetTag("rag.context.count", hits.Count);
        generationActivity?.SetTag("rag.prompt.length", prompt.Length);

        var response = await _chatClient.GetResponseAsync(prompt, cancellationToken: cancellationToken);

        var parsedAnswer = RagAnswerResponseParser.Parse(response.Text, hits);

        generationActivity?.SetTag("rag.answer.status", parsedAnswer.AnswerStatus);
        generationActivity?.SetTag("rag.answer.parse_fallback", parsedAnswer.UsedFallback);
        generationActivity?.SetTag("rag.claims.count", parsedAnswer.Claims.Count);

        var citations = hits
            .Select(hit => new Citation(
                hit.DocumentId,
                hit.ChunkId,
                hit.Source,
                hit.RrfScore,
                hit.RerankScore,
                hit.VectorScore,
                hit.KeywordScore,
                hit.MatchedBy,
                hit.PageNumber,
                hit.Kind,
                hit.SectionTitle
                ))
            .ToList();

        activity?.SetTag("rag.answer.status", parsedAnswer.AnswerStatus);
        activity?.SetTag("rag.answer.parse_fallback", parsedAnswer.UsedFallback);
        activity?.SetTag("rag.claims.count", parsedAnswer.Claims.Count);
        activity?.SetTag("rag.citations.count", citations.Count);

        var finalDiagnostics = BuildRetrievalDiagnostics(topK, hits);
        SetRetrievalDiagnosticTags(activity, finalDiagnostics);

        return new RagAnswer(
            Answer: parsedAnswer.Answer,
            AnswerStatus: parsedAnswer.AnswerStatus,
            QueryRewrite: queryRewrite,
            Claims: parsedAnswer.Claims,
            Citations: citations,
            RetrievalDiagnostics: finalDiagnostics,
            ModelInfo: BuildModelInfo());
    }

    private static string BuildContext(IReadOnlyList<SearchHit> hits)
    {
        return string.Join(
            "\n\n",
            hits.Select((hit, index) => $"""
                [Source {index + 1}]
                DocumentId: {hit.DocumentId}
                ChunkId: {hit.ChunkId}
                Source: {hit.Source}
                RrfScore: {hit.RrfScore}
                RerankerScore: {hit.RerankScore}
                PageNumber: {hit.PageNumber}
                Kind: {hit.Kind}
                Section: {hit.SectionTitle}
                Text:
                {hit.Text}
                """));
    }

    private ModelInfo BuildModelInfo()
    {
        return new ModelInfo(
            ChatProvider: _chatOptions.Provider,
            ChatModel: _chatOptions.Model,
            EmbeddingProvider: _embeddingOptions.Provider,
            EmbeddingModel: _embeddingOptions.Model);
    }

    private RetrievalDiagnostics BuildRetrievalDiagnostics(
        int requestedTopK,
        IReadOnlyList<SearchHit> hits)
    {
        var rerankingApplied = hits.Any(hit => hit.RerankScore.HasValue);

        return new RetrievalDiagnostics(
            RequestedTopK: requestedTopK,
            CandidateCount: _retrievalOptions.CandidateCount,
            ReturnedContextCount: hits.Count,
            RerankingApplied: rerankingApplied,
            RetrievalMode: _retrievalOptions.RetrievalMode,
            FusionMode: _retrievalOptions.FusionMode,
            RerankerMode: _retrievalOptions.EnableReranking
                ? _retrievalOptions.RerankerMode
                : RetrievalStrategyNames.NoReranker,
            RrfConstant: _retrievalOptions.RrfConstant);
    }

    private static void SetRetrievalDiagnosticTags(
        Activity? activity,
        RetrievalDiagnostics diagnostics)
    {
        activity?.SetTag("rag.retrieval.requested_top_k", diagnostics.RequestedTopK);
        activity?.SetTag("rag.retrieval.candidate_count", diagnostics.CandidateCount);
        activity?.SetTag("rag.retrieval.returned_context_count", diagnostics.ReturnedContextCount);
        activity?.SetTag("rag.retrieval.reranking_applied", diagnostics.RerankingApplied);
        activity?.SetTag("rag.retrieval.mode", diagnostics.RetrievalMode);
        activity?.SetTag("rag.retrieval.fusion_mode", diagnostics.FusionMode);
        activity?.SetTag("rag.retrieval.reranker_mode", diagnostics.RerankerMode);
        activity?.SetTag("rag.retrieval.rrf_constant", diagnostics.RrfConstant);
    }
}
