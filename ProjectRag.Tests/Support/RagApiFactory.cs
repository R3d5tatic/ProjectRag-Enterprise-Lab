using Azure.AI.DocumentIntelligence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ProjectRag.Application.Abstractions;
using ProjectRag.Infrastructure;
using ProjectRag.Infrastructure.Search;

namespace ProjectRag.Tests.Support;

public sealed class RagApiFactory : WebApplicationFactory<Program>, IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly IReadOnlyDictionary<string, string?>? _configurationOverrides;
    private readonly Func<IServiceProvider, IRetrievalSearchService>? _retrievalSearchServiceFactory;

    public RagApiFactory()
    {
    }

    internal RagApiFactory(
        IReadOnlyDictionary<string, string?> configurationOverrides,
        Func<IServiceProvider, IRetrievalSearchService>? retrievalSearchServiceFactory = null)
    {
        _configurationOverrides = configurationOverrides;
        _retrievalSearchServiceFactory = retrievalSearchServiceFactory;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();

        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            if (_configurationOverrides is not null)
            {
                configurationBuilder.AddInMemoryCollection(_configurationOverrides);
            }
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<RagDbContext>>();
            services.RemoveAll<DocumentIntelligenceClient>();
            services.RemoveAll<IDocumentExtractor>();
            services.RemoveAll<IEmbeddingGenerator<string, Embedding<float>>>();
            services.RemoveAll<IChatClient>();
            services.RemoveAll<ISearchIndexService>();
            services.RemoveAll<IQueryRewriteService>();
            services.RemoveAll<IVectorSearchService>();
            services.RemoveAll<IKeywordSearchService>();
            services.RemoveAll<IRetrievalSearchService>();
            services.RemoveAll<IRerankerService>();

            services.AddSingleton<IDocumentExtractor, FakeDocumentExtractor>();
            services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>, FakeEmbeddingGenerator>();
            services.AddSingleton<IChatClient, FakeChatClient>();
            services.AddSingleton<ISearchIndexService, FakeSearchIndexService>();
            services.AddScoped<IQueryRewriteService, FakeQueryRewriteService>();
            services.AddScoped<IVectorSearchService, FakeVectorSearchService>();
            services.AddScoped<IKeywordSearchService, InMemoryKeywordSearchService>();
            if (_retrievalSearchServiceFactory is null)
            {
                services.AddScoped<IRetrievalSearchService, HybridRetrievalSearchService>();
            }
            else
            {
                services.AddScoped<IRetrievalSearchService>(_retrievalSearchServiceFactory);
            }
            services.AddScoped<IRerankerService, FakeRerankerService>();

            services.AddDbContext<RagDbContext>(options =>
                options.UseSqlite(_connection));

            using var scope = services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<RagDbContext>();
            db.Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connection.Dispose();
        }
        base.Dispose(disposing);
    }
}
