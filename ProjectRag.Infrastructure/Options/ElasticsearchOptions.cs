namespace ProjectRag.Infrastructure.Options;

internal sealed class ElasticsearchOptions
{
    public const string SectionName = "Elasticsearch";

    public string Endpoint { get; set; } = "http://localhost:9200";
    public string IndexName { get; set; } = "projectrag-chunks";
    public int TimeoutSeconds { get; set; } = 120;
    public bool EnableSemanticTextRetrieval { get; set; } = false;
    public string? SemanticTextInferenceId { get; set; }
}
