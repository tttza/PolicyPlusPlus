---
mode: agent
---
# Local Velopack Packaging Instructions

This document captures a reusable, detailed instruction prompt for generating local Velopack release artifacts of the WinUI 3 application **Policy++** (repository: <https://github.com/tttza/PolicyPlusPlus>) without uploading to GitHub. It mirrors the logic defined in the GitHub Actions workflow: `.github/workflows/publish.yml`.

> Target example version below: `2.0.0-alpha.1` (note the dot between `alpha` and `1`). Adjust the version where needed.

---
## Objective
Create local (not published) Velopack release packages for both `win-x64` and `win-arm64` using the `Release-Unpackaged` configuration, producing:

- Self-contained publish outputs under `publish/win-x64` and `publish/win-arm64`
- Velopack artifacts under `Releases/` (Setup EXE, Portable ZIP, `.nupkg`, `RELEASES-win-<arch>`, optional `releases.win-<arch>.json`)

No source changes, no uploads, no tag creation unless explicitly performed afterwards.

---
## Requirements Summary
1. Use configuration: `Release-Unpackaged`
2. Build self-contained: `--self-contained true`
3. Override version explicitly: `/p:GitVersionOverride=2.0.0-alpha.1`
4. Target runtimes: `win-x64`, `win-arm64`
5. Publish output directories:
   - `publish/win-x64`
   - `publish/win-arm64`
6. Velopack (vpk) packaging with:
   - `--packId dev.tttza.PolicyPlusPlus`
   - `--packTitle "Policy++"`
   - `--packAuthors "tttza.dev"`
   - `--packVersion 2.0.0-alpha.1`
   - `--runtime <runtime>`
   - `--packDir publish/<runtime>`
   - `--mainExe PolicyPlusPlus.exe`
   - `--icon PolicyPlusPlus/Assets/AppIcon.ico`
   - `-o Releases` (output directory)
   - `-c <runtime>` (channel name per architecture)
7. Do not modify `global.json` or project files.
8. Do not upload or create a GitHub Release in this procedure.
9. Provide post-build artifact listing and verification steps.
10. Suggest optional next actions (signing, tagging, simulated update feed).

---
## Command Sequence (cmd.exe)
Run each line separately in a Developer Command Prompt or standard `cmd.exe` at the repository root.

```cmd
dotnet clean PolicyPlusPlus.sln -c Release-Unpackaged
dotnet restore PolicyPlusPlus.sln
dotnet publish PolicyPlusPlus\PolicyPlusPlus.csproj -c Release-Unpackaged -r win-x64  --self-contained true -o publish\win-x64  /p:GitVersionOverride=2.0.0-alpha.1
dotnet publish PolicyPlusPlus\PolicyPlusPlus.csproj -c Release-Unpackaged -r win-arm64 --self-contained true -o publish\win-arm64 /p:GitVersionOverride=2.0.0-alpha.1
dotnet tool install -g vpk
vpk download github --repoUrl https://github.com/tttza/PolicyPlusPlus || echo skip
vpk pack --packId dev.tttza.PolicyPlusPlus --packTitle "Policy++" --packAuthors "tttza.dev" --packVersion 2.0.0-alpha.1 --runtime win-x64  --packDir publish\win-x64  --mainExe PolicyPlusPlus.exe --icon PolicyPlusPlus/Assets/AppIcon.ico -o Releases -c win-x64
vpk pack --packId dev.tttza.PolicyPlusPlus --packTitle "Policy++" --packAuthors "tttza.dev" --packVersion 2.0.0-alpha.1 --runtime win-arm64 --packDir publish\win-arm64 --mainExe PolicyPlusPlus.exe --icon PolicyPlusPlus/Assets/AppIcon.ico -o Releases -c win-arm64
```

### (Optional) PowerShell Variant
```powershell
dotnet clean .\PolicyPlusPlus.sln -c Release-Unpackaged
dotnet restore .\PolicyPlusPlus.sln
dotnet publish .\PolicyPlusPlus\PolicyPlusPlus.csproj -c Release-Unpackaged -r win-x64  --self-contained true -o .\publish\win-x64  /p:GitVersionOverride=2.0.0-alpha.1
dotnet publish .\PolicyPlusPlus\PolicyPlusPlus.csproj -c Release-Unpackaged -r win-arm64 --self-contained true -o .\publish\win-arm64 /p:GitVersionOverride=2.0.0-alpha.1
dotnet tool install -g vpk
vpk download github --repoUrl https://github.com/tttza/PolicyPlusPlus || Write-Host "skip"
vpk pack --packId dev.tttza.PolicyPlusPlus --packTitle "Policy++" --packAuthors "tttza.dev" --packVersion 2.0.0-alpha.1 --runtime win-x64  --packDir .\publish\win-x64  --mainExe PolicyPlusPlus.exe --icon PolicyPlusPlus/Assets/AppIcon.ico -o .\Releases -c win-x64
vpk pack --packId dev.tttza.PolicyPlusPlus --packTitle "Policy++" --packAuthors "tttza.dev" --packVersion 2.0.0-alpha.1 --runtime win-arm64 --packDir .\publish\win-arm64 --mainExe PolicyPlusPlus.exe --icon PolicyPlusPlus/Assets/AppIcon.ico -o .\Releases -c win-arm64
```
