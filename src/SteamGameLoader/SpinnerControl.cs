using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SteamGameLoader;

/// <summary>A small self-drawn spinning arc, so the app needs no image assets.</summary>
internal sealed class SpinnerControl : Control
{
    private readonly Timer _timer;
    private float _angle;

    public SpinnerControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                  ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        DoubleBuffered = true;

        _timer = new Timer { Interval = 16 };
        _timer.Tick += (_, _) =>
        {
            _angle = (_angle + 6f) % 360f;
            Invalidate();
        };
        _timer.Start();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        _timer.Stop();
        _timer.Dispose();
        base.OnHandleDestroyed(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        float penWidth = Math.Max(3f, Width / 12f);
        var rect = new RectangleF(penWidth / 2, penWidth / 2, Width - penWidth, Height - penWidth);

        using var trackPen = new Pen(Color.FromArgb(60, 255, 255, 255), penWidth);
        g.DrawEllipse(trackPen, rect);

        using var arcPen = new Pen(Color.FromArgb(102, 192, 244), penWidth)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
        };
        g.DrawArc(arcPen, rect, _angle, 100f);
    }
}
