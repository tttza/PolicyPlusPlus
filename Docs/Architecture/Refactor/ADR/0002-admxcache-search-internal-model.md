# ADR 0002: AdmxCache Search Internal Model (Request + Value Objects)

- Status: Accepted
- Date: 2026-01-11

## Context

ADR 0001 extracted the implementation of `AdmxCache.SearchAsync(...)` into an internal service.
As of 2026-01-11, `AdmxCache` delegates the canonical multi-culture overload to `PolicyPlusCore/Core/Caching/AdmxCacheSearchService.cs`.

This reduced the size of `AdmxCache.cs`, but the extracted search implementation is still a large, multi-phase method that mixes:

- Input normalization and heuristics (short query, phrase mode, ID/registry detection)
- Culture preference normalization (primary/second/fallback)
- SQL query construction + parameterization
- Result post-processing (boosts, filtering, rescoring)

To continue refactoring safely (and keep ranking tests tie-tolerant), we need a clearer internal model that makes invariants explicit and keeps responsibilities separated.

This ADR focuses on internal structure only. It is intentionally behavior-preserving and does not change public APIs.

## Decision

### 1) Keep public API stable
- No changes to `IAdmxCache`.
- Keep the existing `AdmxCache.SearchAsync(...)` overload set.

### 2) Introduce a minimal internal “search request” model
Introduce internal types used only inside the search implementation:

- `SearchRequest` (application-level request object)
  - Holds: query, cultures, fields, and-mode, limit.
  - Enforces basic invariants (empty query/None fields => empty result; limit bounds; culture list fallback).

- `SearchQuery` (value object)
  - Holds: raw query and normalized representations used by search.
  - Exposes derived flags needed by the algorithm (short query, phrase mode, registry/id heuristics).

- `CulturePreference` (value object)
  - Holds normalized cultures, plus derived `Primary` and `Second` cultures.
  - Enforces the existing rule: `Second` cannot equal `Primary` (case-insensitive), and fallback ordering is preserved.

- `SearchFieldSet` (value object)
  - Wraps `SearchFields` and exposes derived booleans (UseName/UseId/UseRegistry/UseDescription).
  - Centralizes any gating rules (e.g., registry field enabled only when the query looks like a registry path).

To minimize churn, these types will be implemented in a single file initially (e.g. `PolicyPlusCore/Core/Caching/Search/SearchModel.cs`).
We intentionally avoid creating many small files until the boundary stabilizes.

### 3) Keep infrastructure details isolated
SQLite-specific query construction will remain in the search service for now, but it must only depend on the internal model types and not on facade-only helpers.

Follow-up refactors may split SQL building and post-processing into separate internal components once the model is in place.

### 4) Placement / folder layout rules (DDD-aligned, incremental)

We will not attempt to reorganize the entire `PolicyPlusCore/Core` folder as part of this ADR.
Instead, we establish a local placement rule for Search so future DDD-oriented refactors have a consistent home.

Initial structure (minimal file count):

- `PolicyPlusCore/Core/AdmxCache.cs`
  - Public facade and overloads remain here.
  - Overloads must remain thin wrappers that map inputs to the canonical overload.

- `PolicyPlusCore/Core/Caching/AdmxCacheSearchService.cs`
  - Orchestrates search execution (SQLite access + applying the algorithm).

- `PolicyPlusCore/Core/Caching/Search/SearchModel.cs`
  - Contains the internal model types defined in this ADR (`SearchRequest`, `SearchQuery`, `CulturePreference`, `SearchFieldSet`).
  - Implemented as a single file initially to minimize churn.

Future direction (once stable):

- Pure value objects that become broadly useful beyond Search may later be promoted to a shared location (e.g., `PolicyPlusCore/Core/Domain/ValueObjects/`).
- SQLite-specific concerns may later be split out under a more explicit infrastructure area, but this is out of scope for this ADR.

## Rationale

- A request/value-object model makes implicit rules explicit and testable.
- Keeping the types internal preserves public API stability while improving maintainability.
- Starting with a single file reduces the overhead and keeps the refactor incremental.

## Consequences

### Positive
- Search behavior is easier to reason about and adjust safely.
- Culture and query normalization rules have single sources of truth.

### Negative / Risks
- Adds a small amount of indirection.

### Mitigations
- Keep types small, immutable, and internal.
- Add minimal tests to ensure overloads remain equivalent.

## Compatibility

- Public API impact: none.
- Behavior changes: none intended.
- Migration steps:
  1. Add internal search model types.
  2. Refactor `AdmxCacheSearchService` to use the model.
  3. Add tests for overload equivalence (non-brittle; tie-tolerant ranking preserved).

## Test Plan

- Maintain the tie-tolerant ranking contract described in `Docs/Architecture/Refactor/TestPlan.md`.
- Add a minimal overload-equivalence test suite:
  - `SearchAsync(query, culture, limit)` equals `SearchAsync(query, new[]{culture}, defaultFields, andMode:false, limit)`
  - `includeDescription` toggles only the `Description` field
- Run:
  - `dotnet test PolicyPlusModTests/PolicyPlusModTests.csproj -c Debug-Unpackaged -r win-x64`

## Notes

This ADR intentionally scopes to Search only. Scan/indexing/maintenance refactors remain out of scope here.
