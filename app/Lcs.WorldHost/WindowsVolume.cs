using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

namespace Lcs.WorldHost;

[SupportedOSPlatform("windows")]
internal static class WindowsVolume
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern SafeFileHandle CreateFile(string name, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern uint GetFinalPathNameByHandle(SafeFileHandle handle, StringBuilder path, uint size, uint flags);

    internal static bool IsPhysicalRoot(string path)
    {
        // Metadata-only open. A textual drive root (including SUBST) is insufficient evidence.
        using var handle = CreateFile(path, 0, 7, IntPtr.Zero, 3, 0x02000000, IntPtr.Zero);
        if (handle.IsInvalid) return false;
        var result = new StringBuilder(512);
        uint count = GetFinalPathNameByHandle(handle, result, (uint)result.Capacity, 1); // VOLUME_NAME_GUID
        return count is > 0 and < 512 && Regex.IsMatch(result.ToString(),
            @"^\\\\\?\\Volume\{[0-9a-fA-F-]{36}\}\\\z", RegexOptions.CultureInvariant);
    }
}
