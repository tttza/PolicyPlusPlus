# Refactor Overview (PolicyPlusCore)

## Problem Statement

The current Core codebase contains branching decisions that are spread across multiple files and methods. This increases the risk of regressions when changes are required.

Typical symptoms:

- Similar decisions implemented in multiple places
- Large methods mixing responsibilities (policy evaluation + IO wiring + caching + heuristics)
- Hard-to-test logic due to implicit environment branching

## Goals

- Reduce scattered branching by **centralizing decisions** in dedicated components.
- Improve change safety using **spec-driven development** (TestPlan-first).
- Preserve public APIs and behavior while refactoring (incremental, reversible).
- Keep Core UI-agnostic and avoid new heavy dependencies.

For changeable/unfinished design assumptions (layering, naming, and directory conventions), see `WorkingAssumptions.md`.

## Domain Modeling Intent (DDD)

As part of this refactor, we intentionally move toward a clearer domain model using **Entities** and **Value Objects** where it improves correctness and reduces branching.

### Entities

Entities represent concepts with identity and lifecycle (identity-based equality).

Examples (candidates, not mandatory):

- A policy definition identity (e.g., policy unique id) and its lifecycle within a bundle/index
- A persistence target instance (e.g., a policy source instance) with stateful lifecycle

### Value Objects

Value Objects represent immutable concepts with value-based equality.

Examples (candidates, not mandatory):

- Culture name / preference (normalized culture identifier)
- Registry path/value identifiers (normalized hive/path/name triples)
- Policy key (stable identifier), search query tokens, search fields

### Guardrails

- Do not introduce DDD types for their own sake; only when they reduce scattered branching and improve testability.
- Prefer small immutable types and keep them in Core (UI-agnostic).
- Keep public APIs stable; introduce new domain types internally first and adapt at boundaries.

## Non-goals

- No semantic changes to policy evaluation unless explicitly approved.
- No large-scale rewrite.
- No new persistence formats.

## Target Architecture (Incremental)

We will keep existing public entry points and introduce internal components behind them.

### Proposed internal structure

The target layering is:

- `PolicyPlusCore/Core/` — Domain-ish semantics (no SQLite/FTS, no filesystem enumeration, no environment-variable branching)
- `PolicyPlusCore/Infrastructure/` — concrete IO + persistence (SQLite/FTS, file enumeration, registry IO, OS/PInvoke)
- `PolicyPlusCore/Application/` — optional; use cases and public facades as the Application boundary

We can adopt this incrementally while keeping existing files and namespaces stable:

- `Core/Culture/`
  - Culture name normalization and preference ordering (Value Object candidates)

- `Core/Search/`
  - Search domain logic: query tokenization, field selection, ordering rules, heuristics (no DB dependency)

- `Infrastructure/Caching/`
  - `AdmxCacheScanService.cs` — scan/culture/signature decisions
  - `AdmxCacheScanOrchestrationService.cs` — scan top-level orchestration behind the facade
  - `AdmxCacheSearchService.cs` — search SQL/FTS execution (uses `Core/Search` for domain logic)
  - `AdmxCacheMaintenanceService.cs` — maintenance/compaction/FTS decisions
  - `AdmxCacheFileUsageStore.cs` — isolates file-usage tracking concerns
  - `AdmxCacheCulturePurgeService.cs` — culture-localized purge (I18n + FTS rows)
  - `AdmxCacheSourceSignatureBuilder.cs` — source signature computation (ADMX/ADML)
  - `AdmxCacheInitializationService.cs` — initialization orchestration behind the facade
  - `AdmxCacheWriterGate.cs` — shared cross-process writer serialization helper

- `Application/`
  - (optional) public facades and use cases; coordinates Domain + Infrastructure

- `Core/Policies/`
  - `PolicyEvaluator.cs` — evaluation and evidence scoring (pure logic)
  - `PolicyRegistryWalker.cs` — walking/reading policy-related registry representation
  - `PolicyApplier.cs` — apply/derive registry mutations for a policy state
  - `PolicySaveService.cs` — orchestration behind the current save pipeline facade

This is not a hard rule; we can adjust as we discover better boundaries.

## Migration Strategy (Coexistence)

### Strangler/Façade approach

- Keep existing files (e.g., `AdmxCache.cs`, `PolicyProcessing.cs`, `PolicySavePipeline.cs`) as **facades**.
- New behavior-preserving code goes into new internal classes.
- Old methods become thin forwarders.

Facades are treated as the Application boundary (even if temporarily located under `PolicyPlusCore/Core`).

### Adapters for environment branching

Where environment/IO branching exists:

- Introduce internal factories (e.g., `IPolicySourceFactory`) to isolate the decision logic.
- Default implementation preserves current behavior.
- Tests can inject fakes to reduce brittleness.

## Naming Guidelines

- Avoid `V2` unless semantics intentionally diverge.
- Use suffixes to communicate responsibility:
  - `*Service` — orchestration and dependency-heavy behavior
  - `*Evaluator` — pure or mostly-pure evaluation logic
  - `*Applier` — derives/apply mutations
  - `*Store` — persistence wrapper over a table/file/etc.
  - `*Facade` — public entry point that delegates
  - `*Adapter` — bridges old and new shapes
- Prefer `internal` over name-based hiding.
- Keep namespaces aligned to folders for new code.

## First Candidate Extraction Steps

- Extract policy evaluation (read-only) into `PolicyEvaluator`.
- Split AdmxCache into scan/search/maintenance subcomponents.
- Move save pipeline orchestration into `PolicySaveService`.

See [TestPlan.md](TestPlan.md) for the safety contract.
