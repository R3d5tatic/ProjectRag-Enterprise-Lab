using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using ProjectRag.Application.Models;

namespace ProjectRag.Infrastructure.Search;

internal static class ElasticVectorSearchFilterBuilder
{
    public static ICollection<Query> Build(SearchFilters? filters, string embeddingModel)
    {
        var queries = ElasticSearchFilterBuilder.Build(filters);
        queries.Add(BuildEmbeddingModelFilter(embeddingModel));

        return queries;
    }

    public static TermQuery BuildEmbeddingModelFilter(string embeddingModel)
    {
        return new TermQuery
        {
            Field = new Field("embeddingModel"),
            Value = embeddingModel
        };
    }
}
