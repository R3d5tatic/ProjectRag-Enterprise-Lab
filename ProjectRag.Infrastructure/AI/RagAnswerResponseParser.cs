using ProjectRag.Application.Models;
using System.Text.Json;

namespace ProjectRag.Infrastructure.AI;

internal sealed record ParsedRagAnswer(
    string Answer,
    string AnswerStatus,
    IReadOnlyList<AnswerClaim> Claims,
    bool UsedFallback);

internal static class RagAnswerResponseParser
{
    public static ParsedRagAnswer Parse(string responseText, IReadOnlyList<SearchHit> hits)
    {
        try
        {
            using var document = JsonDocument.Parse(JsonObjectExtractor.Extract(responseText));
            var root = document.RootElement;

            var answerStatus = root.TryGetProperty("answerStatus", out var statusElement)
                ? statusElement.GetString()
                : null;

            var answer = root.TryGetProperty("answer", out var answerElement)
                ? answerElement.GetString()
                : null;

            if (answerStatus is not ("answered" or "insufficientContext")
                || string.IsNullOrWhiteSpace(answer))
            {
                return InsufficientContext();
            }

            if (answerStatus == "insufficientContext")
            {
                return new ParsedRagAnswer(answer, "insufficientContext", [], UsedFallback: false);
            }

            var claims = ParseClaims(root, hits);

            return claims.Count == 0
                ? InsufficientContext()
                : new ParsedRagAnswer(answer, "answered", claims, UsedFallback: false);
        }
        catch
        {
            return InsufficientContext();
        }
    }

    public static ParsedRagAnswer InsufficientContext()
    {
        return new ParsedRagAnswer(
            Answer: "I do not have enough information in the available documents to answer that question.",
            AnswerStatus: "insufficientContext",
            Claims: [],
            UsedFallback: true);
    }

    private static IReadOnlyList<AnswerClaim> ParseClaims(
        JsonElement root,
        IReadOnlyList<SearchHit> hits)
    {
        if (!root.TryGetProperty("claims", out var claimsElement)
            || claimsElement.ValueKind is not JsonValueKind.Array)
        {
            return [];
        }

        var claims = new List<AnswerClaim>();

        foreach (var claimElement in claimsElement.EnumerateArray())
        {
            if (!claimElement.TryGetProperty("text", out var textElement)
                || !claimElement.TryGetProperty("sourceIndexes", out var sourceIndexesElement)
                || sourceIndexesElement.ValueKind is not JsonValueKind.Array)
            {
                continue;
            }

            var text = textElement.GetString();

            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var citationChunkIds = sourceIndexesElement
                .EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.Number)
                .Select(x => x.GetInt32())
                .Where(index => index >= 1 && index <= hits.Count)
                .Select(index => hits[index - 1].ChunkId)
                .Distinct()
                .ToList();

            if (citationChunkIds.Count == 0)
            {
                continue;
            }

            claims.Add(new AnswerClaim(text, citationChunkIds));
        }

        return claims;
    }
}
