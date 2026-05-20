namespace ProjectRag.Contracts;

public sealed record AskRequest(
    string Question,
    int TopK = 10,
    SearchFiltersRequest? Filters = null);

public sealed record AskResponse(
    string Answer,
    string AnswerStatus,
    QueryRewriteResponse QueryRewrite,
    IReadOnlyList<ClaimResponse> Claims,
    IReadOnlyList<CitationResponse> Citations,
    RetrievalDiagnosticsResponse RetrievalDiagnostics,
    ModelInfoResponse ModelInfo);

public sealed record ClaimResponse(
    string Text,
    IReadOnlyList<string> CitationChunkIds);

public sealed record RetrievalDiagnosticsResponse(
    int RequestedTopK,
    int CandidateCount,
    int ReturnedContextCount,
    bool RerankingApplied,
    string RetrievalMode,
    string FusionMode,
    string RerankerMode,
    int RrfConstant);

public sealed record ModelInfoResponse(
    string ChatProvider,
    string ChatModel,
    string EmbeddingProvider,
    string EmbeddingModel);

public sealed record CitationResponse(
    string DocumentId,
    string ChunkId,
    string SourceUri,
    int? PageNumber,
    double RrfScore,
    double? RerankScore,
    double? VectorScore,
    double? KeywordScore,
    string MatchedBy,
    string Kind,
    string? SectionTitle);
