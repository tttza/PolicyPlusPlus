# ADR 0003: AdmxCache Scan/Maintenance as Internal Services

- Status: Accepted
- Date: 2026-01-13

## Context

ADR 0001/0002 established a pattern for refactoring `AdmxCache` safely:

- Keep `IAdmxCache` and `AdmxCache` public surface stable.
- Move high-branching logic behind internal services.
- Introduce small internal models/value objects where they reduce scattered branching.
- Keep ranking tests tie-tolerant; prefer characterization tests over brittle assertions.

After ADR 0002, the `SearchAsync(...)` implementation is delegated to `PolicyPlusCore/Core/Caching/AdmxCacheSearchService.cs` and uses an internal model (`PolicyPlusCore/Core/Caching/Search/SearchModel.cs`).

The next highest-risk/highest-branching part of `AdmxCache` is the scan/update path, which mixes:

- Culture selection and normalization rules
- Scan decisions (what to parse, what to skip, how to detect changes)
- Index persistence and incremental updates in SQLite
- File usage tracking and schema management
- Maintenance decisions (compaction/FTS maintenance/PRAGMA behavior)
- Error handling and partial failure tolerance

This mixture makes behavior-preserving refactors hard because it is difficult to isolate decisions and to test them.

## Decision

### 1) Keep public API stable

- No changes to `IAdmxCache`.
- Keep `AdmxCache.InitializeAsync`, `SetSourceRoot`, `ScanAndUpdateAsync` public behavior and signatures.

### 2) Split scan/update responsibilities behind internal services

Introduce internal components under `PolicyPlusCore/Core/Caching/` (names may evolve, responsibilities should not):

- `AdmxCacheScanService`
  - Orchestrates `ScanAndUpdateAsync`.
  - Owns scan decision logic (what needs rebuild, incremental vs global rebuild, culture list application).
  - May call lower-level stores/components.

- `AdmxCacheMaintenanceService`
  - Owns maintenance and compaction decisions and operations.
  - Encapsulates SQLite maintenance operations (e.g., FTS rebuild/optimize/PRAGMAs) and scheduling logic.

- (optional, later) `AdmxCacheFileUsageStore`
  - Encapsulates file usage schema creation and file usage recording.
  - This is deferred until we have characterization tests for file usage behaviors.

`AdmxCache` remains a facade:

- Public methods stay in `PolicyPlusCore/Core/AdmxCache.cs`.
- The facade should delegate early to the internal services.

### 3) Establish local placement rules (incremental, DDD-aligned)

We do not reorganize all of `PolicyPlusCore/Core` in this ADR.

- `PolicyPlusCore/Core/AdmxCache.cs`
  - Public facade and small adapters only.
  - Keeps the public overloads and lifecycle entry points.

- `PolicyPlusCore/Core/Caching/*`
  - Orchestrators and infrastructure-heavy services related to the cache.

- Internal models/value objects introduced for Scan/Maintenance should live near the services first.
  - If a model proves broadly useful across multiple areas, it can be promoted later.

### 4) Behavior preservation contract

This ADR is a structure-only refactor. It does not intentionally change:

- What gets indexed for the same input ADMX/ADML tree
- Culture fallback/ordering behavior
- Incremental vs global rebuild thresholds
- Maintenance scheduling decisions
- Error tolerance (which exceptions are swallowed vs surfaced)

If any semantic change is needed, it must be captured in a separate ADR with explicit tests.

## Rationale

- The scan/update path has similar complexity to search, but with more stateful and IO-heavy concerns.
- A service split makes it possible to add targeted characterization tests (scan invariants) before extracting deeper pieces.
- Keeping `AdmxCache` as a facade preserves compatibility with UI and existing callers.

## Consequences

### Positive

- Clearer boundaries and a smaller `AdmxCache` facade.
- Easier to add scan-specific characterization tests.
- Incremental extraction path mirrors the already-successful Search extraction.

### Negative / Risks

- Risk of subtly changing error handling or ordering by moving code.
- Additional indirection and more internal files.

### Mitigations

- Introduce characterization tests before moving logic.
- Move code in small steps (single-responsibility blocks), re-run tests after each step.
- Keep exception behavior unchanged unless explicitly addressed.

## Compatibility

- Public API impact: none.
- Behavior changes: none intended.
- Migration steps:
  1. Add characterization tests for scan/update invariants.
  2. Introduce `AdmxCacheScanService` and delegate from `AdmxCache.ScanAndUpdateAsync`.
  3. Extract maintenance calls into `AdmxCacheMaintenanceService`.
  4. Extract scan culture planning into `AdmxCacheScanCulturePlanService`.
  4.5 Extract culture gate (missing ADML => purge + skip) into `AdmxCacheScanCultureGateService`.
  5. Extract signature/skip decision + signature persistence into `AdmxCacheScanSignatureService`.
  6. Extract bundle load + file usage refresh into `AdmxCacheScanBundleAndUsageService`.
  6.5 Extract global rebuild meta updates into `AdmxCacheScanMetaService`.

## Test Plan

Add/extend tests under `PolicyPlusModTests` (and use existing isolation patterns for cache tests):

- Characterization: scan is stable on repeated runs
  - Build a minimal synthetic ADMX/ADML root and run `ScanAndUpdateAsync` twice.
  - Assert the set of policy unique IDs is unchanged.

- Characterization: scan detects ADMX changes
  - Run scan, then mutate the synthetic ADMX/ADML to add a new policy.
  - Run scan again and assert the new policy unique ID becomes searchable.

- Characterization: missing culture ADML removes culture-specific search terms
  - Run scan for `en-US` and a second culture with localized strings (e.g., `fr-FR`).
  - Delete the second culture's ADML file and re-scan.
  - Assert a query containing only culture-specific terms no longer matches.

- Characterization: culture preference ordering is respected
  - Provide multiple cultures and assert the selected `PolicyI18n` culture for a known policy is stable.
  - Avoid ordering assertions when ties are possible.

- Characterization: partial failures do not abort the whole scan
  - Provide one valid ADMX and one intentionally broken/unsupported file.
  - Assert scan completes and still indexes the valid policy set.

Run:

- `dotnet test PolicyPlusModTests/PolicyPlusModTests.csproj -c Debug-Unpackaged -r win-x64`

## Notes

In DDD terms, `PolicyPlusCore/Core/Caching/*` is treated as infrastructure-equivalent code (IO-heavy: SQLite, filesystem scanning, cache persistence). It lives under `Core/` for now to minimize churn while we extract responsibilities behind the existing `AdmxCache` facade. Once the boundaries stabilize, we may move these implementations under a dedicated infrastructure area (e.g., `PolicyPlusCore/Core/Infrastructure/AdmxCache/*`) without changing the public API.

This ADR only establishes boundaries and the extraction direction. It intentionally does not decide:

- Whether scan uses a dedicated internal request model (similar to `SearchRequest`)
- Whether file usage tracking should be split immediately

Those decisions should be made after characterization tests clarify current behavior.
