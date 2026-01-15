# ADR 0009: AdmxCache search helper consolidation

- Status: Accepted
- Date: 2026-01-13

## Context

`PolicyPlusCore/Core/AdmxCache.cs` previously contained helper methods used primarily by search:

- Query heuristics: `LooksLikeRegistryQuery`, `LooksLikeIdQuery`
- Result ordering: `AppendPriorityOrdered`
- Culture normalization: `NormalizeCultureName` (also used outside search)

During earlier steps, equivalent logic was introduced under `PolicyPlusCore/Core/Caching/Search/` (`SearchHeuristics`, `SearchOrdering`, and related request modeling). However, the search implementation (`AdmxCacheSearchService`) still referenced the helpers on `AdmxCache`, keeping an unnecessary dependency from internal search logic back to the public facade.

This ADR consolidates search-related helper logic in the internal search model and removes the remaining duplicate helper methods from the facade.

## Decision

- Update `AdmxCacheSearchService` to use helpers under `PolicyPlusCore.Core.Caching.Search` instead of calling into `AdmxCache`.
- Keep culture normalization available to all cache components via a small internal helper in `PolicyPlusCore/Core/Caching/`.
- Remove the now-unused search helper methods from `PolicyPlusCore/Core/AdmxCache.cs`.

## Rationale

- Reduces responsibility and size of `AdmxCache.cs`.
- Removes internal-to-public back-references (`AdmxCacheSearchService` -> `AdmxCache.*`).
- Keeps helper logic close to the search model, making future search refactors safer.

## Consequences

### Positive

- `AdmxCache.cs` no longer owns search heuristics/ordering.
- Search service becomes self-contained within `Core/Caching/*`.

### Negative / Risks

- Behavior drift risk when changing helper call sites.

### Mitigations

- Lift-and-shift the existing helper logic (no semantic changes intended).
- Run the full `PolicyPlusModTests` suite.

## Compatibility

- Public API impact: None
- Behavior changes: None intended (structure-only refactor)
- Migration steps:
  - Introduce a shared internal culture normalization helper
  - Switch `AdmxCacheSearchService` to use `SearchHeuristics` / `SearchOrdering`
  - Remove duplicate helpers from `AdmxCache.cs`

## Test Plan

References:

- `Docs/Architecture/Refactor/TestPlan.md` — “AdmxCache” search invariants.

Validation:

- `dotnet test PolicyPlusModTests/PolicyPlusModTests.csproj -c Debug-Unpackaged -r win-x64`

## Implementation

- Added `PolicyPlusCore/Core/Caching/AdmxCacheCulture.cs` for shared culture normalization.
- Updated `PolicyPlusCore/Core/Caching/AdmxCacheSearchService.cs` to use `SearchHeuristics` / `SearchOrdering`.
- Removed duplicated helper methods from `PolicyPlusCore/Core/AdmxCache.cs`.
