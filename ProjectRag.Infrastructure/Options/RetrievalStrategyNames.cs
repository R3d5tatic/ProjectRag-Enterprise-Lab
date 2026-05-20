namespace ProjectRag.Infrastructure.Options;

internal static class RetrievalStrategyNames
{
    public const string Hybrid = "hybrid";

    public const string ApplicationRrf = "application_rrf";
    public const string ElasticNativeRrf = "elastic_native_rrf";

    public const string LlmReranker = "llm";
    public const string ElasticSemanticReranker = "elastic_semantic";
    public const string NoReranker = "none";

    public static bool IsSupportedRetrievalMode(string? mode)
    {
        return mode?.Equals(Hybrid, StringComparison.OrdinalIgnoreCase) == true;
    }

    public static bool IsSupportedFusionMode(string? mode)
    {
        return mode?.Equals(ApplicationRrf, StringComparison.OrdinalIgnoreCase) == true
            || mode?.Equals(ElasticNativeRrf, StringComparison.OrdinalIgnoreCase) == true;
    }

    public static bool IsSupportedRerankerMode(string? mode)
    {
        return mode?.Equals(LlmReranker, StringComparison.OrdinalIgnoreCase) == true
            || mode?.Equals(ElasticSemanticReranker, StringComparison.OrdinalIgnoreCase) == true
            || mode?.Equals(NoReranker, StringComparison.OrdinalIgnoreCase) == true;
    }
}
