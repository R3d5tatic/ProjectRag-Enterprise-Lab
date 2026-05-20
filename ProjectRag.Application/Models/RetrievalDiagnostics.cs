namespace ProjectRag.Application.Models;

public sealed record RetrievalDiagnostics(
    int RequestedTopK,
    int CandidateCount,
    int ReturnedContextCount,
    bool RerankingApplied,
    string RetrievalMode,
    string FusionMode,
    string RerankerMode,
    int RrfConstant);