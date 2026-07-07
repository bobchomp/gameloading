using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace SteamGameLoader;

/// <summary>
/// One-time setup tool: scans the desktop for existing Steam game shortcuts and
/// rewrites the checked ones to launch through the loading popup instead, or
/// restores previously-converted ones back to their original target.
/// </summary>
internal sealed class SetupForm : Form
{
    private readonly ShortcutBackupStore _backupStore = new();
    private readonly CheckedListBox _list;

    public SetupForm()
    {
        Text = "Steam Game Loading Popup - Setup";
        ClientSize = new System.Drawing.Size(560, 420);
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new System.Drawing.Size(480, 320);

        var instructions = new Label
        {
            Text = "These are the Steam shortcuts found on your Desktop. Check the ones you want " +
                   "to show a \"Game Loading\" popup for, then click Convert. Already-converted " +
                   "shortcuts are checked to be restored instead.",
            Dock = DockStyle.Top,
            Height = 60,
            Padding = new Padding(10, 10, 10, 0),
        };

        _list = new CheckedListBox
        {
            Dock = DockStyle.Fill,
            CheckOnClick = true,
            IntegralHeight = false,
        };

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 46,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(10),
        };

        var rescanButton = new Button { Text = "Rescan", AutoSize = true };
        rescanButton.Click += (_, _) => RescanShortcuts();

        var restoreButton = new Button { Text = "Restore Checked", AutoSize = true };
        restoreButton.Click += (_, _) => RestoreChecked();

        var convertButton = new Button { Text = "Convert Checked", AutoSize = true };
        convertButton.Click += (_, _) => ConvertChecked();

        buttonPanel.Controls.Add(rescanButton);
        buttonPanel.Controls.Add(restoreButton);
        buttonPanel.Controls.Add(convertButton);

        Controls.Add(_list);
        Controls.Add(buttonPanel);
        Controls.Add(instructions);

        Load += (_, _) => RescanShortcuts();
    }

    private void RescanShortcuts()
    {
        _list.Items.Clear();
        var shortcuts = ShortcutScanner.ScanDesktopShortcuts();

        if (shortcuts.Count == 0)
        {
            MessageBox.Show(this,
                "No Steam game shortcuts were found on your Desktop.\n\n" +
                "In Steam, right-click a game -> Manage -> Add desktop shortcut, then click Rescan here.",
                "No shortcuts found", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        foreach (var shortcut in shortcuts.OrderBy(s => s.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            // Default: check new shortcuts for conversion, check converted ones for restore.
            _list.Items.Add(shortcut, true);
        }
    }

    private void ConvertChecked()
    {
        int count = 0;
        foreach (DetectedShortcut shortcut in CheckedItemsOf(alreadyConverted: false))
        {
            try
            {
                ConvertShortcut(shortcut);
                count++;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to convert \"{shortcut.DisplayName}\": {ex.Message}",
                    "Conversion error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        ShellNotify.RefreshDesktopIcons();
        RescanShortcuts();
        MessageBox.Show(this, $"Converted {count} shortcut(s) to use the Game Loading popup.",
            "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void RestoreChecked()
    {
        int count = 0;
        foreach (DetectedShortcut shortcut in CheckedItemsOf(alreadyConverted: true))
        {
            try
            {
                RestoreShortcut(shortcut);
                count++;
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Failed to restore \"{shortcut.DisplayName}\": {ex.Message}",
                    "Restore error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        ShellNotify.RefreshDesktopIcons();
        RescanShortcuts();
        MessageBox.Show(this, $"Restored {count} shortcut(s) to their original target.",
            "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private System.Collections.Generic.IEnumerable<DetectedShortcut> CheckedItemsOf(bool alreadyConverted)
    {
        for (int i = 0; i < _list.Items.Count; i++)
        {
            if (!_list.GetItemChecked(i))
                continue;

            if (_list.Items[i] is DetectedShortcut shortcut && shortcut.AlreadyConverted == alreadyConverted)
                yield return shortcut;
        }
    }

    private void ConvertShortcut(DetectedShortcut shortcut)
    {
        string key = ShortcutBackupStore.KeyFor(shortcut.ShortcutPath);

        if (!_backupStore.Has(key))
        {
            if (shortcut.IsUrlShortcut)
            {
                _backupStore.Record(key, new ShortcutBackupStore.BackupEntry
                {
                    WasUrlShortcut = true,
                    UrlRawContent = File.ReadAllText(shortcut.ShortcutPath),
                });
            }
            else
            {
                ShellLinkHelper.ShortcutInfo original = ShellLinkHelper.Read(shortcut.ShortcutPath);
                _backupStore.Record(key, new ShortcutBackupStore.BackupEntry
                {
                    WasUrlShortcut = false,
                    TargetPath = original.TargetPath,
                    Arguments = original.Arguments,
                    IconLocation = original.IconLocation,
                    IconIndex = original.IconIndex,
                    WorkingDirectory = original.WorkingDirectory,
                });
            }
        }

        ShortcutBackupStore.BackupEntry backup = _backupStore.Get(key)!;

        string iconLocation;
        int iconIndex;
        if (shortcut.IsUrlShortcut && !string.IsNullOrEmpty(shortcut.UrlIconFile))
        {
            iconLocation = shortcut.UrlIconFile;
            iconIndex = shortcut.UrlIconIndex;
        }
        else if (!shortcut.IsUrlShortcut && !string.IsNullOrEmpty(backup.IconLocation))
        {
            iconLocation = backup.IconLocation;
            iconIndex = backup.IconIndex;
        }
        else
        {
            iconLocation = SteamHelper.GetSteamInstallPath() is string steamPath
                ? Path.Combine(steamPath, "steam.exe")
                : Application.ExecutablePath;
            iconIndex = 0;
        }

        string lnkPath = shortcut.IsUrlShortcut
            ? Path.ChangeExtension(shortcut.ShortcutPath, ".lnk")
            : shortcut.ShortcutPath;

        // Delete the old file *before* creating the new one: if both briefly
        // exist together, Explorer's live Desktop view can end up caching a
        // spurious "Name (2)" ghost icon for whichever one it detected second.
        if (shortcut.IsUrlShortcut && !string.Equals(lnkPath, shortcut.ShortcutPath, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(shortcut.ShortcutPath);
        }

        ShellLinkHelper.Write(
            lnkPath,
            targetPath: Application.ExecutablePath,
            arguments: shortcut.AppId.ToString(),
            iconLocation: iconLocation,
            iconIndex: iconIndex,
            workingDirectory: Path.GetDirectoryName(Application.ExecutablePath) ?? "");
    }

    private void RestoreShortcut(DetectedShortcut shortcut)
    {
        string key = ShortcutBackupStore.KeyFor(shortcut.ShortcutPath);
        ShortcutBackupStore.BackupEntry? backup = _backupStore.Get(key);
        if (backup is null)
            return;

        if (backup.WasUrlShortcut)
        {
            string urlPath = Path.ChangeExtension(shortcut.ShortcutPath, ".url");

            if (!string.Equals(urlPath, shortcut.ShortcutPath, StringComparison.OrdinalIgnoreCase) &&
                File.Exists(shortcut.ShortcutPath))
            {
                File.Delete(shortcut.ShortcutPath);
            }

            File.WriteAllText(urlPath, backup.UrlRawContent ?? "");
        }
        else
        {
            ShellLinkHelper.Write(
                shortcut.ShortcutPath, backup.TargetPath, backup.Arguments, backup.IconLocation, backup.IconIndex, backup.WorkingDirectory);
        }

        _backupStore.Remove(key);
    }
}
