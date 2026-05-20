using ProjectRag.Application.Models;
using System.Text.Json;

namespace ProjectRag.Infrastructure.AI;

internal sealed record RerankParseResult(
    IReadOnlyList<SearchHit> Results,
    bool UsedFallback);

internal static class RerankResponseParser
{
    public static RerankParseResult Parse(
        IReadOnlyList<SearchHit> candidates,
        string responseText,
        int topK)
    {
        try
        {
            using var document = JsonDocument.Parse(JsonObjectExtractor.Extract(responseText));

            if (!document.RootElement.TryGetProperty("scores", out var scoresElement)
                || scoresElement.ValueKind is not JsonValueKind.Array)
            {
                return Fallback(candidates, topK);
            }

            var scoresByIndex = new Dictionary<int, double>();

            foreach (var scoreElement in scoresElement.EnumerateArray())
            {
                if (!scoreElement.TryGetProperty("index", out var indexElement)
                    || !scoreElement.TryGetProperty("score", out var scoreValueElement))
                {
                    continue;
                }

                var index = indexElement.GetInt32();
                var score = scoreValueElement.GetDouble();

                if (index < 1 || index > candidates.Count)
                {
                    continue;
                }

                scoresByIndex[index] = Math.Clamp(score, 0, 1);
            }

            if (scoresByIndex.Count == 0)
            {
                return Fallback(candidates, topK);
            }

            var results = candidates
                .Select((candidate, index) =>
                {
                    var oneBasedIndex = index + 1;
                    var rerankScore = scoresByIndex.TryGetValue(oneBasedIndex, out var score)
                        ? score
                        : 0;

                    return candidate with { RerankScore = rerankScore };
                })
                .OrderByDescending(x => x.RerankScore)
                .ThenByDescending(x => x.RrfScore)
                .Take(topK)
                .ToList();

            return new RerankParseResult(results, UsedFallback: false);
        }
        catch
        {
            return Fallback(candidates, topK);
        }
    }

    public static RerankParseResult Fallback(
        IReadOnlyList<SearchHit> candidates,
        int topK)
    {
        return new RerankParseResult(
            candidates.Take(topK).ToList(),
            UsedFallback: true);
    }
}
