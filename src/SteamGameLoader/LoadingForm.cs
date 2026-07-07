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
/// flips on, this watches for that first process's own window to appear:
///  - if its exe name doesn't look like a launcher, that window IS the game,
///    so the popup just closes.
///  - if it does look like a launcher, the popup hides (the launcher has its
///    own loading UI, so ours would be redundant) and watches quietly in the
///    background for a second, different process to appear under the game's
///    install folder. Once that shows up, the popup reappears and waits for
///    that process's window before finally closing.
/// </summary>
internal sealed class LoadingForm : Form
{
    private static readonly TimeSpan SafetyTimeout = TimeSpan.FromSeconds(90);

    private enum WatchState
    {
        WaitingForHandoff,
        WaitingForLauncherWindow,
        WaitingForRealGameProcess,
        WaitingForRealGameWindow,
    }

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
                    BeginWaitingForLauncherWindow();
                break;

            case WatchState.WaitingForLauncherWindow:
                AdvanceWaitingForLauncherWindow();
                break;

            case WatchState.WaitingForRealGameProcess:
                AdvanceWaitingForRealGameProcess();
                break;

            case WatchState.WaitingForRealGameWindow:
                AdvanceWaitingForRealGameWindow();
                break;
        }
    }

    private void BeginWaitingForLauncherWindow()
    {
        _installDir = SteamHelper.TryGetInstallDir(_appId);
        _initialPids = _installDir is not null ? GameProcessWatcher.SnapshotPidsUnder(_installDir) : new HashSet<int>();
        if (_installDir is null || _initialPids.Count == 0)
        {
            // Can't tell where the game lives, or nothing showed up there yet -
            // there's no way to watch for a launcher handoff, so fall back to
            // the original, simpler behavior.
            Close();
            return;
        }

        _phaseStartedAt = DateTime.UtcNow;
        _state = WatchState.WaitingForLauncherWindow;
        _titleLabel.Text = "Starting game…";
    }

    private void AdvanceWaitingForLauncherWindow()
    {
        // A different process already showing up means the hand-off happened
        // before the first process ever displayed a window of its own.
        int? newPid = GameProcessWatcher.FindNewPidUnder(_installDir!, _initialPids);
        if (newPid is int found)
        {
            BeginWaitingForRealGameWindow(found);
            return;
        }

        int? pidWithWindow = FindPidWithWindow(_initialPids);
        if (pidWithWindow is not int pid)
            return;

        if (!GameProcessWatcher.LooksLikeLauncher(pid))
        {
            // No separate launcher involved - that window IS the game.
            Close();
            return;
        }

        // The launcher's own window is up, with its own loading UI - hide ours
        // and watch quietly in the background for it handing off to the game.
        Hide();
        _phaseStartedAt = DateTime.UtcNow;
        _state = WatchState.WaitingForRealGameProcess;
    }

    private void AdvanceWaitingForRealGameProcess()
    {
        int? newPid = GameProcessWatcher.FindNewPidUnder(_installDir!, _initialPids);
        if (newPid is int found)
            BeginWaitingForRealGameWindow(found);
    }

    private void BeginWaitingForRealGameWindow(int gamePid)
    {
        _gamePid = gamePid;
        _phaseStartedAt = DateTime.UtcNow;
        _state = WatchState.WaitingForRealGameWindow;
        _titleLabel.Text = "Finishing launch…";
        Show();
    }

    private void AdvanceWaitingForRealGameWindow()
    {
        if (_gamePid is int pid && (!GameProcessWatcher.IsRunning(pid) || GameProcessWatcher.HasVisibleWindow(pid)))
            Close();
    }

    private static int? FindPidWithWindow(IEnumerable<int> pids)
    {
        foreach (int pid in pids)
        {
            if (GameProcessWatcher.HasVisibleWindow(pid))
                return pid;
        }

        return null;
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
