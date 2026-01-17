# ADR 0006: AdmxCache Culture Purge Extraction

- Status: Accepted
- Date: 2026-01-13

## Context

`PolicyPlusCore/Core/AdmxCache.cs` has been progressively reduced into a facade delegating to internal services (ADR 0001–0005).

`AdmxCache` still contains `PurgeCultureAsync`, which is a write-side method that:

- Acquires the cross-process writer lock (best-effort).
- Opens the cache database and starts a transaction.
- Enumerates per-culture FTS rowids (`PolicyIndexMap`).
- Executes FTS5 delete commands for each rowid.
- Deletes culture rows from `PolicyIndexMap` and `PolicyI18n`.

This method is DB- and transaction-heavy and is invoked from the scan pipeline as part of "culture missing" gating. Keeping it inside the facade makes `AdmxCache.cs` harder to read and increases the risk of subtle drift (transaction boundaries, error swallowing, writer-lock semantics) in follow-on changes.

## Decision

### 1) Keep public API stable

- No changes to `IAdmxCache`.
- No changes to the public entry points of `AdmxCache`.

### 2) Extract culture purge into an internal component

Introduce an internal component under `PolicyPlusCore/Core/Caching/`:

- `AdmxCacheCulturePurgeService`
  - Owns the logic currently in `AdmxCache.PurgeCultureAsync`.
  - Uses `AdmxCacheWriterGate` for cross-process writer serialization.
  - Preserves the existing best-effort behavior (safe no-op on failure) and exception swallowing boundaries.

The facade method remains (private) but becomes a thin delegate.

### 3) Defer unification with apply-pipeline deletions

`AdmxCacheApplyService` also performs culture-localized deletions as part of `DiffAndApplyAsync`.

For this ADR, we only extract `PurgeCultureAsync` as-is. Any future refactor that unifies culture deletion logic across services should be done as a follow-up ADR to avoid accidental semantic changes.

## Rationale

- The method is a natural seam: it has a single purpose (purge culture-localized cache rows).
- It reduces `AdmxCache.cs` size and helps keep it as a facade.
- It aligns with the existing pattern of extracting DB-heavy logic into `Core/Caching/*Service` or `*Store` classes.

## Consequences

### Positive

- `AdmxCache.cs` becomes smaller and easier to maintain.
- The purge logic gains a single internal entry point, making it easier to test and evolve.

### Negative / Risks

- Accidental changes to transaction boundaries or exception swallowing can change behavior.
- Over-eager consolidation with apply-pipeline logic could introduce subtle differences.

### Mitigations

- Lift-and-shift extraction first (no behavior changes intended).
- Keep writer-lock acquisition retry budgets and delays identical.
- Run the full unit test suite after extraction.

## Compatibility

- Public API impact: none.
- Behavior changes: none intended.
- Migration steps:
  1. Add `AdmxCacheCulturePurgeService`.
  2. Make `AdmxCache.PurgeCultureAsync` delegate to the service.
  3. Run tests; add characterization tests only if coverage gaps are discovered.

## Test Plan

See `Docs/Architecture/Refactor/TestPlan.md`.

- New tests (only if needed):
  - Characterization: purge is best-effort (no throw) under typical missing-culture scenarios.
  - Characterization: writer-lock acquisition remains best-effort and does not block indefinitely.
- Updated tests: none required if existing scan characterization tests cover the call site.

Run:

- `dotnet test PolicyPlusModTests/PolicyPlusModTests.csproj -c Debug-Unpackaged -r win-x64`

## Notes

This ADR is intentionally scoped to extracting the method as-is. It does not change the schema or the meaning of the purge.
