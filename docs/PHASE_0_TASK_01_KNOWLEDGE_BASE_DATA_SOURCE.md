# Phase 0 Task 01: KnowledgeBase + DataSource Baseline

## Goal

Introduce the smallest provider-neutral production domain baseline for grouping documents and describing where they come from.

This task should not change ingestion behavior yet.

## Implement Now

- Add `KnowledgeBase`.
- Add `DataSource`.
- Link `Document` to `KnowledgeBase`.
- Link `Document` to optional `DataSource`.
- Keep existing ingestion, search, ask, and eval behavior working.

## Suggested Domain Shape

`KnowledgeBase` should represent a logical collection of documents.

Minimum fields:

- `Id`
- `Name`
- `Description`
- `CreatedAt`
- `UpdatedAt`

`DataSource` should represent a configured source of documents.

Minimum fields:

- `Id`
- `KnowledgeBaseId`
- `Name`
- `SourceType`
- `SourceUri`
- `CreatedAt`
- `UpdatedAt`

`Document` should remain the current document entity name.

Add:

- `KnowledgeBaseId`
- optional `DataSourceId`

## Boundary Rules

- Domain stays provider-neutral.
- Do not add Ollama, Azure, Elasticsearch, Microsoft.Extensions.AI, VectorData, or Agent Framework concepts to Domain.
- Do not add tenant, ACL, retrieval profile, model profile, or agent concepts in this task.
- Infrastructure owns EF mappings and migrations.
- API contracts should not expose these new concepts yet unless needed to keep existing tests passing.

## Behavior

- Existing local folder ingestion can attach documents to a default knowledge base.
- Existing endpoint behavior should remain compatible.
- No async ingestion yet.
- No provider switching yet.
- No new retrieval behavior yet.

## Tests

Add focused tests that prove:

- A default knowledge base is created or resolved during ingestion.
- Ingested documents are linked to a knowledge base.
- Existing `/ingestions`, `/documents`, `/search`, and `/ask` tests still pass.

Do not add tests for provider switching, agents, Azure deployment, or retrieval profiles.

## Acceptance Criteria

- `dotnet build ProjectRag.slnx` passes.
- `dotnet test ProjectRag.Tests/ProjectRag.Tests.csproj` passes.
- Existing API contracts remain backward-compatible.
- Domain entities remain provider-neutral.
- No broad rename from `Document` to `SourceDocument`.

## Out Of Scope

- New public endpoints for knowledge bases or data sources.
- UI.
- Async ingestion worker behavior.
- Queueing.
- Tenant/ACL modeling.
- Provider/runtime profiles.
- Microsoft.Extensions.VectorData.
- Microsoft Agent Framework.
- Elasticsearch-native retrieval changes.
