using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace SteamGameLoader;

/// <summary>
/// Minimal process introspection used to detect when a specific, already-known
/// executable (resolved from a game's own install manifest) has started and
/// shown a window. Unlike guessing which of several processes is "the game",
/// this only ever looks for one exact, known exe path.
/// </summary>
internal static class ProcessLookup
{
    private const int ProcessQueryLimitedInformation = 0x1000;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName(IntPtr hProcess, int dwFlags, StringBuilder lpExeName, ref int lpdwSize);

    public static int? FindPidForExecutable(string exePath)
    {
        foreach (Process process in Process.GetProcesses())
        {
            using (process)
            {
                string? path = TryGetExecutablePath(process.Id);
                if (path is not null && string.Equals(path, exePath, StringComparison.OrdinalIgnoreCase))
                    return process.Id;
            }
        }

        return null;
    }

    public static bool HasVisibleWindow(int pid)
    {
        try
        {
            using Process process = Process.GetProcessById(pid);
            return process.MainWindowHandle != IntPtr.Zero;
        }
        catch (ArgumentException)
        {
            return false; // Already exited.
        }
    }

    public static bool IsRunning(int pid)
    {
        try
        {
            using Process process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static string? TryGetExecutablePath(int pid)
    {
        IntPtr handle = OpenProcess(ProcessQueryLimitedInformation, false, pid);
        if (handle == IntPtr.Zero)
            return null;

        try
        {
            var sb = new StringBuilder(1024);
            int size = sb.Capacity;
            return QueryFullProcessImageName(handle, 0, sb, ref size) ? sb.ToString() : null;
        }
        finally
        {
            CloseHandle(handle);
        }
    }
}
