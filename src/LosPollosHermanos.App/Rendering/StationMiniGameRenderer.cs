using LosPollosHermanos.Model;
using System.Drawing.Drawing2D;

namespace LosPollosHermanos.App.Rendering;

public sealed class StationMiniGameRenderer
{
    public void Draw(Graphics g, StationMiniGameSnapshot miniGame, Rectangle viewport)
    {
        if (!miniGame.IsActive)
        {
            return;
        }

        var state = g.Save();
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using var veil = new SolidBrush(Color.FromArgb(116, 6, 8, 12));
        g.FillRectangle(veil, viewport);

        var width = Math.Min(520f, viewport.Width - 96f);
        var height = 286f;
        var rect = new RectangleF(
            viewport.X + (viewport.Width - width) / 2f,
            viewport.Y + (viewport.Height - height) / 2f,
            width,
            height);

        RenderPrimitives.FillRoundedPanel(g, rect, Color.FromArgb(244, 24, 22, 20), Color.FromArgb(160, GameTheme.Accent), radius: 18f, borderWidth: 1.4f);

        using var titleFont = GameTheme.CreateHeadingFont(18f);
        using var bodyFont = GameTheme.CreateBodyFont(9.4f);
        using var monoFont = GameTheme.CreateMonoFont(8.4f, FontStyle.Bold);
        using var titleBrush = new SolidBrush(GameTheme.TextPrimary);
        using var bodyBrush = new SolidBrush(GameTheme.TextSecondary);
        using var accentBrush = new SolidBrush(GameTheme.Accent);
        using var mutedBrush = new SolidBrush(GameTheme.TextMuted);

        var innerX = rect.X + 22f;
        var innerY = rect.Y + 18f;
        var innerWidth = rect.Width - 44f;

        g.DrawString(miniGame.Title, titleFont, titleBrush, innerX, innerY);
        RenderPrimitives.DrawWrappedText(g, miniGame.Instruction, bodyFont, bodyBrush, new RectangleF(innerX, innerY + 34f, innerWidth, 38f));

        var playRect = new RectangleF(innerX, innerY + 82f, innerWidth, 116f);
        RenderPrimitives.FillRoundedPanel(g, playRect, Color.FromArgb(214, 14, 13, 12), Color.FromArgb(96, 138, 117, 96), radius: 14f, borderWidth: 1f);

        switch (miniGame.Type)
        {
            case StationMiniGameType.GrillTiming:
                DrawTimingGame(g, miniGame, playRect);
                break;
            case StationMiniGameType.FryerDrop:
                DrawDropGame(g, miniGame, playRect, GameTheme.Warning, "корзина");
                break;
            case StationMiniGameType.AssemblyStack:
                DrawAssemblyGame(g, miniGame, playRect);
                break;
            case StationMiniGameType.DrinksFill:
                DrawDrinkGame(g, miniGame, playRect);
                break;
            case StationMiniGameType.ServingPack:
                DrawDropGame(g, miniGame, playRect, GameTheme.Success, "пакет");
                break;
        }

        var footerY = rect.Bottom - 54f;
        RenderPrimitives.DrawChip(g, new RectangleF(innerX, footerY, innerWidth, 22f), miniGame.PrimaryAction, monoFont, Color.FromArgb(178, 18, 15, 14), Color.FromArgb(104, GameTheme.Accent), GameTheme.TextPrimary);
        DrawSingleLine(g, string.IsNullOrWhiteSpace(miniGame.Feedback) ? miniGame.SecondaryAction : miniGame.Feedback, monoFont, mutedBrush, new RectangleF(innerX, footerY + 28f, innerWidth, 18f));

        g.Restore(state);
    }

    private static void DrawTimingGame(Graphics g, StationMiniGameSnapshot miniGame, RectangleF rect)
    {
        var grillRect = new RectangleF(rect.X + 26f, rect.Y + 18f, rect.Width - 52f, 42f);
        using var grillBody = new SolidBrush(Color.FromArgb(64, 59, 55));
        using var grillLine = new Pen(Color.FromArgb(150, 140, 126), 2f);
        g.FillRectangle(grillBody, grillRect);
        for (var i = 0; i < 8; i++)
        {
            var x = grillRect.X + 10f + i * ((grillRect.Width - 20f) / 7f);
            g.DrawLine(grillLine, x, grillRect.Y + 6f, x, grillRect.Bottom - 6f);
        }

        var pattyX = grillRect.X + (grillRect.Width - 44f) * miniGame.Cursor;
        using var patty = new SolidBrush(Color.FromArgb(124, 65, 40));
        using var pattyEdge = new Pen(Color.FromArgb(68, 34, 25), 2f);
        g.FillEllipse(patty, pattyX, grillRect.Y + 5f, 44f, 30f);
        g.DrawEllipse(pattyEdge, pattyX, grillRect.Y + 5f, 44f, 30f);

        DrawTargetTrack(g, miniGame, new RectangleF(rect.X + 34f, rect.Bottom - 34f, rect.Width - 68f, 12f), GameTheme.Warning);
    }

    private static void DrawDropGame(Graphics g, StationMiniGameSnapshot miniGame, RectangleF rect, Color accent, string targetLabel)
    {
        var lane = new RectangleF(rect.X + 38f, rect.Y + 58f, rect.Width - 76f, 18f);
        using var laneBrush = new SolidBrush(Color.FromArgb(94, 84, 74));
        g.FillRectangle(laneBrush, lane);

        var targetX = lane.X + lane.Width * miniGame.TargetStart;
        var targetW = lane.Width * (miniGame.TargetEnd - miniGame.TargetStart);
        var targetRect = new RectangleF(targetX, rect.Y + 34f, targetW, 66f);
        using var targetBrush = new SolidBrush(Color.FromArgb(76, accent));
        using var targetPen = new Pen(Color.FromArgb(180, accent), 2f);
        g.FillRectangle(targetBrush, targetRect);
        g.DrawRectangle(targetPen, targetRect.X, targetRect.Y, targetRect.Width, targetRect.Height);

        var itemX = lane.X + lane.Width * miniGame.Cursor;
        if (miniGame.Type == StationMiniGameType.FryerDrop)
        {
            DrawFriesSprite(g, itemX - 15f, rect.Y + 16f);
        }
        else
        {
            DrawBagItemSprite(g, itemX - 16f, rect.Y + 17f);
        }

        using var font = GameTheme.CreateMonoFont(8f, FontStyle.Bold);
        using var brush = new SolidBrush(GameTheme.TextSecondary);
        DrawSingleLine(g, targetLabel, font, brush, new RectangleF(targetRect.X - 12f, targetRect.Bottom + 4f, targetRect.Width + 24f, 18f), StringAlignment.Center);
    }

    private static void DrawAssemblyGame(Graphics g, StationMiniGameSnapshot miniGame, RectangleF rect)
    {
        var plate = new RectangleF(rect.X + rect.Width / 2f - 78f, rect.Bottom - 28f, 156f, 14f);
        using var plateBrush = new SolidBrush(Color.FromArgb(218, 213, 202));
        using var platePen = new Pen(Color.FromArgb(132, 119, 104), 1.5f);
        g.FillEllipse(plateBrush, plate);
        g.DrawEllipse(platePen, plate);

        var centerX = rect.X + rect.Width / 2f;
        var baseY = rect.Bottom - 40f;
        var colors = new[]
        {
            Color.FromArgb(231, 188, 104),
            Color.FromArgb(95, 142, 80),
            Color.FromArgb(131, 67, 42),
            Color.FromArgb(238, 213, 122)
        };

        for (var i = 0; i < miniGame.StepIndex; i++)
        {
            using var layer = new SolidBrush(colors[Math.Min(i, colors.Length - 1)]);
            g.FillRectangle(layer, centerX - 58f + i * 4f, baseY - i * 16f, 116f - i * 8f, 13f);
        }

        using var nextBrush = new SolidBrush(Color.FromArgb(130, GameTheme.Accent));
        g.FillRectangle(nextBrush, centerX - 54f, rect.Y + 16f, 108f, 12f);
        RenderPrimitives.DrawProgressBar(
            g,
            new RectangleF(rect.X + 46f, rect.Y + 82f, rect.Width - 92f, 10f),
            miniGame.StepCount == 0 ? 0f : miniGame.StepIndex / (float)miniGame.StepCount,
            GameTheme.Success,
            Color.FromArgb(86, 70, 61, 52),
            Color.FromArgb(104, 122, 102, 84));
    }

    private static void DrawDrinkGame(Graphics g, StationMiniGameSnapshot miniGame, RectangleF rect)
    {
        var cup = new RectangleF(rect.X + rect.Width / 2f - 42f, rect.Y + 14f, 84f, 88f);
        using var glass = new SolidBrush(Color.FromArgb(82, 217, 231, 244));
        using var outline = new Pen(Color.FromArgb(186, 230, 238, 244), 2f);
        g.FillRectangle(glass, cup);
        g.DrawRectangle(outline, cup.X, cup.Y, cup.Width, cup.Height);

        var fillHeight = cup.Height * Math.Clamp(miniGame.Fill, 0f, 1f);
        using var soda = new SolidBrush(Color.FromArgb(218, 88, 160, 234));
        g.FillRectangle(soda, cup.X + 5f, cup.Bottom - 5f - fillHeight, cup.Width - 10f, fillHeight);

        var targetTop = cup.Bottom - 5f - cup.Height * miniGame.TargetEnd;
        var targetBottom = cup.Bottom - 5f - cup.Height * miniGame.TargetStart;
        using var target = new Pen(Color.FromArgb(230, GameTheme.Success), 3f);
        g.DrawRectangle(target, cup.X - 7f, targetTop, cup.Width + 14f, targetBottom - targetTop);
    }

    private static void DrawTargetTrack(Graphics g, StationMiniGameSnapshot miniGame, RectangleF rect, Color accent)
    {
        RenderPrimitives.FillRoundedPanel(g, rect, Color.FromArgb(86, 70, 61, 52), Color.FromArgb(104, 122, 102, 84), radius: rect.Height / 2f);
        var targetRect = new RectangleF(rect.X + rect.Width * miniGame.TargetStart, rect.Y, rect.Width * (miniGame.TargetEnd - miniGame.TargetStart), rect.Height);
        using var targetBrush = new SolidBrush(Color.FromArgb(210, GameTheme.Success));
        g.FillRectangle(targetBrush, targetRect);

        var cursorX = rect.X + rect.Width * miniGame.Cursor;
        using var cursorPen = new Pen(accent, 3f);
        g.DrawLine(cursorPen, cursorX, rect.Y - 7f, cursorX, rect.Bottom + 7f);
    }

    private static void DrawFriesSprite(Graphics g, float x, float y)
    {
        using var box = new SolidBrush(Color.FromArgb(214, 64, 58));
        using var fry = new SolidBrush(Color.FromArgb(246, 205, 93));
        g.FillRectangle(fry, x + 4f, y, 5f, 34f);
        g.FillRectangle(fry, x + 12f, y + 2f, 5f, 32f);
        g.FillRectangle(fry, x + 20f, y - 1f, 5f, 35f);
        g.FillRectangle(box, x + 2f, y + 22f, 28f, 30f);
    }

    private static void DrawBagItemSprite(Graphics g, float x, float y)
    {
        using var bag = new SolidBrush(Color.FromArgb(230, 191, 126));
        using var fold = new SolidBrush(Color.FromArgb(178, 128, 76));
        g.FillRectangle(bag, x + 2f, y + 8f, 32f, 42f);
        g.FillRectangle(fold, x + 7f, y, 22f, 12f);
    }

    private static void DrawSingleLine(Graphics g, string? text, Font font, Brush brush, RectangleF rect, StringAlignment alignment = StringAlignment.Near)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        using var format = new StringFormat
        {
            Alignment = alignment,
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap,
            Trimming = StringTrimming.EllipsisCharacter
        };
        g.DrawString(text, font, brush, rect, format);
    }
}
