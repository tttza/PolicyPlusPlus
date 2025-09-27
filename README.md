# Policy++

A tool to search, view, edit, and share Local Group Policy (Administrative Templates/ADMX).  
This repository is a fork of Fleex255’s [PolicyPlus](https://github.com/Fleex255/PolicyPlus) with additional features and a modernized UI.

[![build & release](https://github.com/tttza/PolicyPlusPlus/actions/workflows/publish.yml/badge.svg)](https://github.com/tttza/PolicyPlusPlus/actions/workflows/publish.yml)

> Note: This tool modifies the Windows Registry. Create a restore point or take a backup before use.

<img width="1227" height="734" alt="image" src="https://github.com/user-attachments/assets/85dc1ad5-e970-4813-bdc8-89c18588ef40" />

---

## What it can do

- Search and display
  - Fast search by Name / Description / Policy ID / Registry path
  - See technical details like target OS, supported on, and application target
- Edit and save
  - Toggle Enabled / Disabled / Not Configured
  - Edit elements: enum, numeric, string, multi-line text, lists
  - Queue changes in a temporary Pending list and save them in batch
- Share and migrate
  - Export / import .reg
  - One-click copy of Name / Policy ID / Registry path
  - Copy template hierarchy path as easy-to-read text
- Switch sources
  - Local GPO (User/Computer)
  - Per-user GPO (editable; application may be limited depending on Windows edition)
  - Standalone POL files, offline user hive, live registry
- ADMX ingestion
  - Auto-load from the default PolicyDefinitions folder
  - Load/switch from any chosen folder
- Display and language
  - Toggle columns (ID/Category/Applies to/Supported on, etc.)
  - Switch display language; optional secondary-language names

---

## Key features added in this Mod

- Bookmarks
  - Bookmark frequently used policies and filter by bookmark list
  - Manage bookmark lists (create, rename, delete)
- Quick Edit
  - Quickly edit and save multiple selected policies at once (pairs well with bookmarks)
- History
  - List previously saved changes and re-apply them when needed
- Enhanced search
  - Partial match on Policy ID and search by Registry path
- Copy tools
  - One-click copy for Name / ID / Registry path / Hierarchy path
- Secondary language display
  - Show labels in multiple languages on a single screen
- Dark mode / High-DPI support
  - Implemented with WinUI 3 for a modern UI

<img width="367" height="321" alt="image" src="https://github.com/user-attachments/assets/c9e33042-60d0-4b47-871b-fe7e1bc3ef75" />
<img width="1671" height="706" alt="image" src="https://github.com/user-attachments/assets/9699590e-966f-478e-8d84-c828b9b8ef90" />

---

## Differences from PolicyPlus (important)

- Not all features of PolicyPlus have been carried over yet
  - Features like “Acquire ADMX Files” are not included (please prepare/place ADMX files yourself)

Original (PolicyPlus by Fleex255): https://github.com/Fleex255/PolicyPlus

---

## Notes

- Behavior on Home Edition has not been sufficiently tested
- If ADMX/ADML are missing, obtain Microsoft’s administrative templates and place them in `PolicyDefinitions` and the corresponding language folder
- This tool directly manipulates the registry; mistakes can affect your system. Use with caution
- Much of the implementation was assisted by GitHub Copilot. While improvements are ongoing, it may contain critical errors that are easy to overlook. Please use with care.

---

## Download

Release builds are available on GitHub Releases:  
https://github.com/tttza/PolicyPlusPlus/releases

---

## For developers

- Main directories
  - `PolicyPlusCore`: ADMX/ADML processing, evaluation, and persistence abstractions
  - `PolicyPlusPlus`: Desktop UI
  - `PolicyPPElevationHost`: Separate process for admin-required operations
  - `PolicyPlusModTests` / `PolicyPlusPlus.Tests.UI`: Unit / UI tests
- Build and run (cmd.exe)
  ```cmd
  dotnet restore PolicyPlusMod.sln
  dotnet build PolicyPlusMod.sln -c Debug-Unpackaged
  dotnet run --project PolicyPlusPlus\PolicyPlusPlus.csproj -c Debug-Unpackaged
  ```
- Tests
  ```cmd
  dotnet test PolicyPlusModTests\PolicyPlusModTests.csproj -c Debug-Unpackaged
  dotnet test PolicyPlusPlus.Tests.UI\PolicyPlus.Tests.UI.csproj -c Debug-Unpackaged -- --stop-on-fail on
  ```

Please report issues and improvement proposals via Issues / PR.
