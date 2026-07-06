using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SteamGameLoader;

/// <summary>
/// The "Game Loading..." popup. Launches the Steam game, then polls Steam's own
/// "Running" registry flag every 500ms and closes itself as soon as the game
/// actually starts, with a safety-net timeout and a manual Cancel button.
/// </summary>
internal sealed class LoadingForm : Form
{
    private static readonly TimeSpan SafetyTimeout = TimeSpan.FromSeconds(90);

    private readonly uint _appId;
    private readonly Timer _pollTimer;
    private readonly DateTime _startedAt = DateTime.UtcNow;
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

        var spinner = new SpinnerControl { Size = new Size(48, 48), Location = new Point(24, 28) };

        string gameName = SteamHelper.TryGetGameName(appId) ?? $"Steam game (AppID {appId})";

        var titleLabel = new Label
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
        Controls.Add(titleLabel);
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
        if (SteamHelper.IsGameRunning(_appId))
        {
            Close();
            return;
        }

        if (DateTime.UtcNow - _startedAt > SafetyTimeout)
        {
            Close();
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
