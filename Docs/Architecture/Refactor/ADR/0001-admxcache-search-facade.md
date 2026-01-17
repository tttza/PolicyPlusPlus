# ADR 0001: AdmxCache Search as Facade + Tie-tolerant Ranking Contract

- Status: Accepted
- Date: 2026-01-11

## Context

`PolicyPlusCore/Core/AdmxCache.cs` currently contains a large amount of branching logic (query interpretation, multi-culture fallback, short-query heuristics, phrase mode, score aggregation, and filtering).

This makes it harder to:
- Add or modify search behavior safely
- Test and reason about which decisions are applied
- Refactor internals without regressions

We also want to move toward clearer domain modeling (DDD) using Entities / Value Objects where it improves correctness.

## Decision

### 1) Keep the public API stable
- Keep `IAdmxCache` and the current `AdmxCache` public surface stable.
- Refactor internals incrementally using a façade + delegation approach.

### 2) Split Search implementation behind a façade
Introduce internal components under `PolicyPlusCore/Core/Caching/` (names are illustrative; adjust as needed):
- `AdmxCacheSearchService` — owns Search query interpretation + SQL/query construction + score computation
- (later) `AdmxCacheScanService`, `AdmxCacheMaintenanceService`, `AdmxCacheFileUsageStore`

`AdmxCache` remains the entry point and delegates to the internal service(s).

### 3) Ranking tests must be tie-tolerant
We explicitly do NOT guarantee a stable ordering for ties (equal or near-equal `PolicyHit.Score`).

Test contract for ranking:
- Prefer assertions on set membership within Top-K.
- Assert ordering only when score separation is meaningful (e.g., `A.Score - B.Score >= Δ`).
- Prefer score bands/ranges over exact score values.

This is recorded in `Docs/Architecture/Refactor/TestPlan.md`.

### 4) Introduce Value Objects internally first
We will introduce Value Objects internally (not at public boundaries) where they reduce scattered branching:
- Search query normalization/tokenization (e.g., `SearchQuery`)
- Culture normalization / preference handling (e.g., `CultureName`)
- Registry path normalization/tokenization (e.g., `RegistryPathKey`)

These will be adapted at the façade boundary so external call sites remain unchanged.

## Rationale

- Keeping `IAdmxCache` stable minimizes risk to UI and other callers.
- Moving branching decisions into a dedicated service reduces duplication and makes search behavior easier to test.
- Tie-tolerant ranking avoids over-specifying behavior that should be allowed to evolve (e.g., improvements to scoring or SQL plans).
- Introducing Value Objects internally allows better invariants and unit tests without forcing a breaking change.

## Consequences

### Positive
- Search logic becomes easier to read, test, and evolve.
- Refactor steps can be small and reversible.
- Ranking tests become robust against incidental tie ordering.

### Negative / Risks
- Initial refactor may temporarily increase indirection.
- Without careful boundaries, Value Objects can proliferate and add complexity.

### Mitigations
- Start with Search only; keep other responsibilities in `AdmxCache` until Search is stable.
- Add/extend characterization tests before moving code.
- Keep Value Objects small, immutable, and internal until proven.

## Compatibility

- Public API impact: none intended.
- Behavior changes: none intended; refactor is behavior-preserving.
- Migration steps:
  1. Add/adjust tests per TestPlan.
  2. Introduce `AdmxCacheSearchService`.
  3. Delegate from `AdmxCache.SearchAsync(...)` to the service.
  4. Keep existing integration tests passing.

## Test Plan

- Update/confirm `Docs/Architecture/Refactor/TestPlan.md` includes tie-tolerant ranking guidance.
- Ensure AdmxCache tests run under the isolated collection (`AdmxCache.Isolated`) where applicable.
- Run:
  - `dotnet test PolicyPlusModTests/PolicyPlusModTests.csproj -c Debug-Unpackaged -r win-x64`

## Notes

This ADR focuses on Search because it is a high-branching, high-change-rate area with a clear boundary (`IAdmxCache`). Subsequent ADRs may cover scan/indexing and maintenance responsibilities.
