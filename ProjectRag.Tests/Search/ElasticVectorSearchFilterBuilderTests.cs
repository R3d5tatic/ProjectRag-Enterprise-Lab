using ProjectRag.Application.Models;
using ProjectRag.Infrastructure.Search;

namespace ProjectRag.Tests.Search;

public sealed class ElasticVectorSearchFilterBuilderTests
{
    [Fact]
    public void BuildEmbeddingModelFilter_uses_embedding_model_field_and_value()
    {
        var filter = ElasticVectorSearchFilterBuilder.BuildEmbeddingModelFilter("nomic-embed-text");

        Assert.Equal("embeddingModel", filter.Field.ToString());
        Assert.Equal("nomic-embed-text", filter.Value.ToString());
    }

    [Fact]
    public void Build_includes_existing_filters_and_embedding_model_filter()
    {
        var filters = new SearchFilters(SourceType: "localFile");

        var queries = ElasticVectorSearchFilterBuilder.Build(filters, "nomic-embed-text");

        Assert.Equal(2, queries.Count);
    }
}
