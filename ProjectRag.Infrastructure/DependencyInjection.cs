using Azure.AI.DocumentIntelligence;
using Elastic.Clients.Elasticsearch;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ProjectRag.Application.Abstractions;
using ProjectRag.Infrastructure.AI;
using ProjectRag.Infrastructure.DocumentIntelligence;
using ProjectRag.Infrastructure.Ingestion;
using ProjectRag.Infrastructure.Options;
using ProjectRag.Infrastructure.Search;

namespace ProjectRag.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureForApi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddPersistence(configuration);
        services.AddChatGeneration(configuration);
        services.AddEmbeddingGeneration(configuration);
        services.AddElasticsearch(configuration);
        services.AddIngestionQueueing(configuration);
        services.AddQueryAndRetrieval(configuration);

        return services;
    }

    public static IServiceCollection AddInfrastructureForWorker(
    this IServiceCollection services,
    IConfiguration configuration)
    {
        services.AddPersistence(configuration);
        services.AddAzureDocumentIntelligence(configuration);
        services.AddEmbeddingGeneration(configuration);
        services.AddElasticsearch(configuration);
        services.AddIngestionProcessing(configuration);

        return services;
    }

    private static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("ProjectRagDb")
            ?? throw new InvalidOperationException("Connection string 'ProjectRagDb' was not found.");

        services.AddDbContext<RagDbContext>(options => options.UseSqlite(connectionString));

        return services;
    }

    private static IServiceCollection AddEmbeddingGeneration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<EmbeddingRuntimeOptions>()
            .Bind(configuration.GetSection(EmbeddingRuntimeOptions.SectionName))
            .Validate(x => !string.IsNullOrWhiteSpace(x.Provider), "Embedding provider is required.")
            .Validate(
                x => AiProviderNames.IsSupportedEmbeddingProvider(x.Provider),
                "Embedding provider is not supported.")
            .Validate(x => !string.IsNullOrWhiteSpace(x.Endpoint), "Embedding endpoint is required.")
            .Validate(x => Uri.TryCreate(x.Endpoint, UriKind.Absolute, out _), "Embedding endpoint must be an absolute URI.")
            .Validate(x => !string.IsNullOrWhiteSpace(x.Model), "Embedding model is required.")
            .Validate(x => x.TimeoutSeconds > 0, "Embedding timeout must be greater than zero.")
            .ValidateOnStart();

        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<EmbeddingRuntimeOptions>>().Value;

            return EmbeddingGeneratorFactory.Create(options);
        });

        return services;
    }

    private static IServiceCollection AddChatGeneration(
    this IServiceCollection services,
    IConfiguration configuration)
    {
        services.AddOptions<ChatRuntimeOptions>()
            .Bind(configuration.GetSection(ChatRuntimeOptions.SectionName))
            .Validate(x => !string.IsNullOrWhiteSpace(x.Provider), "Chat provider is required.")
            .Validate(
                x => AiProviderNames.IsSupportedChatProvider(x.Provider),
                "Chat provider is not supported.")
            .Validate(x => !string.IsNullOrWhiteSpace(x.Endpoint), "Chat endpoint is required.")
            .Validate(x => Uri.TryCreate(x.Endpoint, UriKind.Absolute, out _), "Chat endpoint must be an absolute URI.")
            .Validate(x => !string.IsNullOrWhiteSpace(x.Model), "Chat model is required.")
            .Validate(x => x.TimeoutSeconds > 0, "Chat timeout must be greater than zero.")
            .ValidateOnStart();

        services.AddSingleton<IChatClient>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ChatRuntimeOptions>>().Value;

            return ChatClientFactory.Create(options);
        });

        return services;
    }
    private static IServiceCollection AddAzureDocumentIntelligence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<DocumentIntelligenceOptions>()
            .Bind(configuration.GetSection(DocumentIntelligenceOptions.SectionName))
            .Validate(x => !string.IsNullOrWhiteSpace(x.Endpoint), "Document Intelligence endpoint is required.")
            .Validate(x => Uri.TryCreate(x.Endpoint, UriKind.Absolute, out _), "Document Intelligence endpoint must be an absolute URI.")
            .Validate(x => !string.IsNullOrWhiteSpace(x.ApiKey), "Document Intelligence API key is required.")
            .Validate(x => !string.IsNullOrWhiteSpace(x.ModelId), "Document Intelligence model id is required.")
            .ValidateOnStart();

        services.AddSingleton<DocumentIntelligenceClient>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<DocumentIntelligenceOptions>>().Value;

            return new DocumentIntelligenceClient(
                new Uri(options.Endpoint),
                new Azure.AzureKeyCredential(options.ApiKey));
        });

        return services;
    }

    private static IServiceCollection AddIngestionQueueing(
        this IServiceCollection services,
        IConfiguration configuration)
    {

        services.AddScoped<IIngestionRunQueueService, IngestionRunQueueService>();
        return services;
    }

    private static IServiceCollection AddIngestionProcessing(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<ChunkingOptions>()
            .Bind(configuration.GetSection(ChunkingOptions.SectionName))
            .Validate(x => !string.IsNullOrWhiteSpace(x.Strategy), "Chunking strategy is required.")
            .Validate(
                x => ChunkingStrategyNames.IsSupported(x.Strategy),
                $"Chunking strategy must be '{ChunkingStrategyNames.Paragraph}'.")
            .Validate(x => x.MaxChunkSize > 0, "Chunking max chunk size must be greater than zero.")
            .ValidateOnStart();

        services.AddSingleton<ITextChunker, SimpleTextChunker>();
        services.AddSingleton<IDocumentExtractor, AzureDocumentIntelligenceExtractor>();
        services.AddScoped<ITextDocumentIngestionService, FileSystemDocumentIngestionService>();
        services.AddScoped<IIngestionRunProcessor, IngestionRunProcessor>();
        services.AddScoped<IPendingIngestionRunQueue, DatabaseIngestionRunQueue>();

        return services;
    }

    private static IServiceCollection AddElasticsearch(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<ElasticsearchOptions>()
            .Bind(configuration.GetSection(ElasticsearchOptions.SectionName))
            .Validate(x => !string.IsNullOrWhiteSpace(x.Endpoint), "Elasticsearch endpoint is required.")
            .Validate(x => Uri.TryCreate(x.Endpoint, UriKind.Absolute, out _), "Elasticsearch endpoint must be an absolute URI.")
            .Validate(x => !string.IsNullOrWhiteSpace(x.IndexName), "Elasticsearch index name is required.")
            .Validate(x => x.TimeoutSeconds > 0, "Elasticsearch timeout must be greater than zero.")
            .Validate(
                x => !x.EnableSemanticTextRetrieval || !string.IsNullOrWhiteSpace(x.SemanticTextInferenceId),
                "Elasticsearch semantic text inference id is required when semantic text retrieval is enabled.")
            .ValidateOnStart();

        services.AddSingleton<ElasticsearchClient>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ElasticsearchOptions>>().Value;

            var settings = new ElasticsearchClientSettings(new Uri(options.Endpoint))
                .DefaultIndex(options.IndexName)
                .RequestTimeout(TimeSpan.FromSeconds(options.TimeoutSeconds));

            return new ElasticsearchClient(settings);
        });

        services.AddScoped<ISearchIndexService, ElasticSearchIndexService>();
        services.AddScoped<IKeywordSearchService, ElasticKeywordSearchService>();
        services.AddScoped<IVectorSearchService, ElasticVectorSearchService>();

        return services;
    }

    private static IServiceCollection AddQueryAndRetrieval(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<RetrievalOptions>()
            .Bind(configuration.GetSection(RetrievalOptions.SectionName))
            .Validate(x => x.CandidateCount > 0, "Retrieval candidate count must be greater than zero.")
            .Validate(x => x.MaxCandidateCount > 0, "Retrieval max candidate count must be greater than zero.")
            .Validate(x => x.MaxTopK > 0, "Retrieval max top-k must be greater than zero.")
            .Validate(x => x.CandidateCount <= x.MaxCandidateCount, "Retrieval candidate count must be less than or equal to max candidate count.")
            .Validate(x => x.MaxTopK <= x.MaxCandidateCount, "Retrieval max top-k must be less than or equal to max candidate count.")
            .Validate(x => x.RrfConstant > 0, "Retrieval RRF constant must be greater than zero.")
            .Validate(x => x.RerankerMaxTextChars > 0, "Retrieval reranker max text chars must be greater than zero.")
            .Validate(x => x.ElasticKnnNumCandidatesMultiplier > 0, "Elastic kNN num candidates multiplier must be greater than zero.")
            .Validate(x => x.ElasticKnnMinNumCandidates > 0, "Elastic kNN minimum num candidates must be greater than zero.")
            .Validate(x => x.ElasticRrfRankWindowMultiplier > 0, "Elastic RRF rank window multiplier must be greater than zero.")
            .Validate(x => !string.IsNullOrWhiteSpace(x.RetrievalMode), "Retrieval mode is required.")
            .Validate(x => !string.IsNullOrWhiteSpace(x.FusionMode), "Retrieval fusion mode is required.")
            .Validate(x => !string.IsNullOrWhiteSpace(x.RerankerMode), "Retrieval reranker mode is required.")
            .Validate(
                x => RetrievalStrategyNames.IsSupportedRetrievalMode(x.RetrievalMode),
                $"Retrieval mode must be '{RetrievalStrategyNames.Hybrid}'.")
            .Validate(
                x => RetrievalStrategyNames.IsSupportedFusionMode(x.FusionMode),
                $"Retrieval fusion mode must be '{RetrievalStrategyNames.ApplicationRrf}' or '{RetrievalStrategyNames.ElasticNativeRrf}'.")
            .Validate(
                x => RetrievalStrategyNames.IsSupportedRerankerMode(x.RerankerMode),
                $"Retrieval reranker mode must be '{RetrievalStrategyNames.LlmReranker}', '{RetrievalStrategyNames.ElasticSemanticReranker}', or '{RetrievalStrategyNames.NoReranker}'.")
            .Validate(
                x => x.EnableReranking || x.RerankerMode.Equals(RetrievalStrategyNames.NoReranker, StringComparison.OrdinalIgnoreCase),
                $"Retrieval reranker mode must be '{RetrievalStrategyNames.NoReranker}' when reranking is disabled.")
            .Validate(
                x => !x.RerankerMode.Equals(RetrievalStrategyNames.ElasticSemanticReranker, StringComparison.OrdinalIgnoreCase)
                    || x.FusionMode.Equals(RetrievalStrategyNames.ElasticNativeRrf, StringComparison.OrdinalIgnoreCase),
                $"Retrieval reranker mode '{RetrievalStrategyNames.ElasticSemanticReranker}' requires fusion mode '{RetrievalStrategyNames.ElasticNativeRrf}'.")
            .Validate(
                x => !x.RerankerMode.Equals(RetrievalStrategyNames.ElasticSemanticReranker, StringComparison.OrdinalIgnoreCase)
                    || !string.IsNullOrWhiteSpace(x.ElasticRerankInferenceId),
                "Elastic rerank inference id is required when Elastic semantic reranking is enabled.")
            .ValidateOnStart();

        services.AddScoped<IQueryRewriteService, LlmQueryRewriteService>();
        services.AddScoped<HybridRetrievalSearchService>();
        services.AddScoped<ElasticNativeRrfSearchService>();
        services.AddScoped<IRetrievalSearchService>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<RetrievalOptions>>().Value;

            return options.FusionMode.Equals(RetrievalStrategyNames.ElasticNativeRrf, StringComparison.OrdinalIgnoreCase)
                ? serviceProvider.GetRequiredService<ElasticNativeRrfSearchService>()
                : serviceProvider.GetRequiredService<HybridRetrievalSearchService>();
        });
        services.AddScoped<IRankFusionService, RrfRankFusionService>();
        services.AddScoped<IRerankerService, LlmRerankerService>();
        services.AddScoped<IRagAnswerService, RagAnswerService>();

        return services;
    }
}

