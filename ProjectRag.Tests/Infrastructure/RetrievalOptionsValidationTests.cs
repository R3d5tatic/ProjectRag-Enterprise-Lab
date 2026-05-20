using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ProjectRag.Application.Abstractions;
using ProjectRag.Infrastructure;
using ProjectRag.Infrastructure.Options;
using ProjectRag.Infrastructure.Search;

namespace ProjectRag.Tests.Infrastructure;

public sealed class RetrievalOptionsValidationTests
{
    [Fact]
    public void RetrievalOptions_accepts_current_supported_modes()
    {
        var options = GetRetrievalOptions();

        Assert.Equal(RetrievalStrategyNames.Hybrid, options.RetrievalMode);
        Assert.Equal(RetrievalStrategyNames.ApplicationRrf, options.FusionMode);
        Assert.True(options.EnableReranking);
        Assert.Equal(RetrievalStrategyNames.LlmReranker, options.RerankerMode);
    }

    [Fact]
    public void RetrievalOptions_accepts_disabled_reranking_when_mode_is_none()
    {
        var options = GetRetrievalOptions(new Dictionary<string, string?>
        {
            ["Retrieval:EnableReranking"] = "false",
            ["Retrieval:RerankerMode"] = RetrievalStrategyNames.NoReranker
        });

        Assert.False(options.EnableReranking);
        Assert.Equal(RetrievalStrategyNames.NoReranker, options.RerankerMode);
    }

    [Fact]
    public void RetrievalOptions_accepts_elasticsearch_native_rrf_fusion_mode()
    {
        var options = GetRetrievalOptions(new Dictionary<string, string?>
        {
            ["Retrieval:FusionMode"] = RetrievalStrategyNames.ElasticNativeRrf
        });

        Assert.Equal(RetrievalStrategyNames.ElasticNativeRrf, options.FusionMode);
    }

    [Theory]
    [InlineData("Retrieval:RetrievalMode", "native_rrf", "Retrieval mode must be 'hybrid'.")]
    [InlineData("Retrieval:FusionMode", "unknown_rrf", "Retrieval fusion mode must be 'application_rrf' or 'elastic_native_rrf'.")]
    [InlineData("Retrieval:RerankerMode", "cross_encoder", "Retrieval reranker mode must be 'llm', 'elastic_semantic', or 'none'.")]
    public void RetrievalOptions_rejects_unsupported_modes(
        string key,
        string value,
        string expectedFailure)
    {
        var exception = Assert.Throws<OptionsValidationException>(() =>
            GetRetrievalOptions(new Dictionary<string, string?>
            {
                [key] = value
            }));

        Assert.Contains(expectedFailure, exception.Failures);
    }

    [Fact]
    public void RetrievalOptions_requires_none_reranker_mode_when_reranking_is_disabled()
    {
        var exception = Assert.Throws<OptionsValidationException>(() =>
            GetRetrievalOptions(new Dictionary<string, string?>
            {
                ["Retrieval:EnableReranking"] = "false",
                ["Retrieval:RerankerMode"] = RetrievalStrategyNames.LlmReranker
            }));

        Assert.Contains(
            "Retrieval reranker mode must be 'none' when reranking is disabled.",
            exception.Failures);
    }

    [Theory]
    [InlineData("Retrieval:ElasticKnnNumCandidatesMultiplier", "0", "Elastic kNN num candidates multiplier must be greater than zero.")]
    [InlineData("Retrieval:ElasticKnnMinNumCandidates", "0", "Elastic kNN minimum num candidates must be greater than zero.")]
    [InlineData("Retrieval:ElasticRrfRankWindowMultiplier", "0", "Elastic RRF rank window multiplier must be greater than zero.")]
    public void RetrievalOptions_rejects_invalid_elastic_native_tuning_options(
        string key,
        string value,
        string expectedFailure)
    {
        var exception = Assert.Throws<OptionsValidationException>(() =>
            GetRetrievalOptions(new Dictionary<string, string?>
            {
                [key] = value
            }));

        Assert.Contains(expectedFailure, exception.Failures);
    }

    [Fact]
    public void RetrievalService_uses_application_rrf_by_default()
    {
        using var provider = BuildServiceProvider();

        var service = provider.GetRequiredService<IRetrievalSearchService>();

        Assert.IsType<HybridRetrievalSearchService>(service);
    }

    [Fact]
    public void RetrievalService_uses_elasticsearch_native_rrf_when_configured()
    {
        using var provider = BuildServiceProvider(new Dictionary<string, string?>
        {
            ["Retrieval:FusionMode"] = RetrievalStrategyNames.ElasticNativeRrf
        });

        var service = provider.GetRequiredService<IRetrievalSearchService>();

        Assert.IsType<ElasticNativeRrfSearchService>(service);
    }

    private static RetrievalOptions GetRetrievalOptions(
        IReadOnlyDictionary<string, string?>? overrides = null)
    {
        using var provider = BuildServiceProvider(overrides);

        return provider.GetRequiredService<IOptions<RetrievalOptions>>().Value;
    }

    private static ServiceProvider BuildServiceProvider(
        IReadOnlyDictionary<string, string?>? overrides = null)
    {
        var configurationValues = new Dictionary<string, string?>
        {
            ["ConnectionStrings:ProjectRagDb"] = "Data Source=:memory:",
            ["Embedding:Provider"] = "Ollama",
            ["Embedding:Endpoint"] = "http://localhost:11434",
            ["Embedding:Model"] = "nomic-embed-text",
            ["Embedding:TimeoutSeconds"] = "300",
            ["Chat:Provider"] = "Ollama",
            ["Chat:Endpoint"] = "http://localhost:11434",
            ["Chat:Model"] = "llama3.2",
            ["Chat:TimeoutSeconds"] = "300",
            ["Elasticsearch:Endpoint"] = "http://localhost:9200",
            ["Elasticsearch:IndexName"] = "projectrag-chunks",
            ["Elasticsearch:TimeoutSeconds"] = "120",
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

        return services.BuildServiceProvider();
    }
}
