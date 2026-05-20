# Phase 0 Domain Review

## Purpose

Define the smallest production-minded domain baseline for ProjectRag-Enterprise-Lab before adding provider switching, async ingestion, Microsoft.Extensions.AI refactors, agents, Azure deployment, or CI/CD.

This is a review artifact, not an implementation plan.

## Current Domain Model

- `KnowledgeBase`: logical product boundary that groups data sources, documents, and ingestion runs.
- `DataSource`: provider-neutral provenance record for local source paths and future source types.
- `Document`: current persisted source document record.
- `DocumentChunk`: current persisted chunk record with text, layout metadata, and chunk kind.
- `IngestionRun`: current ingestion request/run record.
- `IngestionItem`: per-file ingestion record with status, error details, and optional document link.
- `IngestionRunStatus`: current ingestion state enum.
- `ChunkKind`: current chunk classification enum.

## Current Boundaries

- Domain owns provider-neutral entities and enums.
- Application owns provider-neutral ports and RAG models.
- Contracts owns HTTP request/response DTOs.
- Infrastructure owns EF Core, SQLite, Elasticsearch, Ollama, Azure Document Intelligence, chunking, retrieval, reranking, and answer generation implementations.
- API owns HTTP endpoints and composition root behavior.

## Concept Triage

| Concept | Decision | Reason |
|---|---|---|
| KnowledgeBase | Implemented | Production anchor for grouping documents, data sources, and ingestion history. |
| Workspace | Defer | May duplicate `KnowledgeBase` unless a broader product/customer boundary becomes necessary. |
| DataSource | Implemented | Provider-neutral provenance for current local ingestion and future source types. |
| SourceDocument | Evaluate | May clarify the current `Document` role if future documents belong to data sources or knowledge bases. |
| IngestionRun | Implemented | Production name for durable ingestion history. |
| IngestionItem | Implemented | Tracks per-file status, partial failures, and document linkage. |
| ChunkingProfile | Defer | Useful when chunking experiments become selectable and measurable. |
| EmbeddingProfile | Defer | Useful when provider switching and model comparison begin. |
| ModelProfile | Defer | Useful when chat/rewrite/rerank/eval providers become configurable. |
| RetrievalProfile | Defer | Useful when retrieval strategies become selectable. |
| RetrievalRun | Defer | Useful for diagnostics and eval reporting, but not needed for the first slice. |
| AnswerRun | Defer | Useful for audit/history later, but not needed before retrieval and ingestion are stable. |
| EvalRun | Defer | Useful when eval reporting grows beyond the current test harness. |
| AgentRun | Later | Agentic RAG is explicitly later and must not replace deterministic RAG. |
| Tenant / ACL | Later | Enterprise hardening concern, not the first Phase 0 slice. |
| Provider/runtime profile concepts | Defer | Belongs after the core product model is stable. |
| Search strategy concepts | Defer | Belongs with retrieval experiments, especially Elasticsearch-native features. |
| Observability/diagnostic run metadata | Defer | Important, but should follow stable run boundaries. |

## Implemented Baseline

- `KnowledgeBase`
- `DataSource`
- `IngestionRun`
- `IngestionItem`

Current relationships:

- `KnowledgeBase` owns `DataSource`, `Document`, and `IngestionRun` records.
- `DataSource` provides provenance for `Document` and `IngestionRun` records.
- `IngestionRun` owns `IngestionItem` records.
- `IngestionItem` optionally links to the `Document` it produced, reused, or reindexed.
- Failed `IngestionItem` records may have no `DocumentId`.

## Defer-Later Defaults

- Provider profiles should wait until provider switching is the active phase.
- Retrieval profiles should wait until retrieval experiments are the active phase.
- Eval and answer run history should wait until reporting/audit needs are clearer.
- Agent concepts should wait until deterministic RAG remains stable and measurable.
- Tenant and ACL concepts should wait until enterprise hardening is explicitly in scope.

## Decisions

- Keep `Document` as the entity name for now.
  - Reason: the current name is simple, established across the repo, and still provider-neutral.
  - Defer `SourceDocument` unless `Document` becomes ambiguous after `KnowledgeBase` and `DataSource` exist.

- Introduce `KnowledgeBase` and `DataSource` together when code changes begin.
  - Reason: `KnowledgeBase` is the grouping boundary, while `DataSource` explains where documents came from.
  - Avoid adding `Workspace` unless a broader product/customer boundary is needed later.

- Use `IngestionRun` for the production model.
  - Reason: `Run` better represents durable workflow history and future observability.
  - This was implemented as an evolution of the existing concept, not a broad rewrite.

- Use `IngestionItem` for per-file ingestion visibility.
  - Reason: it gives production-grade status and error tracking without requiring a queue architecture yet.
  - Async ingestion should reuse `IngestionRun` and `IngestionItem`, not introduce a separate job model.
