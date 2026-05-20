using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ProjectRag.Infrastructure;
using ProjectRag.Infrastructure.Options;

namespace ProjectRag.Tests.Infrastructure;

public sealed class AiRuntimeOptionsValidationTests
{
    [Theory]
    [InlineData("Ollama")]
    [InlineData("LMStudio")]
    [InlineData("FoundryLocal")]
    public void ChatRuntimeOptions_accepts_supported_provider(string provider)
    {
        var options = GetOptions<ChatRuntimeOptions>(new Dictionary<string, string?>
        {
            ["Chat:Provider"] = provider,
            ["Chat:ApiKey"] = ""
        });

        Assert.Equal(provider, options.Provider);
    }

    [Theory]
    [InlineData("AzureOpenAI")]
    [InlineData("UnknownAI")]
    public void ChatRuntimeOptions_rejects_unsupported_provider(string provider)
    {
        var exception = Assert.Throws<OptionsValidationException>(() =>
            GetOptions<ChatRuntimeOptions>(new Dictionary<string, string?>
            {
                ["Chat:Provider"] = provider
            }));

        Assert.Contains("Chat provider is not supported.", exception.Failures);
    }

    [Theory]
    [InlineData("Ollama")]
    [InlineData("LMStudio")]
    [InlineData("FoundryLocal")]
    public void EmbeddingRuntimeOptions_accepts_supported_provider(string provider)
    {
        var options = GetOptions<EmbeddingRuntimeOptions>(new Dictionary<string, string?>
        {
            ["Embedding:Provider"] = provider,
            ["Embedding:ApiKey"] = ""
        });

        Assert.Equal(provider, options.Provider);
    }

    [Theory]
    [InlineData("AzureOpenAI")]
    [InlineData("UnknownAI")]
    public void EmbeddingRuntimeOptions_rejects_unsupported_provider(string provider)
    {
        var exception = Assert.Throws<OptionsValidationException>(() =>
            GetOptions<EmbeddingRuntimeOptions>(new Dictionary<string, string?>
            {
                ["Embedding:Provider"] = provider
            }));

        Assert.Contains("Embedding provider is not supported.", exception.Failures);
    }

    [Fact]
    public void ElasticsearchOptions_accepts_semantic_text_retrieval_when_inference_id_is_configured()
    {
        var options = GetOptions<ElasticsearchOptions>(new Dictionary<string, string?>
        {
            ["Elasticsearch:EnableSemanticTextRetrieval"] = "true",
            ["Elasticsearch:SemanticTextInferenceId"] = ".elser-2-elasticsearch"
        });

        Assert.True(options.EnableSemanticTextRetrieval);
        Assert.Equal(".elser-2-elasticsearch", options.SemanticTextInferenceId);
    }

    [Fact]
    public void ElasticsearchOptions_requires_semantic_text_inference_id_when_enabled()
    {
        var exception = Assert.Throws<OptionsValidationException>(() =>
            GetOptions<ElasticsearchOptions>(new Dictionary<string, string?>
            {
                ["Elasticsearch:EnableSemanticTextRetrieval"] = "true",
                ["Elasticsearch:SemanticTextInferenceId"] = ""
            }));

        Assert.Contains(
            "Elasticsearch semantic text inference id is required when semantic text retrieval is enabled.",
            exception.Failures);
    }

    private static TOptions GetOptions<TOptions>(
        IReadOnlyDictionary<string, string?>? overrides = null)
        where TOptions : class
    {
        var configurationValues = new Dictionary<string, string?>
        {
            ["ConnectionStrings:ProjectRagDb"] = "Data Source=:memory:",
            ["Embedding:Provider"] = "Ollama",
            ["Embedding:Endpoint"] = "http://localhost:11434",
            ["Embedding:Model"] = "nomic-embed-text",
            ["Embedding:ApiKey"] = "",
            ["Embedding:TimeoutSeconds"] = "300",
            ["Chat:Provider"] = "Ollama",
            ["Chat:Endpoint"] = "http://localhost:11434",
            ["Chat:Model"] = "llama3.2",
            ["Chat:ApiKey"] = "",
            ["Chat:TimeoutSeconds"] = "300",
            ["Elasticsearch:Endpoint"] = "http://localhost:9200",
            ["Elasticsearch:IndexName"] = "projectrag-chunks",
            ["Elasticsearch:TimeoutSeconds"] = "120",
            ["Elasticsearch:EnableSemanticTextRetrieval"] = "false",
            ["Elasticsearch:SemanticTextInferenceId"] = "",
            ["Retrieval:CandidateCount"] = "30",
            ["Retrieval:MaxCandidateCount"] = "100",
            ["Retrieval:MaxTopK"] = "20",
            ["Retrieval:RrfConstant"] = "60",
            ["Retrieval:RerankerMaxTextChars"] = "800",
            ["Retrieval:RetrievalMode"] = RetrievalStrategyNames.Hybrid,
            ["Retrieval:FusionMode"] = RetrievalStrategyNames.ApplicationRrf,
            ["Retrieval:EnableReranking"] = "true",
            ["Retrieval:RerankerMode"] = RetrievalStrategyNames.LlmReranker,
            ["Retrieval:ElasticKnnNumCandidatesMultiplier"] = "5",
            ["Retrieval:ElasticKnnMinNumCandidates"] = "50",
            ["Retrieval:ElasticRrfRankWindowMultiplier"] = "1"
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
        services.AddInfrastructureForApi(configuration);

        using var provider = services.BuildServiceProvider();

        return provider.GetRequiredService<IOptions<TOptions>>().Value;
    }
}
