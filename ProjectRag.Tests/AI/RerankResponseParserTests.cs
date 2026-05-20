using ProjectRag.Application.Models;
using ProjectRag.Domain.Enums;
using ProjectRag.Infrastructure.AI;

namespace ProjectRag.Tests.AI;

public sealed class RerankResponseParserTests
{
    [Fact]
    public void Parse_orders_candidates_by_valid_scores()
    {
        var first = Hit("first", rrfScore: 0.9);
        var second = Hit("second", rrfScore: 0.1);

        var parseResult = RerankResponseParser.Parse(
            [first, second],
            """
            {
              "scores": [
                { "index": 1, "score": 0.10 },
                { "index": 2, "score": 0.95 }
              ]
            }
            """,
            topK: 2);

        Assert.False(parseResult.UsedFallback);
        var results = parseResult.Results;

        Assert.Equal(second.ChunkId, results[0].ChunkId);
        Assert.Equal(0.95, results[0].RerankScore);

        Assert.Equal(first.ChunkId, results[1].ChunkId);
        Assert.Equal(0.10, results[1].RerankScore);
    }

    [Fact]
    public void Parse_falls_back_for_invalid_json()
    {
        var first = Hit("first");
        var second = Hit("second");

        var parseResult = RerankResponseParser.Parse(
            [first, second],
            "not json",
            topK: 1);

        Assert.True(parseResult.UsedFallback);
        var results = parseResult.Results;

        var result = Assert.Single(results);
        Assert.Equal(first.ChunkId, result.ChunkId);
        Assert.Null(result.RerankScore);
    }

    [Fact]
    public void Parse_falls_back_when_no_valid_scores_exist()
    {
        var first = Hit("first");
        var second = Hit("second");

        var parseResult = RerankResponseParser.Parse(
            [first, second],
            """
            {
              "scores": [
                { "index": 3, "score": 0.95 }
              ]
            }
            """,
            topK: 2);

        Assert.True(parseResult.UsedFallback);
        var results = parseResult.Results;

        Assert.Equal(first.ChunkId, results[0].ChunkId);
        Assert.Equal(second.ChunkId, results[1].ChunkId);
        Assert.All(results, result => Assert.Null(result.RerankScore));
    }

    private static SearchHit Hit(string text, double rrfScore = 0.01)
    {
        return new SearchHit(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "source.md",
            text,
            rrfScore,
            PageNumber: null,
            ChunkKind.Paragraph,
            SectionTitle: null,
            MatchedBy: "hybrid");
    }
}
