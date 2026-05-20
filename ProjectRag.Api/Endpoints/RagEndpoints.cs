using Microsoft.EntityFrameworkCore;
using ProjectRag.Application.Abstractions;
using ProjectRag.Application.Models;
using ProjectRag.Application.Telemetry;
using ProjectRag.Contracts;
using ProjectRag.Infrastructure;

namespace ProjectRag.Api.Endpoints;

internal static class RagEndpoints
{
    public static RouteGroupBuilder MapRagEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/health", () =>
        Results.Ok(new
        {
            Status = "Healthy",
            Service = "ProjectRag.Api",
            TimeUtc = DateTime.UtcNow
        }));

        group.MapPost("/ingestions", async (
            StartIngestionRequest request,
            IIngestionRunQueueService ingestionRunQueueService,
            CancellationToken cancellationToken) =>
        {

            var result = await ingestionRunQueueService.QueueAsync(request.SourcePath, cancellationToken);

            return Results.Accepted($"/api/v1/ingestions/{result.IngestionId}", ToIngestionRunResponse(result));
        });

        group.MapGet("/ingestions/{id:guid}", async (
            Guid id,
            IIngestionRunQueueService ingestionRunQueueService,
            CancellationToken cancellationToken) =>
        {
            var result = await ingestionRunQueueService.GetAsync(id, cancellationToken);

            return result is null ? Results.NotFound() : Results.Ok(ToIngestionRunResponse(result));
        });

        group.MapGet("/documents", async (
            RagDbContext db,
            CancellationToken cancellationToken) =>
        {
            var documents = await db.Documents
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new DocumentSummaryResponse(
                x.Id.ToString(),
                x.SourceUri,
                x.Title,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

            return Results.Ok(documents);
        });

        group.MapPost("/search", async (
            SearchRequest request,
            IRetrievalSearchService retrievalSearchService,
            IQueryRewriteService queryRewriteService,
            CancellationToken cancellationToken) =>
        {
            using var activity = ProjectRagTelemetry.ActivitySource.StartActivity("rag.search");
            activity?.SetTag("rag.query.length", request.Query.Length);
            activity?.SetTag("rag.top_k", request.TopK);
            activity?.SetTag("rag.filters.source_type", request.Filters?.SourceType);

            var filters = request.Filters is null
                ? null
                : new SearchFilters(
                      request.Filters.SourceType,
                      request.Filters.SourceUriContains,
                      request.Filters.CreatedFrom,
                      request.Filters.CreatedTo,
                      request.Filters.PageFrom,
                      request.Filters.PageTo);

            var queryRewrite = await queryRewriteService.RewriteAsync(request.Query, cancellationToken);

            var retrievalQuery = new RetrievalQuery(
                queryRewrite.OriginalQuery,
                queryRewrite.SemanticQuery,
                queryRewrite.KeywordQuery);

            var hits = await retrievalSearchService.SearchAsync(retrievalQuery, request.TopK, filters, cancellationToken);

            activity?.SetTag("rag.results.count", hits.Count);
            activity?.SetTag("rag.reranking.applied", hits.Any(x => x.RerankScore.HasValue));

            var response = new SearchResponse(
                request.Query,
                new QueryRewriteResponse(
                    queryRewrite.OriginalQuery,
                    queryRewrite.SemanticQuery,
                    queryRewrite.KeywordQuery,
                    queryRewrite.Status),
                hits.Select(x => new SearchHitResponse(
                    x.ChunkId.ToString(),
                    x.DocumentId.ToString(),
                    x.Source,
                    x.Text.Length <= 300 ? x.Text : x.Text[..300], // limit text preview size to 300
                    x.RrfScore,
                    x.RerankScore,
                    x.PageNumber,
                    x.Kind.ToString(),
                    x.SectionTitle,
                    x.VectorScore,
                    x.KeywordScore,
                    x.MatchedBy)).ToList());

            return Results.Ok(response);
        });


        group.MapPost("/ask", async (
            AskRequest request,
            IRagAnswerService ragAnswerService,
            CancellationToken cancellationToken) =>
        {
            using var activity = ProjectRagTelemetry.ActivitySource.StartActivity("rag.ask");
            activity?.SetTag("rag.question.length", request.Question.Length);
            activity?.SetTag("rag.top_k", request.TopK);
            activity?.SetTag("rag.filters.source_type", request.Filters?.SourceType);

            var filters = request.Filters is null
                ? null
                : new SearchFilters(
                      request.Filters.SourceType,
                      request.Filters.SourceUriContains,
                      request.Filters.CreatedFrom,
                      request.Filters.CreatedTo,
                      request.Filters.PageFrom,
                      request.Filters.PageTo);

            var answer = await ragAnswerService.AnswerAsync(request.Question, request.TopK, filters, cancellationToken);

            activity?.SetTag("rag.answer.status", answer.AnswerStatus);
            activity?.SetTag("rag.claims.count", answer.Claims.Count);
            activity?.SetTag("rag.citations.count", answer.Citations.Count);
            activity?.SetTag("rag.reranking.applied", answer.RetrievalDiagnostics.RerankingApplied);

            var response = new AskResponse(
                answer.Answer,
                answer.AnswerStatus,
                new QueryRewriteResponse(
                    answer.QueryRewrite.OriginalQuery,
                    answer.QueryRewrite.SemanticQuery,
                    answer.QueryRewrite.KeywordQuery,
                    answer.QueryRewrite.Status),
                answer.Claims.Select(x => new ClaimResponse(
                    x.Text,
                    x.CitationChunkIds.Select(id => id.ToString()).ToList()
                    )).ToList(),
                answer.Citations.Select(x => new CitationResponse(
                    x.DocumentId.ToString(),
                    x.ChunkId.ToString(),
                    x.Source,
                    x.PageNumber,
                    x.RrfScore,
                    x.RerankScore,
                    x.VectorScore,
                    x.KeywordScore,
                    x.MatchedBy,
                    x.Kind.ToString(),
                    x.SectionTitle)).ToList(),
                new RetrievalDiagnosticsResponse(
                    answer.RetrievalDiagnostics.RequestedTopK,
                    answer.RetrievalDiagnostics.CandidateCount,
                    answer.RetrievalDiagnostics.ReturnedContextCount,
                    answer.RetrievalDiagnostics.RerankingApplied,
                    answer.RetrievalDiagnostics.RetrievalMode,
                    answer.RetrievalDiagnostics.FusionMode,
                    answer.RetrievalDiagnostics.RerankerMode,
                    answer.RetrievalDiagnostics.RrfConstant),
                new ModelInfoResponse(
                    answer.ModelInfo.ChatProvider,
                    answer.ModelInfo.ChatModel,
                    answer.ModelInfo.EmbeddingProvider,
                    answer.ModelInfo.EmbeddingModel));

            return Results.Ok(response);
        });

        return group;
    }

    private static IngestionRunResponse ToIngestionRunResponse(IngestionRunResult run)
    {
        return new IngestionRunResponse(
            run.IngestionId,
            run.KnowledgeBaseId,
            run.DataSourceId,
            run.SourcePath,
            run.Status,
            run.ErrorMessage,
            run.CreatedAt,
            run.StartedAt,
            run.CompletedAt,
            new IngestionRunSummaryResponse(
                run.Summary.TotalItems,
                run.Summary.CompletedItems,
                run.Summary.FailedItems,
                run.Summary.SkippedItems),
            run.Items.Select(x => new IngestionItemResponse(
                x.IngestionItemId,
                x.DocumentId,
                x.SourceUri,
                x.Status,
                x.ErrorMessage,
                x.CreatedAt,
                x.StartedAt,
                x.CompletedAt)).ToList());
    }
}
