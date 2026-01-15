# ADR 0004: AdmxCache Write/Apply Pipeline as Internal Services

- Status: Accepted
- Date: 2026-01-13

## Context

ADR 0001 through ADR 0003 established a safe incremental refactor pattern for `AdmxCache`:

- Keep `IAdmxCache` and `AdmxCache` public surface stable.
- Move high-branching logic behind internal services.
- Use characterization tests to preserve behavior.

After ADR 0003, most scan decision logic (culture planning, culture gating, signature skip decision, opportunistic maintenance, bundle loading and file-usage refresh, and global rebuild meta update) has been extracted under `PolicyPlusCore/Core/Caching/*`.

However, `PolicyPlusCore/Core/AdmxCache.cs` still contains a large amount of write-side indexing and persistence logic, notably:

- `DiffAndApplyAsync(...)` (delete + insert/update pipeline)
- `UpsertOnePolicyAsync(...)` (including prepared-command variants)
- Meta helpers (`GetMetaAsync`, `SetMetaAsync`, `NeedsGlobalRebuildAsync`)
- File usage helpers (schema ensure, open connection, upsert, stale purge)

This makes `AdmxCache.cs` remain large and hard to reason about, and it makes further refactors riskier because write-side behavior is strongly coupled to the facade.

## Decision

### 1) Keep public API stable

- No changes to `IAdmxCache`.
- No changes to the public behavior or signatures of `AdmxCache.InitializeAsync`, `SetSourceRoot`, `ScanAndUpdateAsync`, and Search-related overloads.

`AdmxCache` remains a facade and delegates to internal services.

### 2) Extract the write/apply pipeline behind internal services

Introduce internal components under `PolicyPlusCore/Core/Caching/` to own the write-side pipeline:

- `AdmxCacheApplyService`
  - Owns the logic currently implemented in `DiffAndApplyAsync(...)`.
  - Maintains batching behavior, transaction boundaries, and cancellation/yield patterns.

- `AdmxCachePolicyUpsertService`
  - Owns the SQL command preparation and upsert logic currently implemented in `UpsertOnePolicyAsync(...)` and its prepared variant.

The facade will call these services with the minimal required dependencies (store connection, runtime flags, and delegates where needed to preserve current semantics).

### 3) Extract SQLite meta access behind a store abstraction

Introduce an internal `AdmxCacheMetaStore` (or similar) under `PolicyPlusCore/Core/Caching/`:

- Owns `GetMetaAsync`, `SetMetaAsync`.
- Owns `NeedsGlobalRebuildAsync` (or an equivalent operation) to keep meta reads/writes in one place.

### 4) Extract file-usage DB operations behind a store abstraction

Introduce an internal `AdmxCacheFileUsageStore` (or similar) under `PolicyPlusCore/Core/Caching/`:

- Owns schema ensure state and schema creation.
- Owns opening a connection and upserting file usage.
- Owns stale purge operations.

Note: ADR 0003 already extracted bundle loading + file-usage refresh orchestration, but the underlying DB operations still live on the facade. This ADR extracts those remaining DB operations.

### 5) Behavior preservation contract

This ADR is a structure-only refactor. It does not intentionally change:

- What gets indexed for the same input ADMX/ADML tree.
- Delete/insert/update ordering and transaction boundaries in the write-side pipeline.
- Writer-lock behavior (including fast-mode behavior).
- Batching behavior, yield points, or any limits.
- Exception swallowing / partial failure tolerance.
- Any environment-variable-controlled behavior (including but not limited to `POLICYPLUS_CACHE_FAST`, `POLICYPLUS_CACHE_ONLY_FILES`, `POLICYPLUS_CACHE_MAX_POLICIES`).

If a semantic change is needed, capture it in a separate ADR with explicit tests.

## Rationale

- The remaining write-side pipeline is the largest contributor to `AdmxCache.cs` size.
- Write-side logic is highly stateful (SQLite, transactions, batching), and extracting it behind a service boundary improves maintainability.
- The facade/service split makes it possible to add targeted tests for write-side invariants without exposing new public APIs.

## Consequences

### Positive

- `AdmxCache.cs` becomes a true facade: smaller, easier to read, and easier to refactor further.
- Write-side invariants become local to dedicated internal services.
- Follow-up refactors can proceed with less risk and smaller diffs.

### Negative / Risks

- Risk of subtle behavior drift when moving try/catch boundaries (exception swallowing is part of observable behavior).
- Risk of changing writer-lock behavior or cancellation behavior.
- Risk of changing prepared command lifetimes or parameter binding order.

### Mitigations

- Use a lift-and-shift approach: move code without rewriting, then refactor later.
- Keep SQL strings, parameter binding order, and transaction boundaries identical.
- Add/extend characterization tests for key invariants before extracting deeper.

## Compatibility

- Public API impact: none.
- Behavior changes: none intended.
- Migration steps:
  1. Introduce `AdmxCacheMetaStore` and route meta reads/writes through it.
  2. Introduce `AdmxCachePolicyUpsertService` and move the upsert command builders/logic into it.
  3. Introduce `AdmxCacheApplyService` and move `DiffAndApplyAsync(...)` into it.
  4. Introduce `AdmxCacheFileUsageStore` and move file-usage schema/open/upsert/purge into it.
  5. Re-run existing tests after each step.

## Test Plan

This ADR builds on the behavioral safety contract in `Docs/Architecture/Refactor/TestPlan.md`.

- Primary coverage: existing characterization tests for scan behavior and partial failure tolerance.
  - `PolicyPlusModTests/Core/AdmxCacheScanCharacterizationTests.cs`

- Existing coverage for search behavior is expected to remain unchanged.

- New tests (minimal, only if needed to protect extracted behavior):
  - Characterization: global rebuild path remains consistent after a forced root/meta change.
  - Characterization: blank localized display names are not persisted (protects the "no fallback persistence" contract).

Run:

- `dotnet test PolicyPlusModTests/PolicyPlusModTests.csproj -c Debug-Unpackaged -r win-x64`

## Notes

This ADR intentionally focuses on the write/apply pipeline and DB access abstractions. It does not attempt to reorganize the full `PolicyPlusCore` folder hierarchy.

In DDD terms, these extracted components remain infrastructure-equivalent code (SQLite + filesystem + caching persistence) and therefore live under `PolicyPlusCore/Core/Caching/*` for now.
