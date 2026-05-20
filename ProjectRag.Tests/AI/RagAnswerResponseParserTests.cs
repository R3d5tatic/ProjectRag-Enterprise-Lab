using ProjectRag.Application.Models;
using ProjectRag.Domain.Enums;
using ProjectRag.Infrastructure.AI;

namespace ProjectRag.Tests.AI;

public sealed class RagAnswerResponseParserTests
{
    [Fact]
    public void Parse_returns_answered_result_for_valid_claim_citation()
    {
        var hit = Hit("Late balances may receive a monthly fee.");

        var result = RagAnswerResponseParser.Parse(
            """
            {
              "answerStatus": "answered",
              "answer": "Late balances may receive a monthly fee.",
              "claims": [
                {
                  "text": "Late balances may receive a monthly fee.",
                  "sourceIndexes": [1]
                }
              ]
            }
            """,
            [hit]);

        Assert.Equal("answered", result.AnswerStatus);
        Assert.False(result.UsedFallback);
        Assert.Contains("monthly fee", result.Answer);

        var claim = Assert.Single(result.Claims);
        Assert.Equal(hit.ChunkId, Assert.Single(claim.CitationChunkIds));
    }

    [Fact]
    public void Parse_falls_back_for_invalid_json()
    {
        var result = RagAnswerResponseParser.Parse("not json", [Hit("source")]);

        Assert.Equal("insufficientContext", result.AnswerStatus);
        Assert.True(result.UsedFallback);
        Assert.Empty(result.Claims);
    }

    [Fact]
    public void Parse_ignores_bad_citation_indexes_and_falls_back_when_no_valid_claims_remain()
    {
        var result = RagAnswerResponseParser.Parse(
            """
            {
              "answerStatus": "answered",
              "answer": "Late balances may receive a monthly fee.",
              "claims": [
                {
                  "text": "Late balances may receive a monthly fee.",
                  "sourceIndexes": [2]
                }
              ]
            }
            """,
            [Hit("source")]);

        Assert.Equal("insufficientContext", result.AnswerStatus);
        Assert.True(result.UsedFallback);
        Assert.Empty(result.Claims);
    }

    [Fact]
    public void Parse_returns_model_insufficient_context_without_claims()
    {
        var result = RagAnswerResponseParser.Parse(
            """
            {
              "answerStatus": "insufficientContext",
              "answer": "There is not enough information."
            }
            """,
            [Hit("source")]);

        Assert.Equal("insufficientContext", result.AnswerStatus);
        Assert.False(result.UsedFallback);
        Assert.Equal("There is not enough information.", result.Answer);
        Assert.Empty(result.Claims);
    }

    private static SearchHit Hit(string text)
    {
        return new SearchHit(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "source.md",
            text,
            RrfScore: 0.01,
            PageNumber: null,
            ChunkKind.Paragraph,
            SectionTitle: null,
            MatchedBy: "hybrid");
    }
}
