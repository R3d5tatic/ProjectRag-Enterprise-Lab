# Phase 0 Task 02: Rename IngestionJob To IngestionRun

## Goal

Rename the current ingestion status concept from `IngestionJob` to `IngestionRun` so the domain language matches durable workflow history.

This is a naming/baseline task only. It should not add async ingestion behavior.

## Rename Scope

- `IngestionJob` -> `IngestionRun`
- `IngestionJobStatus` -> `IngestionRunStatus`
- `IngestionJobs` DbSet/table -> `IngestionRuns`
- `IngestionJobConfiguration` -> `IngestionRunConfiguration`
- `IngestionJobResponse` -> `IngestionRunResponse`

Keep `StartIngestionRequest` unchanged.

## Code Shape

`IngestionRun` should keep the current fields:

- `Id`
- `SourcePath`
- `Status`
- `ErrorMessage`
- `CreatedAt`
- `StartedAt`
- `CompletedAt`

Status values should remain:

- `Pending`
- `Running`
- `Completed`
- `Failed`

## Boundary Rules

- Domain stays provider-neutral.
- Do not add `IngestionItem` yet.
- Do not add queues, background worker behavior, retries, or dead-letter handling.
- Do not change endpoint routes.
- Keep response JSON shape compatible except for C# contract type name.

## API Behavior

The existing endpoints stay the same:

- `POST /api/v1/ingestions`
- `GET /api/v1/ingestions/{id}`

The response field can remain:

- `ingestionId`

Do not rename it to `runId` yet unless a public API versioning decision is made later.

## Migration Notes

Add a migration that renames the table from `IngestionJobs` to `IngestionRuns`.

Prefer an EF rename operation over drop/create so existing local data is preserved.

The migration should not change behavior or add new columns.

## Tests

Update existing API tests to use `IngestionRunResponse`.

Existing assertions should remain valid:

- `POST /ingestions` returns `202 Accepted`.
- returned status becomes `Completed` for current inline ingestion.
- `GET /ingestions/{id}` returns the created run.
- existing search/ask/eval tests still ingest successfully.

## Acceptance Criteria

- `dotnet build ProjectRag.slnx` passes.
- `dotnet test ProjectRag.Tests/ProjectRag.Tests.csproj` passes.
- No references to `IngestionJob` remain outside old migrations unless intentionally preserved by EF history.
- No async behavior added.
- No public route changes.

## Out Of Scope

- `IngestionItem`
- background worker processing
- queueing
- retry/dead-letter behavior
- new ingestion endpoints
- provider switching
- Azure deployment
