using Microsoft.Extensions.AI;
using ProjectRag.Application.Abstractions;
using ProjectRag.Application.Models;
using ProjectRag.Application.Telemetry;

namespace ProjectRag.Infrastructure.AI;

internal sealed class LlmQueryRewriteService : IQueryRewriteService
{
    private readonly IChatClient _chatClient;

    public LlmQueryRewriteService(IChatClient chatClient)
    {
        _chatClient = chatClient;
    }

    public async Task<QueryRewrite> RewriteAsync(string query, CancellationToken cancellationToken)
    {
        using var activity = ProjectRagTelemetry.ActivitySource.StartActivity("rag.query_rewrite");
        activity?.SetTag("rag.query.length", query.Length);

        if (string.IsNullOrWhiteSpace(query))
        {
            activity?.SetTag("rag.query_rewrite.status", "fallback");
            activity?.SetTag("rag.query_rewrite.fallback", true);
            return QueryRewriteResponseParser.Fallback(query).Rewrite;
        }

        try
        {
            var response = await _chatClient.GetResponseAsync(
                BuildPrompt(query),
                cancellationToken: cancellationToken);

            var parseResult = QueryRewriteResponseParser.Parse(query, response.Text);
            var rewrite = parseResult.Rewrite;

            activity?.SetTag("rag.query_rewrite.status", rewrite.Status);
            activity?.SetTag("rag.query_rewrite.fallback", parseResult.UsedFallback);
            activity?.SetTag("rag.semantic_query.length", rewrite.SemanticQuery.Length);
            activity?.SetTag("rag.keyword_query.length", rewrite.KeywordQuery.Length);

            return rewrite;
        }
        catch (Exception ex)
        {
            activity?.SetTag("rag.query_rewrite.status", "fallback");
            activity?.SetTag("rag.query_rewrite.fallback", true);
            activity?.SetTag("rag.error.type", ex.GetType().Name);
            return QueryRewriteResponseParser.Fallback(query).Rewrite;
        }
    }

    private static string BuildPrompt(string query)
    {
        return $$"""
            You rewrite user questions into search queries for a retrieval-augmented generation system.

            Return JSON only. Do not include markdown fences, commentary, or explanations.

            The JSON object must have this shape:
            {
                "semanticQuery": "natural language search query for vector retrieval",
                "keywordQuery": "keyword query for full-text retrieval"
            }

            Rules:
            - Preserve the user's intent.
            - Expand abbreviations or vague wording when useful.
            - The semantic query should read like a concise search sentence.
            - The keyword query may use quoted phrases and OR.
            - Do not invent facts, names, dates, or entities not implied by the question.

            User question:
            {{query}}
            """;
    }

}
