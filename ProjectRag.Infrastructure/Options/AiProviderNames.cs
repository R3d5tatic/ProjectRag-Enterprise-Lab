namespace ProjectRag.Infrastructure.Options;

internal static class AiProviderNames
{
    public const string Ollama = "Ollama";
    public const string LmStudio = "LMStudio";
    public const string FoundryLocal = "FoundryLocal";
    public const string AzureOpenAI = "AzureOpenAI";

    public static bool IsOllama(string provider)
    {
        return provider.Equals(Ollama, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsLmStudio(string provider)
    {
        return provider.Equals(LmStudio, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsFoundryLocal(string provider)
    {
        return provider.Equals(FoundryLocal, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsAzureOpenAI(string provider)
    {
        return provider.Equals(AzureOpenAI, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsOpenAICompatibleLocalProvider(string provider)
    {
        return IsLmStudio(provider)
            || IsFoundryLocal(provider);
    }

    public static bool IsSupportedChatProvider(string provider)
    {
        return IsOllama(provider)
            || IsOpenAICompatibleLocalProvider(provider);
    }

    public static bool IsSupportedEmbeddingProvider(string provider)
    {
        return IsOllama(provider)
            || IsOpenAICompatibleLocalProvider(provider);
    }
}
