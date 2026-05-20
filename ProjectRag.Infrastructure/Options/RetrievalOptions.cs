namespace ProjectRag.Infrastructure.Options;

internal sealed class RetrievalOptions
{
    public const string SectionName = "Retrieval";

    public int CandidateCount { get; set; } = 30;
    public int MaxCandidateCount { get; set; } = 100;
    public int MaxTopK { get; set; } = 20;
    public int RrfConstant { get; set; } = 60;
    public int RerankerMaxTextChars { get; set; } = 800;
    public string RetrievalMode { get; set; } = RetrievalStrategyNames.Hybrid;
    public string FusionMode { get; set; } = RetrievalStrategyNames.ApplicationRrf;
    public bool EnableReranking { get; set; } = true;
    public string RerankerMode { get; set; } = RetrievalStrategyNames.LlmReranker;
    public string? ElasticRerankInferenceId { get; set; }
    public int ElasticRerankWindowSize { get; set; } = 50;
    public int ElasticKnnNumCandidatesMultiplier { get; set; } = 5;
    public int ElasticKnnMinNumCandidates { get; set; } = 50;
    public int ElasticRrfRankWindowMultiplier { get; set; } = 1;
}
