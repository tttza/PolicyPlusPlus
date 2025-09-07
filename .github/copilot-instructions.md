# PolicyPlusMod – Coding Agent Onboarding (Concise, Rev. 2025-09)

(Authoritative quick brief when repo is first loaded. Keep user-facing answers in the user's language; code comments in English only.)

## 1. What This App Does
PolicyPlusMod is a Windows desktop tool that loads Administrative Template (ADMX/ADML) definitions, lets users view/search/edit Windows Group Policy (registry-based) settings, and export/import them (REG / semantic policy). Two UIs share one domain layer:
- `PolicyPlus` (WinForms) – stable reference implementation.
- `PolicyPlus.WinUI3` (WinUI 3) – modernization in progress.
Core logic belongs in `PolicyPlus.Core` and must remain UI-agnostic.

## 2. Tech Stack
- Language: C# (legacy VB already ported; VB files excluded).
- Targets:
  - Core + WinForms: `net8.0-windows`
  - WinUI 3: `net8.0-windows10.0.19041.0`
- Windows App SDK 1.7, CommunityToolkit WinUI controls.
- Tests: xUnit (`PolicyPlusModTests`).
- Packaging: WinForms single-file publish (Release) via `dotnet publish`.
- SDK pinned by `global.json` (8.0.413) – do not drift.

## 3. Repository Layout (High Value Areas)
```
PolicyPlusMod.sln
PolicyPlus.Core/        Domain models & policy processing
PolicyPlus/             WinForms UI (baseline behavior)
PolicyPlus.WinUI3/      WinUI 3 UI (modern)
PolicyPlusModTests/     Tests (Core + limited UI logic)
Docs/                   Legacy architecture & terminology
.github/                (This file lives here)
version.bat             Legacy version stamping
```
Generated output: `artifacts/`, `obj/`, `bin/`.

## 4. Key Domain Concepts
- ADMX + ADML parsed into `AdmxBundle`.
- Policies → registry mutations via policy processing.
- `IPolicySource` abstracts persistence targets (POL file, registry, etc.).
- Policy state = Basic tri-state + element values.

## 5. Build & Run (cmd.exe)
```
dotnet --version
dotnet restore PolicyPlusMod.sln
dotnet build PolicyPlusMod.sln -c Debug
dotnet run --project PolicyPlus/PolicyPlus.csproj -c Debug
dotnet run --project PolicyPlus.WinUI3/PolicyPlus.WinUI3.csproj -c Debug
```
Publish WinForms (Release, single file):
```
dotnet publish PolicyPlus/PolicyPlus.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

## 6. Coding Guidelines
- Favor small, clear methods; keep hot paths lean.
- Code comments: intent/clarification only (English). NO change narration.
- Never embed version/diff history in comments (Git handles history).
- Add logic to `PolicyPlus.Core` first; WinForms acts as behavioral reference.
- WinUI 3 must compile; feature gaps must degrade gracefully (no throwing stubs).
- Warnings treated as errors; fix nullability properly.
- Only `IPolicySource` implementations touch the registry.
- Minimize public surface; prefer internal/private.
- Avoid new external dependencies unless essential (prefer BCL).
- Policy evaluation changes require/adjust focused tests.
- Cache repeated lookups; avoid repeated XML parsing; avoid heavy LINQ in tight loops.
- Respect `global.json` for deterministic builds.
- Comment style rules (strict):
  - Prohibited prefixes (case-insensitive): `// New:`, `// Added:`, `// Removed:`, `// Deleted:`, `// Modified:`, `// Changed:`, `// Update:`, `// Old:` (and similar diff-meta labels). Do not generate them.
  - Do not mark sections with “BEGIN/END CHANGE”.
  - Explain “why” when not obvious; omit obvious “what”.
  - Prefer short summary line; avoid paragraph unless essential.
  - No TODOs without actionable detail. Format: `// TODO: <concise actionable item>`.
  - Do not restate method name or parameter list.
  - Do not leave empty comment stubs.

## 7. Typical Safe Feature Flow
1. Define/extend domain model or logic in Core.
2. Create/update xUnit tests (happy path + one edge case).
3. Adapt WinForms UI using new Core API.
4. Provide minimal WinUI 3 equivalent (or safe no-op).
5. `dotnet test` until clean (no warnings).
6. Update docs only if public semantics changed.

## 8. Test Guidance
```
dotnet test PolicyPlusMod.sln -c Debug
```
Mock `IPolicySource` for persistence tests. For parser changes: minimal ADMX/ADML fixture + structural assertions.

## 9. Performance & Edge Cases
- Large ADMX sets: O(n) passes; pre-index by policy ID / registry key.
- Distinguish Disabled vs NotConfigured (Disabled may delete values).
- List elements: handle purge/name rules correctly.
- Enum evidence scoring changes require regression tests.

## 10. Patterns / Conventions
- Git version stamping separate; WinUI 3 uses `GenerateGitVersion` target.
- Designer (`*.Designer.cs`) files are generated; avoid manual edits except naming.
- No reintroduction of VB.
- Keep registry mutation representations small/immutable.

## 11. What NOT To Do
- Don’t remove WinForms feature before WinUI 3 has parity.
- Don’t implement UI logic in UI layer that belongs in Core.
- Don’t add heavy reflection/dynamic code in hot loops.
- Don’t block the UI thread with I/O.
- Don’t change policy semantics silently (tests first).

## 12. Rapid Orientation Checklist
Before:
- Skim relevant Core areas for existing patterns.
- Check if functionality already exists.
- Search solution for similar keywords.
- Add/plan test if evaluating/parsing behavior.

During:
- Explicit method names (`GetPolicyState`, `SetPolicyState`).
- Validate nulls early; fast return.
- Favor small immutable structs/classes for registry value representations.

After:
```
dotnet build PolicyPlusMod.sln -c Debug
dotnet test PolicyPlusMod.sln
```
Optionally also Release build.

## 13. Sample Feature Walkthrough (Illustrative)
Goal: Add helper `PolicyLookup.FindPolicy(bundle, uniqueId)`.
Steps:
- Add utility `PolicyPlus.Core/Utilities/PolicyLookup.cs`.
- Implement dictionary lookup via `AdmxBundle.Policies`.
- Add tests for found / missing ID.
- Build + test.
- Replace duplicative lookups in both UIs.

## 14. Communication Rules (Agent)
- Always respond (never silent) unless explicitly told to output nothing.
- Start first reply in a task with a one-line purposeful summary + next action.
- Provide concise reasoning only when it adds value; avoid filler.
- For code changes: supply full modified snippet (minimal diff context) inside fenced block; comments English only.
- Commands (cmd.exe) each on its own line inside fenced `cmd`/`bash`/`powershell` code blocks.
- On failure: show concise failing lines + top cause + concrete fix suggestion.
- When uncertain: read/search repo before guessing; say what was checked.
- Do not echo unchanged large sections of instructions back to user.
- Keep answers helpful but not terse to the point of ambiguity.
- Minimum response length guideline: enough to confirm understanding + action (generally ≥1 sentence + artifact if requested).

## 15. Quality Gate Before PR
- Build Debug & Release (WinForms + WinUI 3) succeed.
- All tests pass (including new ones).
- No new warnings.
- WinUI 3 launches (basic window).
- Feature available in both UIs or degrades gracefully.
- No prohibited comment patterns introduced.

## 16. Comment Style Examples (Enforced)
Good:
```csharp
// Caches policy index for O(1) lookup during evaluation.
```
Bad (history/diff noise):
```csharp
// New: Added cache dictionary
// Removed: old list iteration
```
Bad (redundant):
```csharp
// Method to get policy state
```
Good (non-obvious rationale):
```csharp
// Using ordinal ignore case because ADMX IDs are case-insensitive in source files.
```
Good TODO:
```csharp
// TODO: Support per-user override once profile service exposes hive path.
```

## 17. Prohibited Comment Detector (Heuristic Hints)
Avoid generating lines matching (regex, case-insensitive):
```
^\s*//\s*(new|added|removed|deleted|modified|changed|update|old)\b
```
If such a line would be emitted, replace with intent-focused phrasing or omit.

## 18. Fast Self-Check Before Output (Internal Agent Checklist)
- Did I include any prohibited prefixes? (If yes, rewrite.)
- Did I narrate diffs instead of showing code? (If yes, provide code.)
- Did I stay within user language (except code comments)? (If no, adjust.)
- Did I avoid being silent? (Ensure at least minimal summary present.)
- Did I add only intent/clarification comments?

(End)