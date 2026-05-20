using Microsoft.Extensions.AI;
using OllamaSharp;
using OpenAI;
using ProjectRag.Infrastructure.Options;
using System.ClientModel;

namespace ProjectRag.Infrastructure.AI;

internal static class ChatClientFactory
{
    public static IChatClient Create(ChatRuntimeOptions options)
    {
        if (AiProviderNames.IsOllama(options.Provider))
        {
            var httpClient = new HttpClient
            {
                BaseAddress = new Uri(options.Endpoint),
                Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds)
            };

            return new ChatClientBuilder(
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

            return new ChatClientBuilder(
                    openAiClient.GetChatClient(options.Model).AsIChatClient())
                .UseOpenTelemetry(configure: o =>
                {
                    o.EnableSensitiveData = false;
                })
                .Build();
        }

        throw new NotSupportedException($"Chat provider '{options.Provider}' is not supported.");
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
