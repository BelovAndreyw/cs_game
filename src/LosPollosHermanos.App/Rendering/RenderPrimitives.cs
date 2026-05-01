using System.Drawing.Drawing2D;

namespace LosPollosHermanos.App.Rendering;

public static class RenderPrimitives
{
    public static void FillRoundedPanel(Graphics g, RectangleF rect, Color fill, Color border, float radius = GameTheme.PanelRadius, float borderWidth = 1.25f)
    {
        using var path = CreateRoundedRectangle(rect, radius);
        using var fillBrush = new SolidBrush(fill);
        using var borderPen = new Pen(border, borderWidth);
        g.FillPath(fillBrush, path);
        g.DrawPath(borderPen, path);
    }

    public static void DrawProgressBar(Graphics g, RectangleF rect, float progress, Color fill, Color track, Color border)
    {
        FillRoundedPanel(g, rect, track, border, radius: rect.Height / 2f, borderWidth: 1f);
        var clamped = Math.Clamp(progress, 0f, 1f);
        if (clamped <= 0f)
        {
            return;
        }

        var fillRect = new RectangleF(rect.X, rect.Y, Math.Min(rect.Width, Math.Max(rect.Height, rect.Width * clamped)), rect.Height);
        using var path = CreateRoundedRectangle(fillRect, rect.Height / 2f);
        using var brush = new SolidBrush(fill);
        g.FillPath(brush, path);
    }

    public static void DrawChip(Graphics g, RectangleF rect, string text, Font font, Color fill, Color border, Color textColor)
    {
        FillRoundedPanel(g, rect, fill, border, radius: rect.Height / 2f, borderWidth: 1f);
        using var brush = new SolidBrush(textColor);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        g.DrawString(text, font, brush, rect, format);
    }

    public static float DrawWrappedText(Graphics g, string? text, Font font, Brush brush, RectangleF layout)
    {
        if (string.IsNullOrWhiteSpace(text) || layout.Width <= 0f || layout.Height <= 0f)
        {
            return 0f;
        }

        using var format = new StringFormat
        {
            Trimming = StringTrimming.Word,
            FormatFlags = StringFormatFlags.LineLimit
        };

        var measured = g.MeasureString(text, font, new SizeF(layout.Width, 4096f), format);
        g.DrawString(text, font, brush, layout, format);
        return measured.Height;
    }

    private static GraphicsPath CreateRoundedRectangle(RectangleF rect, float radius)
    {
        var clamped = Math.Max(1f, Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2f));
        var diameter = clamped * 2f;
        var path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180f, 90f);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270f, 90f);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0f, 90f);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90f, 90f);
        path.CloseFigure();
        return path;
    }
}
