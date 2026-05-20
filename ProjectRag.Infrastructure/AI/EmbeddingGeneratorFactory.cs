using Microsoft.Extensions.AI;
using OllamaSharp;
using OpenAI;
using ProjectRag.Infrastructure.Options;
using System.ClientModel;

namespace ProjectRag.Infrastructure.AI;

internal static class EmbeddingGeneratorFactory
{
    public static IEmbeddingGenerator<string, Embedding<float>> Create(EmbeddingRuntimeOptions options)
    {
        if (AiProviderNames.IsOllama(options.Provider))
        {
            var httpClient = new HttpClient
            {
                BaseAddress = new Uri(options.Endpoint),
                Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds)
            };

            return new EmbeddingGeneratorBuilder<string, Embedding<float>>(
                    new OllamaApiClient(httpClient, options.Model))
                .UseOpenTelemetry(configure: o =>
                {
                    o.EnableSensitiveData = false;
                })
                .Build();
        }

        if (AiProviderNames.IsOpenAICompatibleLocalProvider(options.Provider))
        {
            var openAiClient = CreateOpenAICompatibleClient(options.Endpoint, options.ApiKey);

            return new EmbeddingGeneratorBuilder<string, Embedding<float>>(
                    openAiClient.GetEmbeddingClient(options.Model).AsIEmbeddingGenerator())
                .UseOpenTelemetry(configure: o =>
                {
                    o.EnableSensitiveData = false;
                })
                .Build();
        }

        throw new NotSupportedException($"Embedding provider '{options.Provider}' is not supported.");
    }

    private static OpenAIClient CreateOpenAICompatibleClient(string endpoint, string apiKey)
    {
        return new OpenAIClient(
            new ApiKeyCredential(apiKey),
            new OpenAIClientOptions
            {
                Endpoint = new Uri(endpoint)
            });
    }
}
