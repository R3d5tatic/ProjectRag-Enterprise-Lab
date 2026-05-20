# Future Fork Notes

ProjectRag stops at Phase 9 as a learning-focused RAG baseline. The notes below are not planned work for this repository. They are starting points for a future fork focused on hardening, provider experiments, Microsoft.Extensions.AI adoption, provider-native search features, and agent/tool orchestration.

## Fork Goals

- Preserve this repo as the learning checkpoint.
- Use the fork to replace educational implementations with production-oriented choices.
- Measure changes with the existing eval harness before changing defaults.
- Keep provider-specific behavior isolated behind Infrastructure/Application boundaries.

## Ingestion Hardening

- Move ingestion out of the request path.
  - Current `/ingestions` work runs inline and can exceed client timeouts.
  - Use the worker project, a background queue, or another durable job mechanism.
  - Phase 0 now has `IngestionRun` and `IngestionItem` records, so async ingestion should reuse these entities instead of introducing a separate job model.
- Add bulk indexing for Elasticsearch.
  - Current indexing is chunk-by-chunk.
  - Bulk indexing should reduce ingestion latency and partial-failure noise.
- Add richer ingestion observability.
  - Current spans track ingestion and file-level work.
  - Split document count, chunk count, extraction time, embedding time, indexing time, and failures into clearer stage spans or metrics.
- Add retry and dead-letter handling.
  - Failed files should be recorded and retryable without rerunning the whole source folder.
- Preserve content hashing and idempotency.
  - Existing idempotency behavior is important; keep it when ingestion moves async.

## Search And Retrieval

- Compare application-level RRF with Elasticsearch native RRF.
  - Current RRF is implemented in .NET for learning and provider neutrality.
  - Elasticsearch-native RRF is now an opt-in adapter path for comparison.
  - Keep application-level RRF as the default baseline until evals justify changing it.
- Compare reranking options.
  - Current reranking uses a local chat model and is intentionally educational.
  - Evaluate Elasticsearch native reranking, Azure AI Search semantic ranker, local ONNX cross-encoders, and dedicated reranker models.
- Add a query rewrite toggle and caching.
  - Query rewriting improves understanding but local LLM rewrite can add noticeable latency.
  - Same original query should not always require another rewrite call.
- Keep metadata filters provider-neutral at the API boundary.
  - Elasticsearch query details should stay inside Infrastructure.
- Compare retrieval modes with eval data.
  - vector-only
  - keyword-only
  - hybrid
  - rewrite + hybrid
  - RRF + rerank

## Microsoft.Extensions.AI And Model Providers

- Increase Microsoft.Extensions.AI usage where it removes custom glue code.
  - Keep the existing Application interfaces as the boundary unless MEAI abstractions are a better fit.
- Add model/provider switching.
  - Candidate providers: Ollama, Microsoft Foundry Local, LM Studio, Azure OpenAI, and other local/on-prem providers.
  - Track chat model, embedding model, provider, rewrite model, and rerank model in diagnostics.
- Add Microsoft.Extensions.AI evaluation packages as a second eval layer.
  - Keep deterministic source-hit tests as the default regression signal.
  - Add optional quality evals for groundedness, relevance, completeness, equivalence, and reporting.
- Evaluate structured output support.
  - Current answer and rerank parsing depends on prompt-following JSON.
  - Compare plain prompting with MEAI `ChatOptions.ResponseFormat` and provider-specific structured output support.

## Agent And Tool Orchestration

- Explore Microsoft Agent Framework in the fork.
  - Keep agentic RAG controlled and tool-based, not open-ended model autonomy.
  - Candidate tools: rewrite query, search documents, fetch chunk, summarize evidence, answer from evidence.
- Keep the deterministic RAG path available.
  - Agentic orchestration should be compared against the non-agentic pipeline using the eval harness.
- Log tool calls through OpenTelemetry.
  - Tool call traces should include tool name, duration, result count, and status, not raw sensitive content by default.

## Ask And Answer Quality

- Improve citation behavior.
  - Current claims cite chunk ids.
  - The fork should validate that each claim is actually supported by its cited chunk.
  - Consider inline citation markers after structured claims stabilize.
- Strengthen refusal behavior.
  - Current unsupported answers return `insufficientContext`.
  - Improve unsupported-answer detection as prompt strategy and eval cases grow.
- Consider streaming answers.
  - `/ask` currently waits for the full chat response.
  - Streaming would improve perceived latency.
- Add cost/token logging.
  - Useful when comparing local, Azure OpenAI, Foundry Local, LM Studio, and other providers.

## Chunking Experiments

- Compare chunking strategies with the eval harness.
  - Current text chunking is paragraph-based.
  - Current scanned document chunking is layout-aware and rule-based.
- Keep `ITextChunker` as the Application boundary.
  - Docling, Microsoft chunking packages, token-aware splitting, recursive splitting, and semantic chunking should be Infrastructure adapters.
  - Do not change Domain/API contracts for an experiment unless evals show the strategy should become product behavior.
- Use indexed chunking metadata when comparing experiments.
  - Current indexed metadata includes chunking strategy and max chunk size.
  - Add a persisted `ChunkingProfile` only when selectable/reusable chunking configurations are needed.
- Candidate strategies:
  - token-aware chunking
  - overlapping chunks
  - recursive splitting
  - semantic chunking
  - table-preserving scanned-document chunks
- Optional Docling spike:
  - Run Docling as a separate Python service or offline experiment first.
  - Map results back into ProjectRag chunk models instead of leaking Docling types into Domain or Contracts.
  - Compare retrieval hit rate, citation correctness, answer status, chunk count, and ingestion latency against the paragraph/layout baseline.
- Measure impact before changing defaults.
  - Judge chunking changes by retrieval hit rate, citation correctness, answer groundedness, and latency.

## Enterprise Hardening

- Add authentication and authorization.
- Add tenant/source filters and per-document ACL metadata.
- Add PII redaction options for ingestion and telemetry.
- Add OpenTelemetry metrics after traces stabilize.
- Add retry policies, rate limiting, and circuit breakers.
- Add operational dashboards for latency, error rate, ingestion failures, model calls, token/cost estimates, and retrieval quality.
- Add opt-in Docker-backed integration tests for Elasticsearch and provider integrations.

## References

- Foundry Local documentation: https://learn.microsoft.com/en-us/azure/foundry-local/
- Foundry Local quickstart: https://learn.microsoft.com/en-us/azure/ai-foundry/foundry-local/get-started
- Microsoft Foundry RAG evaluators: https://learn.microsoft.com/en-us/azure/foundry/concepts/evaluation-evaluators/rag-evaluators
- Microsoft.Extensions.AI evaluation libraries: https://learn.microsoft.com/en-us/dotnet/ai/evaluation/libraries
- Aspire Dashboard standalone: https://aspire.dev/dashboard/standalone/
- .NET vector stores: https://learn.microsoft.com/en-us/dotnet/ai/vector-stores/how-to/use-vector-stores
- Elasticsearch RRF retriever: https://www.elastic.co/docs/reference/elasticsearch/rest-apis/retrievers/rrf-retriever
