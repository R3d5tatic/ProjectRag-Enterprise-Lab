namespace ProjectRag.Infrastructure.Options;

internal sealed class EmbeddingRuntimeOptions
{
    public const string SectionName = "Embedding";

    public string Provider { get; set; } = AiProviderNames.Ollama;
    public string Endpoint { get; set; } = "";
    public string Model { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 300;
}
