using System;
using System.Runtime.InteropServices;

namespace PolicyPlusCore.Helpers
{
    class PInvoke
    {
        [DllImport("user32.dll")]
        public static extern bool ShowScrollBar(nint Handle, int Scrollbar, bool Show);
        [DllImport("userenv.dll")]
        public static extern bool RefreshPolicyEx(bool IsMachine, uint Options);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        public static extern int RegLoadKeyW(nint HiveKey, string Subkey, string File);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        public static extern int RegUnLoadKeyW(nint HiveKey, string Subkey);
        [DllImport("kernel32.dll")]
        public static extern nint GetCurrentProcess();
        [DllImport("advapi32.dll")]
        public static extern bool OpenProcessToken(nint Process, uint Access, ref nint TokenHandle);
        [DllImport("advapi32.dll")]
        public static extern bool AdjustTokenPrivileges(nint Token, bool DisableAll, ref PInvokeTokenPrivileges NewState, uint BufferLength, nint Null, ref uint ReturnLength);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    public static extern bool LookupPrivilegeValueW(string? SystemName, string Name, ref PInvokeLuid LUID);
        [DllImport("kernel32.dll")]
        public static extern bool CloseHandle(nint Handle);
        [DllImport("kernel32.dll")]
        public static extern bool GetProductInfo(int MajorVersion, int MinorVersion, int SPMajor, int SPMinor, ref int EditionCode);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool SendNotifyMessageW(nint Handle, int Message, nuint WParam, nint LParam);
    }

    [StructLayout(LayoutKind.Sequential)]
    struct PInvokeTokenPrivileges
    {
        public uint PrivilegeCount;
        public PInvokeLuid LUID;
        public uint Attributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct PInvokeLuid
    {
        public uint LowPart;
        public int HighPart;
    }
}
