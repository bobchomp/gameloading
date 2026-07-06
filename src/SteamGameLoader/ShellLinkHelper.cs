using System;
using System.Runtime.InteropServices;
using System.Text;

namespace SteamGameLoader;

/// <summary>
/// Reads and writes .lnk shortcut files via the classic IShellLinkW/IPersistFile
/// COM interfaces. Implemented by hand (no Windows Script Host reference) so the
/// build has no extra COM interop dependency to register on the CI machine.
/// </summary>
internal static class ShellLinkHelper
{
    public sealed class ShortcutInfo
    {
        public string TargetPath { get; init; } = "";
        public string Arguments { get; init; } = "";
        public string IconLocation { get; init; } = "";
        public int IconIndex { get; init; }
        public string WorkingDirectory { get; init; } = "";
    }

    public static ShortcutInfo Read(string lnkPath)
    {
        var link = (IShellLinkW)new ShellLinkCoClass();
        ((IPersistFile)link).Load(lnkPath, 0 /* STGM_READ */);

        var targetSb = new StringBuilder(260);
        link.GetPath(targetSb, targetSb.Capacity, IntPtr.Zero, 0);

        var argsSb = new StringBuilder(1024);
        link.GetArguments(argsSb, argsSb.Capacity);

        var iconSb = new StringBuilder(260);
        link.GetIconLocation(iconSb, iconSb.Capacity, out int iconIndex);

        var workDirSb = new StringBuilder(260);
        link.GetWorkingDirectory(workDirSb, workDirSb.Capacity);

        return new ShortcutInfo
        {
            TargetPath = targetSb.ToString(),
            Arguments = argsSb.ToString(),
            IconLocation = iconSb.ToString(),
            IconIndex = iconIndex,
            WorkingDirectory = workDirSb.ToString(),
        };
    }

    public static void Write(string lnkPath, string targetPath, string arguments, string iconLocation, int iconIndex, string workingDirectory)
    {
        var link = (IShellLinkW)new ShellLinkCoClass();

        link.SetPath(targetPath);
        link.SetArguments(arguments ?? "");

        if (!string.IsNullOrEmpty(iconLocation))
            link.SetIconLocation(iconLocation, iconIndex);

        if (!string.IsNullOrEmpty(workingDirectory))
            link.SetWorkingDirectory(workingDirectory);

        ((IPersistFile)link).Save(lnkPath, true);
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLinkCoClass
    {
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0000010b-0000-0000-C000-000000000046")]
    private interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        [PreserveSig] int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
    }
}
