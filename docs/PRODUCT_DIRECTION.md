# Product Direction

ProjectRag is the learning checkpoint. It demonstrates a phased .NET RAG system from a minimal API foundation through hybrid retrieval, query rewriting, Reciprocal Rank Fusion, reranking, grounded answers, evaluation, and OpenTelemetry observability.

The next project or fork should evolve that learning baseline into a production-minded RegainFlow reference architecture for building provider-swappable, observable, agentic RAG systems in .NET.

This document defines the product direction for that future work. It should guide Codex, coding agents, and human contributors before implementation decisions are made.

## North Star

Build a portfolio-grade RAG backend that shows what RegainFlow can deliver for organizations that need practical AI systems, not demos.

The system should demonstrate:

- clean .NET 10 backend architecture
- ports-and-adapters design
- provider-swappable AI integrations
- production-minded ingestion workflows
- measurable retrieval quality
- grounded answers with citations
- observability across the full RAG pipeline
- cloud deployment readiness
- controlled agentic RAG patterns

The goal is not to chase every new AI package or framework. The goal is to evaluate useful tools, integrate them behind clean boundaries, and prove their value with tests, diagnostics, and evals.

## Positioning

ProjectRag-Enterprise-Lab should be positioned as:

> A provider-swappable, observable, production-minded RAG reference architecture for .NET teams.

It should show how to combine:

- .NET 10
- Microsoft.Extensions.AI
- Microsoft.Extensions.VectorData and vector store abstractions where appropriate
- Microsoft AI data ingestion, chunking, and evaluation packages where appropriate
- Microsoft Agent Framework for future agentic RAG workflows
- Elasticsearch for hybrid retrieval, native RRF, semantic search, ELSER, reranking, and diagnostics
- Ollama, LM Studio, Foundry Local, Azure OpenAI, and future Azure-hosted or GPU-backed model providers
- Azure deployment patterns
- GitHub Actions and/or Azure DevOps CI/CD
- containerized services
- Aspire Dashboard and OpenTelemetry
- optional Phoenix-style AI observability and eval workflows if needed later

## Non-Goals

The production fork should avoid becoming a messy feature dump.

Do not:

- tightly couple Domain or API contracts to a specific provider
- let Elasticsearch query DSL leak into API contracts
- let Azure-specific deployment concerns leak into Domain
- let Microsoft Agent Framework replace the deterministic RAG path
- add agentic behavior before retrieval quality, tracing, and evals are measurable
- add a UI before the backend product model is stable
- optimize for novelty over clarity, replaceability, and correctness

## Architecture Principles

### 1. Keep the core provider-neutral

Domain and Application should describe product concepts, not vendor concepts.

Prefer concepts like:

- KnowledgeBase
- DataSource
- IngestionRun
- IngestionItem
- ChunkingProfile
- EmbeddingProfile
- ModelProfile
- RetrievalProfile
- RetrievalRun
- AnswerRun
- EvalRun
- AgentRun

Avoid provider-specific concepts in Domain unless there is a deliberate reason.

Provider-specific code belongs in Infrastructure adapters.

### 2. Use Microsoft.Extensions.AI intentionally

Microsoft.Extensions.AI should reduce custom glue and improve provider portability.

Use it where it strengthens the boundary, such as:

- chat model abstraction
- embedding generation
- OpenTelemetry integration
- structured outputs where supported
- evaluation workflows
- vector store abstractions where they fit the product needs
- data ingestion and chunking packages where they improve maintainability or measurement

Do not blindly replace every Application port with a Microsoft interface. Some app-specific concerns need first-class ports because ProjectRag cares about citations, source metadata, chunk provenance, retrieval diagnostics, eval results, ingestion status, and provider comparison.

### 3. Treat Elasticsearch as a powerful adapter, not the app architecture

Elasticsearch is a key part of the product story, especially for:

- BM25 keyword search
- dense vector search
- hybrid retrieval
- native Reciprocal Rank Fusion
- semantic retrieval
- ELSER sparse vector retrieval
- reranking
- retrieval diagnostics

However, Elasticsearch-specific query construction and response parsing should stay inside Infrastructure.

Application should work with retrieval strategies, search requests, filters, diagnostics, and ranked results.

### 4. Preserve deterministic RAG

Agentic RAG should be added later as an additional path, not as a replacement for the deterministic pipeline.

The system should support comparing:

- deterministic RAG
- deterministic RAG with improved retrieval strategies
- agentic RAG with tool orchestration

Agentic workflows should use explicit tools, traceable steps, and evals.

### 5. Make experiments measurable

Every major change should be measurable against the baseline.

Examples:

- chunking strategy changes should measure retrieval hit rate, citation correctness, groundedness, latency, chunk count, and indexing time
- retrieval strategy changes should compare vector-only, keyword-only, hybrid, native RRF, rerank, and semantic modes
- model provider changes should compare latency, reliability, structured output quality, local/cloud cost, and operational complexity
- agentic changes should compare answer quality, tool-call accuracy, latency, and failure modes against the deterministic path

## Product Capabilities

### Knowledge Management

The product should eventually support logical knowledge bases or workspaces.

A knowledge base should group documents, data sources, ingestion history, retrieval settings, and future access policies.

This enables portfolio scenarios such as:

- legal document assistant
- HR policy assistant
- technical documentation assistant
- customer support knowledge base
- enterprise search prototype
- RegainFlow client demo environment

### Data Sources

The system should evolve beyond local folder ingestion.

Potential sources:

- local files
- uploaded files
- Azure Blob Storage
- SharePoint or OneDrive
- GitHub repositories
- websites
- database exports
- ticketing systems
- CRM exports

Phase 0 does not need to implement all of these. It should define a provider-neutral model that can support them later.

### Ingestion

Ingestion should evolve from inline HTTP execution into a background workflow.

Desired behavior:

- `POST /api/v1/ingestions` returns `202 Accepted`
- the API records an ingestion run
- a worker processes files asynchronously
- each file has its own ingestion item status
- failed files are retryable
- partial failures are visible
- dead-letter handling is possible later
- content hashing and idempotency are preserved
- indexing supports bulk operations where possible

A full event-driven architecture is not required at first. Start with a durable job model and background worker. Move to queues, service bus, or event-driven stages when scaling, fanout, replayability, or independent service ownership justifies it.

### Chunking

Chunking should become an explicit, measurable part of the product.

Candidate strategies:

- paragraph-based chunking
- token-aware chunking
- overlapping chunks
- recursive splitting
- semantic chunking
- layout-aware scanned document chunking
- table-preserving chunking

Chunking strategy should be tracked so eval results and retrieval behavior can be compared over time.

Current implementation direction:

- keep paragraph/layout chunking as the baseline
- keep `ITextChunker` as the provider-neutral Application port
- keep chunker implementations in Infrastructure
- index chunking strategy metadata for comparison
- defer a persisted `ChunkingProfile` until strategies become selectable product configuration
- evaluate Docling and Microsoft chunking packages as adapters before changing defaults

### Embeddings And Model Providers

The system should support provider switching across local and cloud model providers.

Candidate providers:

- Ollama
- LM Studio
- Foundry Local
- Azure OpenAI
- Azure AI Foundry-hosted models
- future Azure GPU-backed endpoints
- other OpenAI-compatible endpoints

Model usage should be recorded in diagnostics and run metadata.

The product should distinguish model purposes:

- chat
- embeddings
- query rewriting
- reranking
- answer generation
- evaluation
- agent planning or tool selection

### Retrieval

Retrieval should support multiple strategies behind a stable Application boundary.

Candidate modes:

- keyword-only retrieval
- vector-only retrieval
- hybrid retrieval
- query rewrite plus hybrid retrieval
- application-level RRF
- Elasticsearch native RRF
- Elasticsearch semantic retrieval
- Elasticsearch ELSER retrieval
- Elasticsearch or model-based reranking

The API should expose meaningful diagnostics without exposing provider internals.

### Answer Generation

Answers should remain grounded and auditable.

The system should continue supporting:

- answer status
- insufficient-context refusal behavior
- structured claims
- citations
- retrieval diagnostics
- model diagnostics

Future improvements may include:

- citation validation
- inline citation markers
- streaming answers
- prompt versioning
- structured output comparisons
- token and cost telemetry

### Evaluation

Evaluation should remain part of the product, not an afterthought.

The current deterministic eval harness should remain the baseline regression signal.

Future evaluation layers may include:

- Microsoft.Extensions.AI evaluation packages
- groundedness checks
- relevance checks
- completeness checks
- equivalence checks
- retrieval quality checks
- citation correctness checks
- tool-call accuracy checks for agents
- experiment reports across providers and retrieval strategies

### Observability

OpenTelemetry should remain the default observability foundation.

The system should trace:

- ingestion runs
- extraction
- chunking
- embedding generation
- indexing
- query rewriting
- vector search
- keyword search
- fusion
- reranking
- answer generation
- eval execution
- agent tool calls later

Aspire Dashboard should be the local .NET-friendly observability surface.

Phoenix or similar AI observability tools can be added later if deeper prompt, retrieval, eval, or LLM experiment workflows are needed.

Sensitive content capture should remain disabled by default.

### Agentic RAG

Agentic RAG is a future capability, not the first step.

When added, it should be controlled and tool-based.

Candidate tools:

- rewrite query
- search documents
- fetch chunk
- summarize evidence
- validate citations
- generate grounded answer
- run eval check

Agent runs should be traceable, measurable, and comparable against deterministic RAG.

Microsoft Agent Framework should be evaluated as the orchestration layer, but it should not leak into Domain or API contracts unnecessarily.

### Azure And Deployment

The production fork should eventually demonstrate cloud readiness.

Target deployment topics:

- containerized API
- containerized worker
- Azure Container Registry
- Azure Container Apps, App Service, or AKS depending on complexity
- Azure-hosted database
- Elasticsearch connection strategy
- Azure OpenAI or Azure AI Foundry model provider
- managed identity where appropriate
- environment-based configuration
- secrets management
- database migrations
- deployment environments
- CI/CD using GitHub Actions and/or Azure DevOps

The first cloud goal should be a working backend deployment, not a full enterprise platform.

### UI

A UI is intentionally out of scope for the early production fork.

The backend should expose stable contracts that make a future UI easy:

- list knowledge bases
- list documents
- create ingestion run
- check ingestion status
- search
- ask
- inspect eval results
- inspect retrieval diagnostics

A UI can be added later as a thin admin/demo surface.

## Suggested Phase Roadmap

### Phase 0 — Product Domain Model Review

Evaluate the current domain model and decide what is missing for a production-minded RAG platform.

Deliverables:

- current domain model summary
- proposed production domain model
- implement-now vs defer-later decision
- provider-neutral naming and boundaries
- smallest implementation slice
- tests and verification plan

### Phase 1 — Production Domain Baseline

Implement the selected Phase 0 model slice.

Likely candidates:

- KnowledgeBase
- DataSource
- IngestionRun
- IngestionItem
- ChunkingProfile
- EmbeddingProfile
- ModelProfile
- RetrievalProfile

Keep the slice small and reviewable.

### Phase 2 — Microsoft.Extensions.AI Alignment

Evaluate where Microsoft.Extensions.AI, VectorData, data ingestion, chunking, and evaluation packages should replace custom glue or supplement existing abstractions.

Current direction:

- Keep RAG-specific Application ports custom.
- Keep MEAI chat and embedding abstractions in Infrastructure.
- Keep Elasticsearch as the primary retrieval adapter.
- Evaluate VectorData before using it for production retrieval.
- Defer MEAI evaluation and ingestion/chunking packages until deterministic behavior is stable.
- Keep sensitive AI telemetry disabled by default.
- Keep MEAI `ChatOptions` and provider-native structured output settings behind Infrastructure adapters unless evals prove a better boundary.

Do not break existing behavior without eval evidence.

### Phase 3 — Provider Switching

Add explicit provider profiles and support local provider comparison.

Compare:

- Ollama
- LM Studio
- Foundry Local
- Azure OpenAI when ready

### Phase 4 — Chunking And Ingestion Experiments

Make chunking strategy selectable and measurable.

Compare strategies with eval data.

### Phase 5 — Elasticsearch Native Retrieval Experiments

Compare existing retrieval with Elasticsearch-native capabilities.

Evaluate:

- native RRF
- semantic search
- ELSER
- reranking

Current implementation direction:

- keep application-level RRF as the default baseline
- expose Elasticsearch-native RRF as an opt-in Infrastructure adapter
- keep retrieval strategy selection config-backed
- keep Domain, Application ports, and API contracts provider-neutral
- defer ELSER, semantic retrieval, and provider-native reranking until native RRF comparison is stable

### Phase 6 — Async Ingestion

Move ingestion out of the request path.

Implement:

- `202 Accepted`
- background worker processing
- per-item status
- retries
- failed item visibility
- optional dead-letter design

### Phase 7 — Observability And Eval Expansion

Improve traces, metrics, and eval reporting.

Add Microsoft evaluation packages and optional Phoenix experiments if useful.

### Phase 8 — Azure Deployment

Containerize and deploy the backend to Azure.

Add CI/CD through GitHub Actions and/or Azure DevOps.

### Phase 9 — Agentic RAG

Add controlled Microsoft Agent Framework-based workflows.

Keep deterministic RAG available and compare both paths.

### Phase 10 — Optional UI

Add a lightweight admin/demo UI only after backend contracts stabilize.

## Codex Working Agreement

Codex and other coding agents should act as mentors and implementation assistants.

The human developer is the driver.

Before coding, Codex should explain:

1. what it found
2. what decision is being made
3. what files are likely affected
4. what belongs in Domain, Application, Infrastructure, and Contracts
5. what should stay provider-neutral
6. what tests are needed
7. what verification commands should be run

Prefer small, reviewable PRs.

Do not implement broad architectural rewrites without first proposing the plan.

Do not add provider-specific coupling to Domain or Contracts unless explicitly approved.

## Success Criteria

This project succeeds if it can show:

- a clean .NET architecture for practical AI systems
- provider-swappable model and vector store integrations
- strong Elasticsearch retrieval engineering
- measurable RAG quality improvements
- observable ingestion, retrieval, and answer-generation pipelines
- production-minded background processing
- credible Azure deployment and CI/CD patterns
- controlled agentic RAG rather than uncontrolled autonomy
- a clear portfolio story for RegainFlow

The final outcome should feel like a reference implementation that a client or engineering leader could review and say:

> This team understands how to turn AI prototypes into production-ready systems.
