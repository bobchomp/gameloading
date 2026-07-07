using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace SteamGameLoader;

/// <summary>
/// The "Game Loading..." popup. Launches the Steam game, then polls Steam's own
/// "Running" registry flag every 500ms and closes itself once the game has
/// actually started, with a safety-net timeout and a manual Cancel button.
///
/// Some games (e.g. Cities: Skylines II via the Paradox Launcher) hand off to
/// a separate launcher process first, which is what Steam's "Running" flag
/// actually reacts to - the real game only starts afterwards. Once the flag
/// flips on, this also watches for a second, different process to appear
/// under the game's own install folder, and keeps the popup up until that
/// process (or the original one, if there never was a separate launcher)
/// shows a visible window.
/// </summary>
internal sealed class LoadingForm : Form
{
    private static readonly TimeSpan SafetyTimeout = TimeSpan.FromSeconds(90);

    private enum WatchState { WaitingForHandoff, WaitingForRealGame }

    private readonly uint _appId;
    private readonly Timer _pollTimer;
    private readonly Label _titleLabel;

    private WatchState _state = WatchState.WaitingForHandoff;
    private DateTime _phaseStartedAt = DateTime.UtcNow;
    private string? _installDir;
    private HashSet<int> _initialPids = new();
    private int? _gamePid;

    private Point _dragStart;
    private bool _dragging;

    public LoadingForm(uint appId)
    {
        _appId = appId;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(340, 160);
        BackColor = Color.FromArgb(32, 32, 36);
        TopMost = true;
        ShowInTaskbar = true;
        KeyPreview = true;
        Text = "Game Loading";
        if (AppIcon.TryLoad() is Icon appIcon)
            Icon = appIcon;

        var spinner = new SpinnerControl { Size = new Size(48, 48), Location = new Point(24, 28) };

        string gameName = SteamHelper.TryGetGameName(appId) ?? $"Steam game (AppID {appId})";

        _titleLabel = new Label
        {
            Text = "Launching…",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 13F, FontStyle.Bold),
            Location = new Point(88, 26),
            AutoSize = true,
        };

        var subtitleLabel = new Label
        {
            Text = gameName,
            ForeColor = Color.FromArgb(190, 190, 190),
            Font = new Font("Segoe UI", 9.5F),
            Location = new Point(88, 56),
            AutoSize = false,
            Size = new Size(228, 40),
        };

        var cancelButton = new Button
        {
            Text = "Cancel",
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            BackColor = Color.FromArgb(60, 60, 66),
            Size = new Size(90, 30),
            Location = new Point(340 - 20 - 90, 160 - 20 - 30),
            Cursor = Cursors.Hand,
        };
        cancelButton.FlatAppearance.BorderColor = Color.FromArgb(90, 90, 96);
        cancelButton.Click += (_, _) => Close();

        Controls.Add(spinner);
        Controls.Add(_titleLabel);
        Controls.Add(subtitleLabel);
        Controls.Add(cancelButton);

        MouseDown += Form_MouseDown;
        MouseMove += Form_MouseMove;
        MouseUp += Form_MouseUp;
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
                Close();
        };

        LaunchGame(appId);

        _pollTimer = new Timer { Interval = 500 };
        _pollTimer.Tick += PollTimer_Tick;
        _pollTimer.Start();
    }

    private static void LaunchGame(uint appId)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = $"steam://rungameid/{appId}",
                UseShellExecute = true,
            });
        }
        catch
        {
            // If Steam itself can't be found/started there is nothing more we can
            // do here; the safety timeout will close the popup regardless.
        }
    }

    private void PollTimer_Tick(object? sender, EventArgs e)
    {
        if (DateTime.UtcNow - _phaseStartedAt > SafetyTimeout)
        {
            Close();
            return;
        }

        switch (_state)
        {
            case WatchState.WaitingForHandoff:
                if (SteamHelper.IsGameRunning(_appId))
                    BeginWatchingForRealGame();
                break;

            case WatchState.WaitingForRealGame:
                AdvanceRealGameWatch();
                break;
        }
    }

    private void BeginWatchingForRealGame()
    {
        _installDir = SteamHelper.TryGetInstallDir(_appId);
        if (_installDir is null)
        {
            // Can't tell where the game lives, so there's no way to watch for a
            // launcher handoff - fall back to the original, simpler behavior.
            Close();
            return;
        }

        _initialPids = GameProcessWatcher.SnapshotPidsUnder(_installDir);
        _phaseStartedAt = DateTime.UtcNow;
        _state = WatchState.WaitingForRealGame;
        _titleLabel.Text = "Starting game…";
    }

    private void AdvanceRealGameWatch()
    {
        if (_gamePid is int pid)
        {
            if (!GameProcessWatcher.IsRunning(pid) || GameProcessWatcher.HasVisibleWindow(pid))
                Close();
            return;
        }

        int? newPid = GameProcessWatcher.FindNewPidUnder(_installDir!, _initialPids);
        if (newPid is int found)
        {
            // A different process just appeared under the game's own install
            // folder - that's the launcher handing off to the real game.
            _gamePid = found;
            return;
        }

        bool anyLookLikeLauncher = false;
        foreach (int initialPid in _initialPids)
        {
            if (GameProcessWatcher.LooksLikeLauncher(initialPid))
            {
                anyLookLikeLauncher = true;
                break;
            }
        }

        if (!anyLookLikeLauncher && _initialPids.Count > 0)
        {
            // Nothing here looks like a launcher, so assume there isn't one and
            // the process Steam already started is the game itself.
            foreach (int initialPid in _initialPids)
            {
                _gamePid = initialPid;
                break;
            }
        }
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width, Height, 16, 16));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var borderPen = new Pen(Color.FromArgb(102, 192, 244), 1.5f);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _pollTimer.Stop();
        _pollTimer.Dispose();
        base.OnFormClosed(e);
    }

    private void Form_MouseDown(object? sender, MouseEventArgs e)
    {
        _dragging = true;
        _dragStart = e.Location;
    }

    private void Form_MouseMove(object? sender, MouseEventArgs e)
    {
        if (!_dragging)
            return;

        Location = new Point(Location.X + e.X - _dragStart.X, Location.Y + e.Y - _dragStart.Y);
    }

    private void Form_MouseUp(object? sender, MouseEventArgs e) => _dragging = false;

    [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
    private static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int cx, int cy);
}
