using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ProjectRag.Infrastructure;
using ProjectRag.Infrastructure.Options;

namespace ProjectRag.Tests.Infrastructure;

public sealed class ChunkingOptionsValidationTests
{
    [Fact]
    public void ChunkingOptions_accepts_current_supported_strategy()
    {
        var options = GetChunkingOptions();

        Assert.Equal(ChunkingStrategyNames.Paragraph, options.Strategy);
        Assert.Equal(1200, options.MaxChunkSize);
    }

    [Fact]
    public void ChunkingOptions_accepts_supported_strategy_case_insensitively()
    {
        var options = GetChunkingOptions(new Dictionary<string, string?>
        {
            ["Chunking:Strategy"] = "Paragraph"
        });

        Assert.Equal("Paragraph", options.Strategy);
    }

    [Fact]
    public void ChunkingOptions_rejects_unsupported_strategy()
    {
        var exception = Assert.Throws<OptionsValidationException>(() =>
            GetChunkingOptions(new Dictionary<string, string?>
            {
                ["Chunking:Strategy"] = "semantic"
            }));

        Assert.Contains("Chunking strategy must be 'paragraph'.", exception.Failures);
    }

    [Fact]
    public void ChunkingOptions_rejects_non_positive_max_chunk_size()
    {
        var exception = Assert.Throws<OptionsValidationException>(() =>
            GetChunkingOptions(new Dictionary<string, string?>
            {
                ["Chunking:MaxChunkSize"] = "0"
            }));

        Assert.Contains("Chunking max chunk size must be greater than zero.", exception.Failures);
    }

    private static ChunkingOptions GetChunkingOptions(
        IReadOnlyDictionary<string, string?>? overrides = null)
    {
        var configurationValues = new Dictionary<string, string?>
        {
            ["ConnectionStrings:ProjectRagDb"] = "Data Source=:memory:",
            ["Embedding:Provider"] = "Ollama",
            ["Embedding:Endpoint"] = "http://localhost:11434",
            ["Embedding:Model"] = "nomic-embed-text",
            ["Embedding:TimeoutSeconds"] = "300",
            ["Elasticsearch:Endpoint"] = "http://localhost:9200",
            ["Elasticsearch:IndexName"] = "projectrag-chunks",
            ["Elasticsearch:TimeoutSeconds"] = "120",
            ["DocumentIntelligence:Endpoint"] = "https://example.test",
            ["DocumentIntelligence:ApiKey"] = "test-key",
            ["DocumentIntelligence:ModelId"] = "prebuilt-layout",
            ["Chunking:Strategy"] = ChunkingStrategyNames.Paragraph,
            ["Chunking:MaxChunkSize"] = "1200"
        };

        if (overrides is not null)
        {
            foreach (var item in overrides)
            {
                configurationValues[item.Key] = item.Value;
            }
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();

        var services = new ServiceCollection();
        services.AddInfrastructureForWorker(configuration);

        using var provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IOptions<ChunkingOptions>>().Value;
    }
}
