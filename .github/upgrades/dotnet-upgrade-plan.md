# .NET 8.0 Upgrade Plan

## Execution Steps

Execute steps below sequentially one by one in the order they are listed.

1. Validate that an .NET 8.0 SDK required for this upgrade is installed on the machine and if not, help to get it installed.
2. Ensure that the SDK version specified in global.json files is compatible with the .NET 8.0 upgrade.
3. Upgrade PolicyPlus\PolicyPlus.csproj
4. Upgrade PolicyPlusModTests\PolicyPlusModTests.csproj

## Settings

This section contains settings and data used by execution steps.

### Excluded projects

Table below contains projects that do belong to the dependency graph for selected projects and should not be included in the upgrade.

| Project name                                   | Description                 |
|:-----------------------------------------------|:---------------------------:|
| (none)                                         |                             |

### Aggregate NuGet packages modifications across all projects

NuGet packages used across all selected projects or their dependencies that need version update in projects that reference them.

| Package Name                          | Current Version | New Version | Description                                            |
|:--------------------------------------|:---------------:|:-----------:|:-------------------------------------------------------|
| Microsoft.Bcl.HashCode                | 1.1.1           | 6.0.0       | Recommended for .NET 8.0                               |
| System.Buffers                        | 4.5.1           | (remove)    | Functionality now provided by the .NET 8.0 framework   |
| System.Collections.Immutable          | 9.0.8           | 8.0.0       | Recommended version per analysis (downgrade accepted)  |
| System.Memory                         | 4.5.5           | (remove)    | Functionality now provided by the .NET 8.0 framework   |
| System.Numerics.Vectors               | 4.5.0           | (remove)    | Functionality now provided by the .NET 8.0 framework   |
| System.Reflection.Metadata            | 9.0.8           | 8.0.1       | Recommended version per analysis (downgrade accepted)  |
| System.Resources.Extensions           | 9.0.8           | 8.0.0       | Recommended version per analysis (downgrade accepted)  |
| System.Runtime.CompilerServices.Unsafe| 6.0.0           | 6.1.2       | Recommended update for .NET 8.0                        |
| System.ValueTuple                     | 4.5.0           | (remove)    | Functionality now provided by the .NET 8.0 framework   |

### Project upgrade details
This section contains details about each project upgrade and modifications that need to be done in the project.

#### PolicyPlus\PolicyPlus.csproj modifications

Project properties changes:
  - Convert project file to SDK style
  - Target framework should be changed from `.NETFramework,Version=v4.8` to `net8.0-windows`

NuGet packages changes:
  - Microsoft.Bcl.HashCode should be updated from `1.1.1` to `6.0.0`
  - System.Collections.Immutable should be updated from `9.0.8` to `8.0.0` (*downgrade per analysis*)
  - System.Reflection.Metadata should be updated from `9.0.8` to `8.0.1` (*downgrade per analysis*)
  - System.Resources.Extensions should be updated from `9.0.8` to `8.0.0` (*downgrade per analysis*)
  - System.Runtime.CompilerServices.Unsafe should be updated from `6.0.0` to `6.1.2`
  - System.Buffers should be removed (framework provided in .NET 8.0)
  - System.Memory should be removed (framework provided in .NET 8.0)
  - System.Numerics.Vectors should be removed (framework provided in .NET 8.0)
  - System.ValueTuple should be removed (framework provided in .NET 8.0)

Other changes:
  - Review code for any deprecated .NET Framework 4.8 APIs and replace with .NET 8 equivalents if build errors arise.

#### PolicyPlusModTests\PolicyPlusModTests.csproj modifications

Project properties changes:
  - Target framework should be changed from `net48` to `net8.0-windows`

NuGet packages changes:
  - (No package version analysis changes required beyond transitive impacts from main project.)

Other changes:
  - Adjust test project references if any removed packages were explicitly referenced.
