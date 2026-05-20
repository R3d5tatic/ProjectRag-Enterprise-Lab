using ProjectRag.Infrastructure.AI;

namespace ProjectRag.Tests.AI;

public sealed class QueryRewriteResponseParserTests
{
    [Fact]
    public void Parse_returns_rewritten_queries_for_valid_json()
    {
        var parseResult = QueryRewriteResponseParser.Parse(
            "late fees",
            """
            {
              "semanticQuery": "late payment fees",
              "keywordQuery": "\"late payment\" OR fees"
            }
            """);
        var result = parseResult.Rewrite;

        Assert.False(parseResult.UsedFallback);
        Assert.Equal("late fees", result.OriginalQuery);
        Assert.Equal("late payment fees", result.SemanticQuery);
        Assert.Equal("\"late payment\" OR fees", result.KeywordQuery);
        Assert.Equal("rewritten", result.Status);
    }

    [Fact]
    public void Parse_falls_back_for_invalid_json()
    {
        var parseResult = QueryRewriteResponseParser.Parse("late fees", "not json");
        var result = parseResult.Rewrite;

        Assert.True(parseResult.UsedFallback);
        Assert.Equal("late fees", result.OriginalQuery);
        Assert.Equal("late fees", result.SemanticQuery);
        Assert.Equal("late fees", result.KeywordQuery);
        Assert.Equal("fallback", result.Status);
    }

    [Fact]
    public void Parse_falls_back_when_required_fields_are_missing()
    {
        var parseResult = QueryRewriteResponseParser.Parse(
            "late fees",
            """
            {
              "semanticQuery": "late payment fees"
            }
            """);
        var result = parseResult.Rewrite;

        Assert.True(parseResult.UsedFallback);
        Assert.Equal("fallback", result.Status);
        Assert.Equal("late fees", result.SemanticQuery);
        Assert.Equal("late fees", result.KeywordQuery);
    }
}
