using ProjectRag.Application.Models;
using System.Text.Json;

namespace ProjectRag.Infrastructure.AI;

internal sealed record QueryRewriteParseResult(
    QueryRewrite Rewrite,
    bool UsedFallback);

internal static class QueryRewriteResponseParser
{
    public static QueryRewriteParseResult Parse(string originalQuery, string responseText)
    {
        try
        {
            using var document = JsonDocument.Parse(JsonObjectExtractor.Extract(responseText));
            var root = document.RootElement;

            var semanticQuery = root.TryGetProperty("semanticQuery", out var semanticElement)
                ? semanticElement.GetString()
                : null;

            var keywordQuery = root.TryGetProperty("keywordQuery", out var keywordElement)
                ? keywordElement.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(semanticQuery)
                || string.IsNullOrWhiteSpace(keywordQuery))
            {
                return Fallback(originalQuery);
            }

            var rewrite = new QueryRewrite(
                OriginalQuery: originalQuery,
                SemanticQuery: semanticQuery,
                KeywordQuery: keywordQuery,
                Status: "rewritten");

            return new QueryRewriteParseResult(rewrite, UsedFallback: false);
        }
        catch
        {
            return Fallback(originalQuery);
        }
    }

    public static QueryRewriteParseResult Fallback(string query)
    {
        var rewrite = new QueryRewrite(
            OriginalQuery: query,
            SemanticQuery: query,
            KeywordQuery: query,
            Status: "fallback");

        return new QueryRewriteParseResult(rewrite, UsedFallback: true);
    }
}
