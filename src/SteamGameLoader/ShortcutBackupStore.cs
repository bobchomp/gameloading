using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SteamGameLoader;

/// <summary>
/// Remembers each shortcut's original target/arguments/icon before we rewrite it,
/// so "Restore Original Shortcuts" can put things back exactly as they were.
/// </summary>
internal sealed class ShortcutBackupStore
{
    public sealed class BackupEntry
    {
        public string TargetPath { get; set; } = "";
        public string Arguments { get; set; } = "";
        public string IconLocation { get; set; } = "";
        public int IconIndex { get; set; }
        public string WorkingDirectory { get; set; } = "";
    }

    private readonly string _path;
    private readonly Dictionary<string, BackupEntry> _entries;

    public ShortcutBackupStore()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SteamGameLoader");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "shortcut-backups.json");
        _entries = Load();
    }

    private Dictionary<string, BackupEntry> Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                string json = File.ReadAllText(_path);
                var data = JsonSerializer.Deserialize<Dictionary<string, BackupEntry>>(json);
                if (data != null)
                    return data;
            }
        }
        catch
        {
            // Corrupt or unreadable backup file - start fresh rather than fail setup.
        }

        return new Dictionary<string, BackupEntry>();
    }

    private void Save()
    {
        string json = JsonSerializer.Serialize(_entries, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_path, json);
    }

    public bool Has(string lnkPath) => _entries.ContainsKey(lnkPath);

    public BackupEntry? Get(string lnkPath) => _entries.TryGetValue(lnkPath, out var entry) ? entry : null;

    public void Record(string lnkPath, BackupEntry entry)
    {
        _entries[lnkPath] = entry;
        Save();
    }

    public void Remove(string lnkPath)
    {
        if (_entries.Remove(lnkPath))
            Save();
    }
}
