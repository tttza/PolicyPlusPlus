using System.Threading.Tasks;

namespace PolicyPlus.WinUI3.Services
{
    public interface IElevationService
    {
        Task<(bool ok, string? error)> WriteLocalGpoBytesAsync(string? machinePolBase64, string? userPolBase64, bool triggerRefresh = true);
        Task<(bool ok, string? error)> OpenRegeditAtAsync(string hive, string subKey);
    }

    internal sealed class ElevationServiceAdapter : IElevationService
    {
        public Task<(bool ok, string? error)> WriteLocalGpoBytesAsync(string? machinePolBase64, string? userPolBase64, bool triggerRefresh = true)
            => ElevationService.Instance.WriteLocalGpoBytesAsync(machinePolBase64, userPolBase64, triggerRefresh);

        public Task<(bool ok, string? error)> OpenRegeditAtAsync(string hive, string subKey)
            => ElevationService.Instance.OpenRegeditAtAsync(hive, subKey);
    }
}
