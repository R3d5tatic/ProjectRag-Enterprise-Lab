using ProjectRag.Infrastructure.AI;
using ProjectRag.Infrastructure.Options;

namespace ProjectRag.Tests.AI;

public sealed class AiRuntimeFactoryTests
{
    [Theory]
    [InlineData("Ollama", "http://localhost:11434", "")]
    [InlineData("LMStudio", "http://localhost:1234/v1", "local-dev-placeholder")]
    [InlineData("FoundryLocal", "http://localhost:5273/v1", "local-dev-placeholder")]
    public void ChatClientFactory_creates_client_for_supported_provider(
        string provider,
        string endpoint,
        string apiKey)
    {
        var client = ChatClientFactory.Create(new ChatRuntimeOptions
        {
            Provider = provider,
            Endpoint = endpoint,
            Model = "llama3.2",
            ApiKey = apiKey,
            TimeoutSeconds = 300
        });

        Assert.NotNull(client);
    }

    [Fact]
    public void ChatClientFactory_rejects_unsupported_provider()
    {
        var exception = Assert.Throws<NotSupportedException>(() =>
            ChatClientFactory.Create(new ChatRuntimeOptions
            {
                Provider = "AzureOpenAI",
                Endpoint = "http://localhost:11434",
                Model = "gpt-4.1-mini",
                TimeoutSeconds = 300
            }));

        Assert.Contains("AzureOpenAI", exception.Message);
    }

    [Theory]
    [InlineData("Ollama", "http://localhost:11434", "")]
    [InlineData("LMStudio", "http://localhost:1234/v1", "local-dev-placeholder")]
    [InlineData("FoundryLocal", "http://localhost:5273/v1", "local-dev-placeholder")]
    public void EmbeddingGeneratorFactory_creates_generator_for_supported_provider(
        string provider,
        string endpoint,
        string apiKey)
    {
        var generator = EmbeddingGeneratorFactory.Create(new EmbeddingRuntimeOptions
        {
            Provider = provider,
            Endpoint = endpoint,
            Model = "nomic-embed-text",
            ApiKey = apiKey,
            TimeoutSeconds = 300
        });

        Assert.NotNull(generator);
    }

    [Fact]
    public void EmbeddingGeneratorFactory_rejects_unsupported_provider()
    {
        var exception = Assert.Throws<NotSupportedException>(() =>
            EmbeddingGeneratorFactory.Create(new EmbeddingRuntimeOptions
            {
                Provider = "AzureOpenAI",
                Endpoint = "http://localhost:11434",
                Model = "text-embedding-3-small",
                TimeoutSeconds = 300
            }));

        Assert.Contains("AzureOpenAI", exception.Message);
    }
}
