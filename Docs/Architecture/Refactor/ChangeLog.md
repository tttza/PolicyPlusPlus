# Refactor Change Log

This log summarizes refactor milestones. Keep entries short and user-impact oriented.

Format:

- Date (YYYY-MM-DD): Summary
  - Scope
  - Notes / Risks
  - Test coverage

## Unreleased

- ADR 0013 complete: PolicyProcessing facade extraction (phase 3b/3c)
  - Scope: Extract `WalkPolicyRegistry`/`ForgetPolicy` → PolicyRegistryWalker, `IsPolicySupported` → PolicySupportChecker
  - Notes: PolicyProcessing.cs reduced from 1,511 to 182 lines. Now pure delegation facade + public types (PolicyState, RegistryKeyValuePair). DeduplicatePolicies remains (bundle-related, deferred).
  - Test coverage: Existing tests cover all extracted methods

- ADR 0013 implemented: PolicyProcessing facade extraction (phase 3a)
  - Scope: Extract `GetPolicyOptionStates` element reading logic from PolicyPlusCore/Core/PolicyProcessing.cs into PolicyPlusCore/Core/Policies/PolicyOptionReader.cs
  - Notes: Structure-only; preserves element-type handling (decimal, boolean, text, list, enum, multiText), list prefix enumeration, and registry read semantics. Uses PolicyStateEvaluator.ValuePresent/ValueListPresent for enum matching.
  - Test coverage: Existing PolicyProcessing tests cover GetPolicyOptionStates via facade delegation

- ADR 0013 implemented: PolicyProcessing facade extraction (phase 2)
  - Scope: Extract `SetPolicyState` application logic from PolicyPlusCore/Core/PolicyProcessing.cs into PolicyPlusCore/Core/Policies/PolicyStateApplier.cs
  - Notes: Structure-only; preserves element-type handling (decimal, boolean, text, list, enum, multiText), ConfiguredPolicyTracker integration, and registry write semantics.
  - Test coverage: PolicyPlusModTests full suite (315 tests pass)

- ADR 0013 implemented: PolicyProcessing facade extraction (phase 1)
  - Scope: Extract `GetPolicyState` evaluation logic from PolicyPlusCore/Core/PolicyProcessing.cs into PolicyPlusCore/Core/Policies/PolicyStateEvaluator.cs
  - Notes: Structure-only; preserves evidence scoring, explicit match priority, and element-type handling. CachedPolicySource moved to internal service.
  - Test coverage: PolicyPlusModTests full suite (315 tests pass)
  - Characterization tests (23 tests in PolicyStateEvaluatorCharacterizationTests.cs):
    - ✅ Explicit match priority (On/Off/Mixed scenarios)
    - ✅ Evidence tie behavior (Off wins on equal evidence)
    - ✅ Boolean OffValue weight (0.1 multiplier)
    - ✅ Null/empty/whitespace handling
    - ✅ List element evidence (ClearKey + values)
    - ✅ Element evidence count conditions
    - ✅ ValueList explicit match (Root/Boolean OnValueList/OffValueList)
    - ✅ ValueList evidence scoring (Root/Boolean OnValueList/OffValueList with 0.1 weight)
    - ✅ Enum with ValueList (requires both Value and ValueList match)
    - ✅ ValueList custom DefaultRegistryKey and entry RegistryKey

- ADR 0012 accepted: target folder layering (Core / Application / Infrastructure)
  - Scope: Clarify dependency direction and target locations for infrastructure-heavy code
  - Notes: No code moves required; legacy paths remain valid during incremental migration
  - Test coverage: N/A (docs)

- Docs aligned to target layering vocabulary
  - Scope: WorkingAssumptions + Overview + README
  - Notes: Facades treated as Application boundary; Infrastructure to move under top-level folder in future refactors
  - Test coverage: N/A (docs)

- ADR 0011 implemented: AdmxCache scan orchestration extraction
  - Scope: Move ScanAndUpdateCoreAsync orchestration into an internal service; keep AdmxCacheTrace in the facade via delegate
  - Notes: Lift-and-shift; trace scope names preserved
  - Test coverage: PolicyPlusModTests full suite

- ADR 0010 implemented (phase 1): Core/Culture culture name normalization
  - Scope: Introduce PolicyPlusCore/Core/Culture/CultureNameNormalization and route internal normalization through it
  - Notes: Behavior-preserving; Utilities shim kept temporarily to minimize churn
  - Test coverage: PolicyPlusModTests full suite

- ADR 0009 implemented: AdmxCache search helper consolidation
  - Scope: Remove search heuristic/ordering helpers from PolicyPlusCore/Core/AdmxCache.cs; use internal Search model helpers instead
  - Notes: Structure-only; preserves search heuristics and ordering behavior while removing internal-to-public back-references
  - Test coverage: PolicyPlusModTests full suite

- ADR 0008 implemented: AdmxCache read-side query extraction
  - Scope: Extract read-side SQL + mapping (GetByPolicyNameAsync/GetByRegistryPathAsync/categories query) from PolicyPlusCore/Core/AdmxCache.cs into an internal service behind the facade
  - Notes: Structure-only; preserves query shapes, culture fallback/normalization, and mapping semantics
  - Test coverage: PolicyPlusModTests full suite

- ADR 0007 implemented: AdmxCache source signature extraction
  - Scope: Extract `ComputeSourceSignatureAsync` from PolicyPlusCore/Core/AdmxCache.cs into an internal builder behind the facade
  - Notes: Structure-only; preserves env-var branching, file enumeration, hashing fallback, and cancellation/exception semantics
  - Test coverage: PolicyPlusModTests full suite

- ADR 0006 implemented: AdmxCache culture purge extraction
  - Scope: Extract `PurgeCultureAsync` from PolicyPlusCore/Core/AdmxCache.cs into an internal service behind the facade
  - Notes: Structure-only; preserves transaction boundaries, exception swallowing, and writer-gate behavior
  - Test coverage: PolicyPlusModTests full suite

- ADR 0005 implemented: AdmxCache initialization split + writer gate
  - Scope: PolicyPlusCore/Core AdmxCache InitializeAsync delegation + shared writer-lock acquisition helper (and adoption in writer-serialized call sites)
  - Notes: Structure-only; no behavior changes intended; reduces drift risk for retry/fast-mode semantics
  - Test coverage: PolicyPlusModTests full suite

- AdmxCache write/apply pipeline extracted behind internal services (ADR 0004)
  - Scope: PolicyPlusCore/Core AdmxCache write-side refactor (meta + upsert + apply)
  - Notes: Lift-and-shift only; preserves writer-lock, transaction boundaries, batching/yield, env-var behavior
  - Test coverage: PolicyPlusModTests full suite

- AdmxCache apply pipeline extracted behind internal service (ADR 0004)
  - Scope: PolicyPlusCore/Core AdmxCache DiffAndApplyAsync moved to AdmxCacheApplyService
  - Notes: Keeps public facade stable; delegates with helper delegates to avoid semantics drift
  - Test coverage: PolicyPlusModTests full suite

- AdmxCache policy upsert extracted behind internal service (ADR 0004)
  - Scope: PolicyPlusCore/Core AdmxCache upsert SQL moved to AdmxCachePolicyUpsertService
  - Notes: Preserves prepared-command path and the "no fallback persistence" rule for blank display names
  - Test coverage: PolicyPlusModTests full suite

- AdmxCache meta persistence extracted behind internal store (ADR 0004)
  - Scope: PolicyPlusCore/Core AdmxCache meta reads/writes moved to AdmxCacheMetaStore
  - Notes: Preserves error handling behavior (safe fallbacks)
  - Test coverage: PolicyPlusModTests full suite

- AdmxCache file-usage persistence extracted behind internal store (ADR 0004)
  - Scope: PolicyPlusCore/Core AdmxCache FileUsage schema/open/upsert/purge moved to AdmxCacheFileUsageStore
  - Notes: Lift-and-shift; preserves schema ensure semantics and purge writer-lock/fast-mode behavior
  - Test coverage: PolicyPlusModTests full suite

- ADR 0003 scan refactor completed
  - Scope: PolicyPlusCore/Core AdmxCache scan/maintenance service split end-to-end
  - Notes: Completes characterization coverage including mixed valid/broken ADMX tolerance
  - Test coverage: PolicyPlusModTests scan characterization + full suite

- AdmxCache scan maintenance extracted behind internal service (ADR 0003)
  - Scope: PolicyPlusCore/Core AdmxCache scan end-of-run maintenance
  - Notes: Keeps public API stable; introduces an internal seam for follow-on refactors
  - Test coverage: PolicyPlusModTests scan characterization + full suite

- AdmxCache scan culture planning extracted behind internal service (ADR 0003)
  - Scope: PolicyPlusCore/Core AdmxCache scan culture list construction
  - Notes: Centralizes default culture + global rebuild culture preservation logic
  - Test coverage: PolicyPlusModTests scan characterization + full suite

- AdmxCache scan signature decision extracted behind internal service (ADR 0003)
  - Scope: PolicyPlusCore/Core AdmxCache scan signature compare + persistence
  - Notes: Centralizes unchanged-culture skip decision and signature storage
  - Test coverage: PolicyPlusModTests scan characterization + full suite

- AdmxCache scan bundle/file-usage step extracted behind internal service (ADR 0003)
  - Scope: PolicyPlusCore/Core AdmxCache scan per-file bundle load and FileUsage refresh
  - Notes: Removes duplicated code paths for skipped vs non-skipped cultures
  - Test coverage: PolicyPlusModTests scan characterization + full suite

- AdmxCache scan culture gate extracted behind internal service (ADR 0003)
  - Scope: PolicyPlusCore/Core AdmxCache scan ADML presence check + purge/skip
  - Notes: Centralizes the "no fallback persistence" rule without changing behavior
  - Test coverage: PolicyPlusModTests scan characterization + full suite

- AdmxCache scan global rebuild meta update extracted behind internal service (ADR 0003)
  - Scope: PolicyPlusCore/Core AdmxCache scan end-of-run source root meta update
  - Notes: Isolates Meta persistence behind a small internal seam; behavior unchanged
  - Test coverage: PolicyPlusModTests scan characterization + full suite

- Search test contract clarified: do not pin tie ordering
  - Scope: AdmxCache search ranking tests
  - Notes: Prefer Top-K membership + score separation checks; allow ties
  - Test coverage: PolicyPlusModTests AdmxCache ranking suite updated

- AdmxCache search extracted behind internal service
  - Scope: PolicyPlusCore/Core AdmxCache search implementation
  - Notes: AdmxCache remains the public facade; search logic moved to internal service for incremental refactor
  - Test coverage: PolicyPlusModTests (286 passing)

- AdmxCache search internal model introduced (ADR 0002)
  - Scope: PolicyPlusCore/Core/Caching/Search internal model + service wiring
  - Notes: Adds internal request/value objects (single-file start) and removes AdmxCache helper dependencies from the search service
  - Test coverage: PolicyPlusModTests overload equivalence suite added

## 2026-01-10

- Initialized refactor documentation set (spec-driven)
  - Scope: PolicyPlusCore/Core refactor planning
  - Notes: Behavior-preserving refactor; add characterization tests before extraction
  - Test coverage: TestPlan drafted; implementation not started
