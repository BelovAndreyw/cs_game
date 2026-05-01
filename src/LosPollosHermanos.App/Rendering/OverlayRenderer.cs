using LosPollosHermanos.Model;

namespace LosPollosHermanos.App.Rendering;

public sealed class OverlayRenderer
{
    public void Draw(Graphics g, GameSnapshot snapshot, Rectangle clientRect, float opacity)
    {
        if (opacity <= 0f)
        {
            return;
        }

        var alpha = Math.Clamp((int)(opacity * GameTheme.OverlayVeil.A), 0, 255);
        using var veil = new SolidBrush(Color.FromArgb(alpha, GameTheme.OverlayVeil));
        g.FillRectangle(veil, clientRect);

        var width = Math.Min(760f, clientRect.Width - 120f);
        var height = snapshot.IsGameOver ? 356f : 304f;
        var panelRect = new RectangleF((clientRect.Width - width) / 2f, (clientRect.Height - height) / 2f, width, height);

        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        RenderPrimitives.FillRoundedPanel(g, panelRect, Color.FromArgb(238, 14, 19, 28), Color.FromArgb(125, 142, 160, 190), radius: 24f, borderWidth: 1.5f);

        using var titleFont = GameTheme.CreateDisplayFont(30f);
        using var bodyFont = GameTheme.CreateBodyFont(10.5f);
        using var monoFont = GameTheme.CreateMonoFont(9f, FontStyle.Bold);
        using var titleBrush = new SolidBrush(GameTheme.TextPrimary);
        using var bodyBrush = new SolidBrush(GameTheme.TextSecondary);
        using var accentBrush = new SolidBrush(snapshot.IsGameOver
            ? snapshot.Outcome == ShiftOutcome.Victory ? GameTheme.Success : GameTheme.Danger
            : GameTheme.Accent);

        var innerX = panelRect.X + 26f;
        var innerY = panelRect.Y + 22f;
        var title = snapshot.IsGameOver
            ? snapshot.Outcome == ShiftOutcome.Victory ? "Смена закрыта" : "Кухня сорвалась"
            : "Лос Поллос Эрманос";
        var subtitle = snapshot.IsGameOver
            ? snapshot.StatusMessage
            : "Ночная смена начинается. Берите заказ, собирайте бургер по станциям и не злите очередь.";

        g.DrawString(title, titleFont, titleBrush, innerX, innerY);
        g.DrawString(snapshot.IsGameOver ? "Итоги смены" : "Как это работает", monoFont, accentBrush, innerX, innerY + 42f);
        RenderPrimitives.DrawWrappedText(g, subtitle, bodyFont, bodyBrush, new RectangleF(innerX, innerY + 64f, panelRect.Width - 52f, 56f));

        if (!snapshot.IsGameOver)
        {
            DrawStartCards(g, bodyFont, monoFont, panelRect, innerX, innerY + 132f);
            DrawFooterCallout(g, bodyFont, monoFont, panelRect, "ENTER — поднять ставни");
            return;
        }

        DrawSummaryCards(g, monoFont, bodyFont, panelRect, innerX, innerY + 128f, snapshot);
        DrawFooterCallout(g, bodyFont, monoFont, panelRect, "ENTER — новая смена");
    }

    private void DrawStartCards(Graphics g, Font bodyFont, Font monoFont, RectangleF panelRect, float x, float y)
    {
        var width = (panelRect.Width - 52f - (GameTheme.PanelGap * 2)) / 3f;
        DrawOverlayCard(g, new RectangleF(x, y, width, 108f), monoFont, bodyFont, GameTheme.Info, "Управление", "WASD или стрелки\nE — действие\nEnter — старт");
        DrawOverlayCard(g, new RectangleF(x + width + GameTheme.PanelGap, y, width, 108f), monoFont, bodyFont, GameTheme.Warning, "Ритм", "1. Принять заказ\n2. Пройти станции\n3. Отдать пакет");
        DrawOverlayCard(g, new RectangleF(x + (width + GameTheme.PanelGap) * 2f, y, width, 108f), monoFont, bodyFont, GameTheme.Accent, "Опасность", "Ошибки режут рейтинг.\nОчередь не любит ждать.");
    }

    private void DrawSummaryCards(Graphics g, Font monoFont, Font bodyFont, RectangleF panelRect, float x, float y, GameSnapshot snapshot)
    {
        var width = (panelRect.Width - 52f - (GameTheme.PanelGap * 3)) / 4f;
        DrawMetricCard(g, new RectangleF(x, y, width, 84f), monoFont, bodyFont, GameTheme.Info, "Очки", snapshot.Score.ToString());
        DrawMetricCard(g, new RectangleF(x + width + GameTheme.PanelGap, y, width, 84f), monoFont, bodyFont, GameTheme.Warning, "Рейтинг", snapshot.Rating.ToString());
        DrawMetricCard(g, new RectangleF(x + (width + GameTheme.PanelGap) * 2f, y, width, 84f), monoFont, bodyFont, GameTheme.Success, "Заказы", snapshot.ServedOrders.ToString());
        DrawMetricCard(g, new RectangleF(x + (width + GameTheme.PanelGap) * 3f, y, width, 84f), monoFont, bodyFont, GameTheme.Danger, "Ошибки", $"{snapshot.Mistakes}/{snapshot.MaxMistakes}");
    }

    private void DrawFooterCallout(Graphics g, Font bodyFont, Font monoFont, RectangleF panelRect, string message)
    {
        var rect = new RectangleF(panelRect.X + 26f, panelRect.Bottom - 58f, panelRect.Width - 52f, 30f);
        RenderPrimitives.FillRoundedPanel(g, rect, Color.FromArgb(176, 13, 18, 26), Color.FromArgb(126, 124, 141, 171), radius: 14f, borderWidth: 1f);
        using var brush = new SolidBrush(GameTheme.TextPrimary);
        using var prefixBrush = new SolidBrush(GameTheme.Accent);
        g.DrawString("Дальше:", monoFont, prefixBrush, rect.X + 12f, rect.Y + 6f);
        g.DrawString(message, bodyFont, brush, rect.X + 72f, rect.Y + 5f);
    }

    private static void DrawOverlayCard(Graphics g, RectangleF rect, Font monoFont, Font bodyFont, Color accent, string eyebrow, string text)
    {
        RenderPrimitives.FillRoundedPanel(g, rect, Color.FromArgb(168, 16, 21, 30), Color.FromArgb(140, accent), radius: 16f);
        using var eyebrowBrush = new SolidBrush(accent);
        using var bodyBrush = new SolidBrush(GameTheme.TextSecondary);
        g.DrawString(eyebrow, monoFont, eyebrowBrush, rect.X + 12f, rect.Y + 10f);
        RenderPrimitives.DrawWrappedText(g, text, bodyFont, bodyBrush, new RectangleF(rect.X + 12f, rect.Y + 34f, rect.Width - 24f, rect.Height - 42f));
    }

    private static void DrawMetricCard(Graphics g, RectangleF rect, Font monoFont, Font bodyFont, Color accent, string label, string value)
    {
        RenderPrimitives.FillRoundedPanel(g, rect, Color.FromArgb(170, 15, 21, 30), Color.FromArgb(140, accent), radius: 16f);
        using var labelBrush = new SolidBrush(GameTheme.TextSecondary);
        using var valueBrush = new SolidBrush(GameTheme.TextPrimary);
        using var accentBrush = new SolidBrush(accent);
        using var valueFont = GameTheme.CreateDisplayFont(20f);
        g.DrawString(label, monoFont, accentBrush, rect.X + 12f, rect.Y + 10f);
        g.DrawString(value, valueFont, valueBrush, rect.X + 12f, rect.Y + 30f);
        g.DrawString("за смену", bodyFont, labelBrush, rect.X + 12f, rect.Bottom - 22f);
    }
}
