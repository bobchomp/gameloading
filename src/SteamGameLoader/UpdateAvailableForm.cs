using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SteamGameLoader;

/// <summary>
/// "A newer version is available" popup, with an Update Now button that
/// downloads the new installer, launches it, and exits this app so the
/// installer can overwrite it; and a Later button that just remembers not to
/// nag about this same version again.
/// </summary>
internal sealed class UpdateAvailableForm : Form
{
    private readonly UpdateChecker.UpdateInfo _update;
    private readonly Button _updateButton;
    private readonly Button _laterButton;
    private readonly Label _statusLabel;

    public UpdateAvailableForm(UpdateChecker.UpdateInfo update)
    {
        _update = update;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(380, 190);
        BackColor = Color.FromArgb(32, 32, 36);
        TopMost = true;
        ShowInTaskbar = true;
        KeyPreview = true;
        Text = "Update Available";
        if (AppIcon.TryLoad() is Icon appIcon)
            Icon = appIcon;

        var titleLabel = new Label
        {
            Text = "Update available",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 13F, FontStyle.Bold),
            Location = new Point(24, 22),
            AutoSize = true,
        };

        var messageLabel = new Label
        {
            Text = "A newer version of Steam Loading Popups is available. Please update now.",
            ForeColor = Color.FromArgb(200, 200, 200),
            Font = new Font("Segoe UI", 9.5F),
            Location = new Point(24, 56),
            Size = new Size(332, 46),
        };

        _statusLabel = new Label
        {
            Text = $"Version {update.Version} is available.",
            ForeColor = Color.FromArgb(150, 150, 155),
            Font = new Font("Segoe UI", 8.75F),
            Location = new Point(24, 104),
            AutoSize = true,
        };

        _laterButton = new Button
        {
            Text = "Later",
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            BackColor = Color.FromArgb(60, 60, 66),
            Size = new Size(90, 32),
            Location = new Point(ClientSize.Width - 20 - 110 - 10 - 90, ClientSize.Height - 20 - 32),
            Cursor = Cursors.Hand,
        };
        _laterButton.FlatAppearance.BorderColor = Color.FromArgb(90, 90, 96);
        _laterButton.Click += (_, _) =>
        {
            UpdateDismissal.Dismiss(_update.Version);
            Close();
        };

        _updateButton = new Button
        {
            Text = "Update Now",
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.FromArgb(6, 32, 44),
            BackColor = Color.FromArgb(102, 192, 244),
            Size = new Size(110, 32),
            Location = new Point(ClientSize.Width - 20 - 110, ClientSize.Height - 20 - 32),
            Cursor = Cursors.Hand,
        };
        _updateButton.FlatAppearance.BorderSize = 0;
        _updateButton.Click += UpdateButton_Click;

        Controls.Add(titleLabel);
        Controls.Add(messageLabel);
        Controls.Add(_statusLabel);
        Controls.Add(_laterButton);
        Controls.Add(_updateButton);

        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
                Close();
        };
    }

    private async void UpdateButton_Click(object? sender, EventArgs e)
    {
        _updateButton.Enabled = false;
        _laterButton.Enabled = false;
        _updateButton.Text = "Downloading…";
        _statusLabel.Text = "Downloading the installer…";

        bool started = await UpdateChecker.DownloadAndLaunchInstallerAsync(_update);

        if (started)
        {
            // The installer needs this process's file handle released before it
            // can overwrite our own exe, so exit immediately rather than closing
            // this dialog normally.
            Environment.Exit(0);
        }
        else
        {
            _statusLabel.Text = "Download failed. Check your connection and try again.";
            _updateButton.Text = "Update Now";
            _updateButton.Enabled = true;
            _laterButton.Enabled = true;
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

    [DllImport("Gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
    private static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int cx, int cy);
}
