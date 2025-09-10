using System.Threading.Tasks;

namespace PolicyPlusPlus.Services
{
    public enum ElevationErrorCode
    {
        None = 0,
        NotConnected,
        Timeout,
        Unauthorized,
        StartFailed,
        ConnectFailed,
        HostExited,
        Canceled,
        ProtocolError,
        IoError,
        Unknown
    }

    public readonly struct ElevationResult
    {
        public bool Ok { get; }
        public ElevationErrorCode Code { get; }
        public string? Error { get; }
        public ElevationResult(bool ok, ElevationErrorCode code, string? error)
        { Ok = ok; Code = code; Error = error; }
        public static ElevationResult Success => new(true, ElevationErrorCode.None, null);
        public static ElevationResult FromError(ElevationErrorCode code, string? error) => new(false, code, error);
    }

    public interface IElevationService
    {
        Task<ElevationResult> WriteLocalGpoBytesAsync(string? machinePolBase64, string? userPolBase64, bool triggerRefresh = true);
        Task<ElevationResult> OpenRegeditAtAsync(string hive, string subKey);
    }

    internal sealed class ElevationServiceAdapter : IElevationService
    {
        public Task<ElevationResult> WriteLocalGpoBytesAsync(string? machinePolBase64, string? userPolBase64, bool triggerRefresh = true)
            => PolicyPlusPlus.Services.ElevationService.Instance.WriteLocalGpoBytesAsync(machinePolBase64, userPolBase64, triggerRefresh);

        public Task<ElevationResult> OpenRegeditAtAsync(string hive, string subKey)
            => PolicyPlusPlus.Services.ElevationService.Instance.OpenRegeditAtAsync(hive, subKey);
    }
}
