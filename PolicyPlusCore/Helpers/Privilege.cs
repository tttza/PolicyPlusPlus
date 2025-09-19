using System.Runtime.InteropServices;

namespace PolicyPlusCore.Helpers
{
    public class Privilege
    {
        public static void EnablePrivilege(string Name)
        {
            var luid = default(PInvokeLuid);
            PInvokeTokenPrivileges priv;
            nint thisProcToken = default;
            PInvoke.OpenProcessToken(PInvoke.GetCurrentProcess(), 0x28U, ref thisProcToken);
            string? argSystemName = null;
            PInvoke.LookupPrivilegeValueW(argSystemName, Name, ref luid);
            priv.Attributes = 2U;
            priv.PrivilegeCount = 1U;
            priv.LUID = luid;
            uint argReturnLength = 0U;
            PInvoke.AdjustTokenPrivileges(
                thisProcToken,
                false,
                ref priv,
                (uint)Marshal.SizeOf(priv),
                nint.Zero,
                ref argReturnLength
            );
            PInvoke.CloseHandle(thisProcToken);
        }
    }
}
