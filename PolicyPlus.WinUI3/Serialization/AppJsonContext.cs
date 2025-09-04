using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using PolicyPlus.WinUI3.Services;

namespace PolicyPlus.WinUI3.Serialization
{
    // DTOs for elevation IPC
    public sealed class HostRequestWriteLocalGpo
    {
        [JsonPropertyName("op")] public string Op { get; set; } = "write-local-gpo";
        [JsonPropertyName("auth")] public string? Auth { get; set; }
        [JsonPropertyName("machinePol")] public string? MachinePol { get; set; }
        [JsonPropertyName("userPol")] public string? UserPol { get; set; }
        [JsonPropertyName("machineBytes")] public string? MachineBytes { get; set; }
        [JsonPropertyName("userBytes")] public string? UserBytes { get; set; }
        [JsonPropertyName("refresh")] public bool Refresh { get; set; }
    }

    public sealed class HostRequestOpenRegedit
    {
        [JsonPropertyName("op")] public string Op { get; set; } = "open-regedit";
        [JsonPropertyName("auth")] public string? Auth { get; set; }
        [JsonPropertyName("hive")] public string Hive { get; set; } = string.Empty;
        [JsonPropertyName("subKey")] public string SubKey { get; set; } = string.Empty;
    }

    public sealed class HostRequestShutdown
    {
        [JsonPropertyName("op")] public string Op { get; set; } = "shutdown";
        [JsonPropertyName("auth")] public string? Auth { get; set; }
    }

    public sealed class HostResponse
    {
        [JsonPropertyName("ok")] public bool Ok { get; set; }
        [JsonPropertyName("error")] public string? Error { get; set; }
    }

    [JsonSourceGenerationOptions(WriteIndented = false, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(AppSettings))]
    [JsonSerializable(typeof(ColumnsOptions))]
    [JsonSerializable(typeof(ColumnState))]
    [JsonSerializable(typeof(List<ColumnState>))]
    [JsonSerializable(typeof(SearchOptions))]
    [JsonSerializable(typeof(List<HistoryRecord>))]
    [JsonSerializable(typeof(HostRequestWriteLocalGpo))]
    [JsonSerializable(typeof(HostRequestOpenRegedit))]
    [JsonSerializable(typeof(HostRequestShutdown))]
    [JsonSerializable(typeof(HostResponse))]
    internal partial class AppJsonContext : System.Text.Json.Serialization.JsonSerializerContext { }
}
