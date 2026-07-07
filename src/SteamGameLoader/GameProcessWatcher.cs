using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace SteamGameLoader;

/// <summary>
/// Steam's "Running" flag flips on the moment it hands off to whatever it was
/// told to launch - for games with a separate launcher (e.g. Paradox
/// Launcher), that's the launcher, not the real game. This watches for a
/// second, different process to appear under the game's own install folder,
/// so the popup can be shown again for that in-between gap too.
/// </summary>
internal static class GameProcessWatcher
{
    private const int ProcessQueryLimitedInformation = 0x1000;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageName(IntPtr hProcess, int dwFlags, StringBuilder lpExeName, ref int lpdwSize);

    /// <summary>Every running process id whose executable currently lives under installDir.</summary>
    public static HashSet<int> SnapshotPidsUnder(string installDir)
    {
        var result = new HashSet<int>();
        foreach (Process process in Process.GetProcesses())
        {
            using (process)
            {
                if (IsUnder(process.Id, installDir))
                    result.Add(process.Id);
            }
        }

        return result;
    }

    /// <summary>The first process id under installDir that isn't already in knownPids, if any.</summary>
    public static int? FindNewPidUnder(string installDir, HashSet<int> knownPids)
    {
        foreach (Process process in Process.GetProcesses())
        {
            using (process)
            {
                if (!knownPids.Contains(process.Id) && IsUnder(process.Id, installDir))
                    return process.Id;
            }
        }

        return null;
    }

    /// <summary>Heuristic: does this process's exe name suggest it's a launcher rather than the game itself?</summary>
    public static bool LooksLikeLauncher(int pid)
    {
        string? path = TryGetExecutablePath(pid);
        return path is not null &&
               Path.GetFileNameWithoutExtension(path).Contains("launcher", StringComparison.OrdinalIgnoreCase);
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

    private static bool IsUnder(int pid, string installDir)
    {
        string? path = TryGetExecutablePath(pid);
        return path is not null && path.StartsWith(installDir, StringComparison.OrdinalIgnoreCase);
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
