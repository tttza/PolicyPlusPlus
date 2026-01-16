# Working Assumptions (Living Doc)

**Status:** Living (non-normative)

**Last updated:** 2026-01-14

This document captures current, changeable assumptions discussed during the `PolicyPlusCore/Core` refactor.

It is intentionally a **living note**:

- It may change as we learn more.
- It is not an ADR and does not imply a final decision.
- When a topic stabilizes, we should promote it into an ADR (or into `Overview.md`) and keep this file concise.

## Why this exists

The refactor is progressing incrementally (facade + internal services). Several cross-cutting design questions emerged:

- How we interpret DDD-ish layering in this repo (Domain / Application / Infrastructure)
- Where “culture semantics” should live (`Core/Culture` vs `Utilities` vs `Core/Caching`)
- How to name and place “services” to avoid confusing Domain Services with Infrastructure orchestration
- What role `Utilities` should play while the refactor is in flight

This file is a low-friction place to keep these assumptions in sync.

## Scope

- Applies primarily to `PolicyPlusCore`.
- UI (`PolicyPlusPlus`) remains a separate layer.

## Layering assumptions

We use DDD terminology as a guideline for dependency direction rather than as a strict framework.

### Domain

Domain is “pure” semantics:

- Value Objects / normalization / validation / comparison rules
- No SQLite/FTS, no filesystem enumeration, no environment-variable branching

In this repo, Domain code is expected to live under `PolicyPlusCore/Core/<Area>/` (e.g., `Core/Culture`).

### Application (Use cases)

Application coordinates domain rules and infrastructure through abstractions.

We may introduce a `PolicyPlusCore/Application/` folder when we have clear cross-cutting use cases that are not well-represented by existing facades.

### Infrastructure

Infrastructure contains concrete IO and persistence:

- SQLite/FTS, file enumeration, registry IO, environment-variable branching

In this repo, infrastructure currently maps to:

- Target: `PolicyPlusCore/Infrastructure/*`
  - `Infrastructure/Caching/*` (cache storage + scan/search pipelines; SQLite/FTS)
  - `Infrastructure/IO/*` (file formats, storage, loaders)
  - `Infrastructure/OS/*` (OS/PInvoke)
- Current: some infrastructure code still lives under legacy locations (e.g., `Core/Caching/*`, `IO/*`, `Helpers/*`). Treat these as Infrastructure-equivalent; Core must not depend on them.

## Dependency direction

Allowed direction (high level):

```
Domain (Core/<Area>)
  ^
  |
Application (Application)   (optional)
  ^
  |
Infrastructure (Infrastructure/*)
  ^
  |
UI (PolicyPlusPlus)
```

Notes:

- Infrastructure may depend on Domain (infra → domain), but Domain must not depend on Infrastructure.
- Public entry points (facades) are treated as the Application boundary: they may delegate into Infrastructure and Domain but should stay thin.

## Naming assumptions

The goal is to make “what kind of thing this is” obvious from the name.

### Domain-ish names (prefer these under Core/<Area>)

- `*Name`, `*Id`, `*Key`, `*Path` (Value Objects)
- `*Normalization`, `*Comparer`, `*Rules`, `*Policy` (pure logic)

### Infrastructure-ish names (prefer these under Infrastructure/*)

- `*Store` (persistence wrapper around a table/file/etc.)
- `*OrchestrationService` (top-level coordination of multiple steps)
- `*MaintenanceService`, `*Scan*Service`, `*Search*Service` (dependency-heavy, IO-aware)

We avoid treating every `*Service` as a Domain Service; “Service” is not a layer by itself.

## Directory sketch (example)

This is an illustrative target shape; it does not imply immediate moves.

```
PolicyPlusCore/
├─ Core/                               (domain semantics)
│  ├─ Culture/
│  │  ├─ CultureNameNormalization.cs
│  │  └─ (future) CultureName.cs
│  └─ Search/                          (search domain: query/ordering/heuristics)
│     └─ SearchModel.cs
├─ Application/                        (optional; public facades / use cases)
│  └─ (future) AdmxCacheFacade.cs
└─ Infrastructure/
   ├─ Caching/                         (SQLite/FTS + scan/search pipelines)
   │  ├─ AdmxCacheScanOrchestrationService.cs
   │  ├─ AdmxCacheSearchService.cs
   │  └─ AdmxCache*Store.cs
   ├─ IO/
   └─ OS/
```

## Culture-related assumptions

### `Core/Culture` is allowed to start small

Having only `CultureNameNormalization` under `Core/Culture` is acceptable as an intermediate step.

Risks to watch:

- If the folder stays single-file forever, it can look arbitrary.
- If `Utilities` continues to accumulate “domain-ish” helpers, boundaries become unclear.

### What belongs where

- `Core/Culture`: normalization/validation/comparison rules; future `CultureName` VO
- `Infrastructure/Caching/*`: ADML missing gate, purge, scan planning, SQL/FTS, maintenance
- `Utilities`: transitional/shared helpers; avoid adding new domain semantics here unless there is a short-term reason

## Utilities assumptions

`PolicyPlusCore/Utilities` is currently treated as a transitional bucket.

Guideline:

- Prefer placing new code into Domain/Application/Infrastructure folders.
- When touching an existing `Utilities` helper, consider whether it is domain-ish or infrastructure-ish and whether it should migrate.
- Do not force a bulk migration; use ADR-sized, reversible steps.

## Related ADRs (stable decisions)

- `ADR 0012`: target folder layering (Core/Application/Infrastructure) and treat facades as Application boundary
- `ADR 0010`: `Core/Culture` for culture name normalization (and future VO path)
- `ADR 0011`: scan orchestration extracted behind internal service; tracing remains in facade

## Promotion criteria

Promote a topic from this file into an ADR when:

- The repo has at least one concrete implementation relying on the rule.
- There is a test-backed behavior contract that would regress without the rule.
- We expect the rule to be stable for the next several refactor steps.
