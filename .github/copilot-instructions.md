# PolicyPlusMod – Coding Agent Onboarding (Concise, Rev. 2025-09 R2 – WinUI 3 Only)

(Authoritative quick brief. Keep user-facing answers in the user's language; code comments in English only.)

## 1. What This App Does
PolicyPlusMod loads Administrative Template (ADMX/ADML) definitions, lets users search / view / edit Windows Group Policy (registry-based) settings, and export/import them (REG / semantic policy). The modern WinUI 3 UI (`PolicyPlusPlus`) is the only UI. Core logic resides in `PolicyPlusCore` and stays UI-agnostic. An elevation host (`PolicyPPElevationHost`) handles privileged operations.

## 2. Tech Stack
- Language: C# (.NET 8; legacy VB already removed)
- Projects / Targets:
  - Core: `PolicyPlusCore` → `net8.0-windows`
  - WinUI 3 UI: `PolicyPlusPlus` → `net8.0-windows10.0.26100.0` (desktop app; min platform 10.0.17763.0)
  - Elevation Host: `PolicyPPElevationHost` → `net8.0-windows` (console/elevation helper)
  - Tests: `PolicyPlusModTests`, `PolicyPlusPlus.Tests.UI` (xUnit)
- Windows App SDK 1.8; CommunityToolkit WinUI controls
- SDK pinned by `global.json` (8.0.413) – do not drift
- Packaging: Dual path – Packaged (MSIX) for standard installs and Unpackaged (Velopack-enabled) for self-updating portable style. Config names with *-Unpackaged select the Velopack/update path; others build MSIX packages.


## 3. Repository Layout (High Value)
```
PolicyPlusPlus.sln
PolicyPlusCore/            Domain models & policy processing
PolicyPlusPlus/            WinUI 3 UI
PolicyPPElevationHost/     Elevation helper process
PolicyPlusModTests/        Core + limited UI logic tests
PolicyPlusPlus.Tests.UI/   WinUI 3 focused tests
Docs/                      Architecture & terminology
.github/                   Automation & this guide
```
Generated output: `artifacts/`, `obj/`, `bin/`.

## 4. Key Domain Concepts
- ADMX + ADML parsed into `AdmxBundle`.
- Policies → registry mutations (evaluation logic in Core).
- `IPolicySource` abstracts persistence targets (POL file, registry, etc.).
- Policy state = tri-state + element values.
- Pending changes tracking (queue, apply, discard, reapply) in UI services.

## 5. Build & Run (cmd.exe)
```
dotnet --version
dotnet restore PolicyPlusPlus.sln
dotnet build PolicyPlusPlus.sln -c Debug-Unpackaged
dotnet run --project PolicyPlusPlus/PolicyPlusPlus.csproj -c Debug-Unpackaged
```
Release build:
```
dotnet build PolicyPlusPlus.sln -c Release-Unpackaged
```
Packaged vs Unpackaged quick reference:
```
Debug / Release: Packaged=true (MSIX; Store-like deployment)
Debug-Unpackaged / Release-Unpackaged: Packaged=false (Velopack, self-contained when configured)
```
Publishing guidance:
1. Unpackaged: invoke publish profile (win-<Platform>-standalone) then Velopack pipeline packages outputs.
2. Packaged: standard dotnet build/publish generates MSIX with signing per certificate settings.

## 6. Coding Guidelines
- Favor small, clear methods; keep hot paths lean.
- Core first: put reusable logic in `PolicyPlusCore`; UI layer orchestrates only.
- Nullability: no blanket suppressions—fix causes.
- Warnings treated as errors.
- Only `IPolicySource` implementations touch the registry.
- Minimize public surface; prefer `internal`.
- Avoid new external dependencies unless essential (prefer BCL).
- No blocking of UI thread with file / network I/O (prefer async + background queue).
- Cache repeated lookups; avoid repeated XML parse; keep heavy LINQ off hot loops.
- Respect `global.json`.
- XAML code-behind minimal: prefer ViewModels / Services. Event handlers should delegate quickly.

Comment style (strict):
- Intent / rationale only when non-obvious.
- Prohibited prefixes (case-insensitive): `// New:`, `// Added:`, `// Removed:`, `// Deleted:`, `// Modified:`, `// Changed:`, `// Update:`, `// Old:` etc.
- No BEGIN/END change markers.
- Actionable TODOs only: `// TODO: <concise actionable item>`.
- Do not restate method name or parameters.
- No version / diff history commentary.

## 7. Typical Safe Feature Flow
1. Search repo to confirm feature not already implemented.
2. Extend domain model / logic in Core (add tests simultaneously).
3. Add or update xUnit tests (happy path + at least one edge case).
4. Integrate into WinUI 3 (graceful degradation if partial data).
5. Run `dotnet test` (Debug; optionally Release) until clean.
6. Assess performance with large ADMX set if logic on critical path.
7. Update docs only if public semantics changed.

## 8. Test Guidance
```
dotnet test PolicyPlusPlus.sln -c Debug-Unpackaged
```
- Mock `IPolicySource` for persistence tests.
- Parser tests: minimal synthetic ADMX/ADML fixtures; assert structural shape.
- Pending changes: verify queue apply/discard/reapply retains element values (lists, enums, numeric, multi-line text).
- Regression tests required before altering policy evaluation semantics.

UI test re-run policy (time saver):
- UI tests assume unit tests are passing. Always run unit tests first; if unit tests are failing, do not run UI tests.
- If only unit tests are failing and the latest UI tests have already passed, do not re-run UI tests.
- Run UI tests only when:
  - There are changes in `PolicyPlusPlus/` (XAML, code-behind, ViewModel/Service UI integration).
  - Changes touch resources affecting visual tree/navigation/bindings (`Resources/`, `Converters/`, `Dialogs/`, etc.).
  - Public contracts or evaluation semantics in `PolicyPlusCore` have changed with potential UI impact.
  - UI tests have not been executed for the latest commit.

Handy commands (cmd.exe):
```
# Unit tests only (fast)
dotnet test PolicyPlusModTests/PolicyPlusModTests.csproj -c Debug-Unpackaged

# UI tests only (heavy / only when needed)
dotnet test PolicyPlusPlus.Tests.UI/PolicyPlus.Tests.UI.csproj -c Debug-Unpackaged -- --stop-on-fail on
```

## 9. Performance & Edge Cases
- Large ADMX sets: maintain O(n) passes; pre-index by policy ID / registry path.
- Distinguish Disabled vs NotConfigured (Disabled may delete values; NotConfigured leaves defaults / may remove policy-specific values depending on semantics).
- List elements: enforce purge/name semantics correctly.
- Enum evidence scoring changes must have regression tests.
- Preserve ordering for multi-line text where user intent depends on order.
- Batch UI updates to avoid redraw storms (e.g., DataGrid refresh throttling).

## 10. Patterns / Conventions
- Registry mutation models remain small & immutable (name, type, data triple patterns).
- Explicit method names: `GetPolicyState`, `QueuePendingChange`, `ApplyPendingAsync`.
- Use `StringComparer.OrdinalIgnoreCase` where ADMX spec is case-insensitive.
- No reintroduction of WinForms-specific utility abstractions.

### Logging Guidance (Concise Rules)
Purpose: Minimize noise while preserving fast failure diagnostics with a single consistent shape across Core / UI / Elevation host.

Core / UI / Elevation shared principles:
- Use `Info` for significant lifecycle events (start/finish), `Warn` for partial/auto‑recovered conditions, `Error` for user‑visible failures. High frequency inner loop details limited to `Debug` or `Trace`.
- Log an exception only once at the boundary where it is handled. If rethrowing, do not log again upstream.
- Avoid PII / raw full data. Represent values as type+size, or prefix snippet + `...`, or a summary (count / hash).
- Large collections: log `count=` and up to 5 representative IDs only.
- Correlation ID: multi‑step operations (save / bulk apply / export) start with `var corr = Log.NewCorrelationId();` and prefix each log line with that token (e.g. `corr42`).
- `LogScope` pattern: `using var scope = LogScope.Info("Area", "corrX start operation"); scope.Complete();` and `scope.Capture(ex);` on exception.
- Guard expensive string building: `if (Log.IsEnabled(LogLevel.Debug))`.
- Area name: single PascalCase word or two segments max (`Save.Encode`).

Level guidance:
- Trace: Temporary deep investigation (disabled by default).
- Debug: Branch outcomes / statistics in dev/CI.
- Info: User action boundaries / phase summaries / single recoveries.
- Warn: Successful retry, partial skip, fallback adopted.
- Error: Operation failed (user impact) / unrecovered exception.

Core layer:
- Must not depend on UI logging types. Return DTOs if UI needs structured info.
- Minimize Trace/Debug inside hot loops (wrap calculations behind level checks).

UI layer:
- Each user command/button: one Info for start, one Info for completion (success/fail).
- Bulk list refresh: single Info summary + optional Debug detail; suppress per‑item spam.
- Rely on UI log viewer filtering instead of verbose duplication.

Elevation host:
- Always log start/success/failure of elevated operations; hash or normalize inputs instead of dumping raw paths/values.
- Batch / buffer writes to reduce file I/O.

Performance:
- Even with Debug enabled on 1000+ policies, average ≤1 line per policy.
- Add Stopwatch only if existing `LogScope` duration is insufficient.

Testing guidance:
- Tests assert presence of key tokens (e.g. corr, count) not full message text nor timestamps.

Prohibited (excerpt):
- Concatenating full user input into exception messages.
- Dumping raw POL/REG large blobs.
- Adding public APIs purely for logging.

Recommended format example:
```
SAVE42 start changes=12
SAVE42 validate ok changed=12 skipped=0
SAVE42 apply warn partial policyId=XYZ missing=Definition
SAVE42 done elapsedMs=153
```

This subsection is an additive guidance block and does not affect numbering of later sections.

## 11. What NOT To Do
- Do not add domain logic to XAML code-behind that belongs in Core / Services.
- Do not introduce heavy reflection/dynamic invoke in hot loops.
- Do not silently change policy semantics (add/update tests first).
- Do not block UI thread with I/O.
- Do not expand public API surface without clear cross-layer need.
- Do not reintroduce WinForms artifacts or dependencies.

## 12. Rapid Orientation Checklist
Before coding:
- Skim `PolicyPlusCore` for existing helpers.
- Search for similar option handling patterns (lists, enums, multi-text).
- Identify existing services to extend instead of duplicating logic.

During:
- Validate inputs early; fast-return on null/empty.
- Keep per-policy recalculations minimal (cache descriptors / lookups).
- Avoid unnecessary allocations in tight loops.

After:
```
dotnet build PolicyPlusPlus.sln -c Debug-Unpackaged
dotnet test PolicyPlusPlus.sln -c Debug-Unpackaged
dotnet build PolicyPlusPlus.sln -c Release-Unpackaged
```
Optionally run a quick UI smoke (launch app, open a policy, view the pending changes window).

## 13. Sample Feature Walkthrough (Illustrative)
Goal: Add helper `PolicyLookup.FindPolicy(bundle, uniqueId)`.
Steps:
- Add `PolicyPlusCore/Utilities/PolicyLookup.cs`.
- Implement dictionary lookup via `AdmxBundle.Policies`.
- Add tests (found / missing ID).
- Build + test.
- Replace duplicative lookups in UI.

## 14. Communication Rules (Agent)
- Always respond (never silent).
- First reply per task: one-line purposeful summary + next action.
- Provide concise reasoning only when it adds value.
- For code changes: show resulting code (minimal diff context) – comments English only.
- Commands (cmd.exe): one per line in fenced blocks.
- On failure: show failing lines + root cause + specific fix.
- State what was searched/read when uncertain before inferring.
- Avoid echoing large unchanged instruction blocks.
- Keep answers helpful but not ambiguous.

## 15. Quality Gate Before PR
- Debug & Release builds succeed (Core, WinUI 3, elevation host, tests).
- All tests pass (including new ones) with no warnings.
- WinUI 3 app launches (main window functional, basic navigation OK).
- No residual WinForms references (projects, namespaces, packages).
- Feature degrades gracefully when optional data absent (no crashes).
- No prohibited comment patterns.
- Performance on large ADMX set not regressed (spot check typical operations).
- Avoid unnecessary UI test re-runs (see Section 8).
 - UI tests are executed only after unit tests pass (Section 8).

## 16. Comment Style Examples
Good:
```csharp
// Caches policy index for O(1) lookup during evaluation.
```
Good (rationale):
```csharp
// Using ordinal ignore case because ADMX IDs are case-insensitive.
```
Good TODO:
```csharp
// TODO: Add per-user hive override once profile service exposes path.
```

## 17. Prohibited Comment Detector (Heuristic)
Avoid generating lines matching (regex, case-insensitive):
```
^\s*//\s*(new|added|removed|deleted|modified|changed|update|old)\b
```
Rephrase or omit.

## 18. Fast Self-Check (Agent)
- Any prohibited prefixes?
- Any diff-history narration?
- Only intent/rationale comments added?
- Tests updated for semantic changes?
- Public surface minimized?
- Reflection avoided in hot paths?
- Prohibited comment patterns avoided?
- Comments English only?