# ADR 0008: AdmxCache read-side query extraction

- Status: Accepted
- Date: 2026-01-13

## Context

`PolicyPlusCore/Core/AdmxCache.cs` has been progressively slimmed by extracting scan, write/apply, writer gating, culture purge, and source signature responsibilities into internal services/stores.

However, `AdmxCache.cs` still contains a meaningful “read-side” surface that mixes:

- SQL construction/execution (including parameter binding and mapping)
- Result mapping to detail models
- Multiple query entry points (e.g., by policy name, by registry path, by category)

Keeping these read-side responsibilities in the facade file increases:

- Local complexity (read + write + scan glue in one file)
- Drift risk when future refactors touch SQL or mapping
- Difficulty in testing/characterizing read-side behavior independently

This ADR proposes extracting the remaining read-side query logic behind the `AdmxCache` facade, continuing the established “strangler” approach.

## Decision

Introduce an internal read-side component under `PolicyPlusCore/Core/Caching/` and move the read-side SQL + mapping logic out of `AdmxCache.cs`.

Proposed shape (exact naming can be adjusted during implementation, but responsibilities must remain the same):

- New internal service:
  - `AdmxCachePolicyQueryService` (or similar)
  - Owns the read-side query entry points currently implemented in `AdmxCache.cs`

- Optional supporting internal mapper:
  - `AdmxCachePolicyDetailMapper` (or keep as a private helper within the service)
  - Owns the `MapDetail`-equivalent logic

Target methods to lift-and-shift out of `AdmxCache.cs` (representative, not exhaustive):

- `GetByPolicyNameAsync(...)`
- `GetByRegistryPathAsync(...)`
- `GetPolicyUniqueIdsByCategoriesAsync(...)`
- The associated row-to-model mapping helper(s) (e.g., `MapDetail`)

What stays in `AdmxCache.cs`:

- Public facade methods and public API surface
- Wiring/orchestration: open DB connection, dependency construction/injection, and delegation to internal services
- Search-specific heuristics/ordering helpers are explicitly *out of scope* for this ADR (planned separately as ADR 0009)

## Rationale

- Keeps `AdmxCache` as a stable facade while reducing file size and responsibility breadth.
- Makes read-side behavior easier to reason about and modify without touching scan/write paths.
- Creates a clearer seam for targeted unit/characterization tests around query behavior.

Alternatives considered:

- Keep read-side in `AdmxCache.cs`: simplest short-term, but preserves long-term maintenance risk.
- Create a generic repository layer for all SQL: likely over-abstracting and harder to keep behavior-preserving.

## Consequences

### Positive

- `AdmxCache.cs` becomes thinner and more focused on orchestration.
- Read-side query logic can evolve independently (still behind the facade).
- Easier to add focused tests for read-side mapping/filters.

### Negative / Risks

- Behavior drift risk when moving SQL/mapping code (null handling, parameter values, normalization, ordering).
- Accidental coupling if the new service starts depending on scan/write concerns.

### Mitigations

- Implement as a lift-and-shift extraction first (structure-only).
- Keep method signatures and parameter semantics identical at the facade boundary.
- Add/extend characterization tests in `PolicyPlusModTests` if coverage gaps are found.

## Compatibility

- Public API impact: None (facade signatures remain stable)
- Behavior changes: None intended (structure-only refactor)
- Migration steps:
  - Introduce internal service
  - Move SQL + mapping implementation
  - Keep `AdmxCache` methods as delegates

## Test Plan

References:

- `Docs/Architecture/Refactor/TestPlan.md` — “AdmxCache” section in the Test Matrix (scan/search invariants; ensure no regressions as we restructure).

Expected test coverage approach:

- New tests: Only if needed to characterize read-side behavior that is not already covered.
- Updated tests: None intended, unless existing tests pin implementation details.
- Manual checks: None.

Minimum validation:

- `dotnet test PolicyPlusModTests/PolicyPlusModTests.csproj -c Debug-Unpackaged -r win-x64`

## Implementation

- Introduced internal read-side service: `PolicyPlusCore/Core/Caching/AdmxCachePolicyQueryService.cs`
- `PolicyPlusCore/Core/AdmxCache.cs` delegates `GetBy*` and categories query methods to the internal service

## Notes

- This ADR intentionally does not address search heuristics/ordering duplication between `AdmxCache.cs` and `Core/Caching/Search/*` (planned as ADR 0009).
- Exact type names and file names may evolve, but the responsibility boundary (read-side query + mapping extraction behind the facade) is the core contract.
