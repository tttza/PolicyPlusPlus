using System;
using System.Runtime.InteropServices;

namespace PolicyPlus
{
    public class Privilege
    {
        public static void EnablePrivilege(string Name)
        {
            var luid = default(PInvokeLuid);
            PInvokeTokenPrivileges priv;
            IntPtr thisProcToken = default(IntPtr);
            PInvoke.OpenProcessToken(PInvoke.GetCurrentProcess(), 0x28U, ref thisProcToken);
            string? argSystemName = null;
            PInvoke.LookupPrivilegeValueW(argSystemName, Name, ref luid);
            priv.Attributes = 2U;
            priv.PrivilegeCount = 1U;
            priv.LUID = luid;
            uint argReturnLength = 0U;
            PInvoke.AdjustTokenPrivileges(thisProcToken, false, ref priv, (uint)Marshal.SizeOf(priv), IntPtr.Zero, ref argReturnLength);
            PInvoke.CloseHandle(thisProcToken);
        }
    }
}
