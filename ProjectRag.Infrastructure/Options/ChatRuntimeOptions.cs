namespace ProjectRag.Infrastructure.Options;

internal sealed class ChatRuntimeOptions
{
    public const string SectionName = "Chat";

    public string Provider { get; set; } = AiProviderNames.Ollama;
    public string Endpoint { get; set; } = "";
    public string Model { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 300;
}
