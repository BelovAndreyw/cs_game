using LosPollosHermanos.Model;
using System.Drawing.Drawing2D;

namespace LosPollosHermanos.App.Rendering;

public sealed class HudRenderer
{
    private const float HeaderHeight = 132f;
    private const float FocusHeight = 304f;
    private const float ActionHeight = 154f;

    public void Draw(Graphics g, GameSnapshot snapshot, Rectangle hudRect, GameObjective objective)
    {
        var state = g.Save();
        g.SetClip(hudRect);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using var background = new LinearGradientBrush(hudRect, GameTheme.HudBackgroundTop, GameTheme.HudBackgroundBottom, LinearGradientMode.Vertical);
        g.FillRectangle(background, hudRect);

        using var softAmber = new SolidBrush(Color.FromArgb(18, GameTheme.Accent));
        using var softBlue = new SolidBrush(Color.FromArgb(12, GameTheme.Info));
        g.FillEllipse(softAmber, hudRect.X - 42, hudRect.Y + 10, hudRect.Width + 86, 128);
        g.FillEllipse(softBlue, hudRect.X + 24, hudRect.Bottom - 196, hudRect.Width - 48, 142);

        using var border = new Pen(GameTheme.ViewportBorder, 1.2f);
        g.DrawRectangle(border, hudRect);

        var x = (float)(hudRect.X + GameTheme.PanelPadding);
        var y = (float)(hudRect.Y + GameTheme.PanelPadding);
        var width = (float)(hudRect.Width - (GameTheme.PanelPadding * 2));

        DrawHeader(g, snapshot, new RectangleF(x, y, width, HeaderHeight));
        y += HeaderHeight + GameTheme.PanelGap;

        DrawFocusCard(g, snapshot, objective, new RectangleF(x, y, width, FocusHeight));
        y += FocusHeight + GameTheme.PanelGap;

        DrawActionCard(g, snapshot, objective, new RectangleF(x, y, width, ActionHeight));
        y += ActionHeight + GameTheme.PanelGap;

        var summaryHeight = hudRect.Bottom - y - GameTheme.PanelPadding;
        if (summaryHeight >= 96f)
        {
            DrawSummaryCard(g, snapshot, new RectangleF(x, y, width, summaryHeight));
        }

        g.Restore(state);
    }

    private void DrawHeader(Graphics g, GameSnapshot snapshot, RectangleF rect)
    {
        RenderPrimitives.FillRoundedPanel(g, rect, GameTheme.PanelFill, GameTheme.PanelBorder, radius: 16f);

        using var titleFont = GameTheme.CreateHeadingFont(15.2f);
        using var subtitleFont = GameTheme.CreateMonoFont(7.6f, FontStyle.Bold);
        using var timeFont = GameTheme.CreateDisplayFont(15f);
        using var metricLabelFont = GameTheme.CreateMonoFont(7.2f, FontStyle.Bold);
        using var metricValueFont = GameTheme.CreateHeadingFont(9.8f);
        using var titleBrush = new SolidBrush(GameTheme.TextPrimary);
        using var accentBrush = new SolidBrush(GameTheme.Accent);

        var innerX = rect.X + 16f;
        var innerY = rect.Y + 13f;
        var innerWidth = rect.Width - 32f;
        var timeRect = new RectangleF(rect.Right - 94f, innerY - 1f, 76f, 38f);
        var titleRect = new RectangleF(innerX, innerY, innerWidth - 92f, 26f);

        DrawSingleLine(g, "Лос Поллос", titleFont, titleBrush, titleRect);
        DrawSingleLine(g, "ночная смена", subtitleFont, accentBrush, new RectangleF(innerX, innerY + 29f, 136f, 14f));

        RenderPrimitives.FillRoundedPanel(g, timeRect, Color.FromArgb(205, 18, 15, 15), Color.FromArgb(160, GameTheme.Accent), radius: 13f);
        DrawCentered(g, GamePresentation.FormatTime(snapshot.TimeRemainingSeconds), timeFont, accentBrush, timeRect);

        RenderPrimitives.DrawChip(
            g,
            new RectangleF(rect.Right - 104f, innerY + 45f, 86f, 20f),
            GamePresentation.FormatDifficulty(snapshot.Difficulty),
            subtitleFont,
            Color.FromArgb(178, 20, 17, 15),
            Color.FromArgb(126, GameTheme.GetDifficultyColor(snapshot.Difficulty)),
            GameTheme.TextPrimary);

        var metricsY = innerY + 66f;
        var metricGap = 7f;
        var metricWidth = (innerWidth - metricGap * 2f) / 3f;
        DrawMiniMetric(g, new RectangleF(innerX, metricsY, metricWidth, 34f), "Очки", snapshot.Score.ToString(), GameTheme.Info, metricLabelFont, metricValueFont);
        DrawMiniMetric(g, new RectangleF(innerX + metricWidth + metricGap, metricsY, metricWidth, 34f), "Рейтинг", snapshot.Rating.ToString(), GameTheme.Success, metricLabelFont, metricValueFont);
        DrawMiniMetric(g, new RectangleF(innerX + (metricWidth + metricGap) * 2f, metricsY, metricWidth, 34f), "Ошибки", $"{snapshot.Mistakes}/{snapshot.MaxMistakes}", GameTheme.Danger, metricLabelFont, metricValueFont);

        var progressRect = new RectangleF(innerX, rect.Bottom - 15f, innerWidth, 7f);
        RenderPrimitives.DrawProgressBar(g, progressRect, GamePresentation.GetShiftProgress(snapshot), GameTheme.Accent, Color.FromArgb(64, 88, 74, 65), Color.FromArgb(110, 146, 123, 106));
    }

    private void DrawFocusCard(Graphics g, GameSnapshot snapshot, GameObjective objective, RectangleF rect)
    {
        var ticketFill = Color.FromArgb(244, 236, 219);
        var ticketBorder = Color.FromArgb(180, 141, 116, 86);
        var ink = Color.FromArgb(57, 41, 33);
        var muted = Color.FromArgb(110, 82, 66);
        var (phaseText, phaseAccent) = GetPhaseBadge(snapshot);

        RenderPrimitives.FillRoundedPanel(g, rect, ticketFill, ticketBorder, radius: 16f, borderWidth: 1.2f);

        using var chipFont = GameTheme.CreateMonoFont(7.8f, FontStyle.Bold);
        using var titleFont = GameTheme.CreateHeadingFont(12.4f);
        using var bodyFont = GameTheme.CreateBodyFont(8.7f);
        using var labelFont = GameTheme.CreateBodyFont(8.4f, FontStyle.Bold);
        using var titleBrush = new SolidBrush(ink);
        using var bodyBrush = new SolidBrush(muted);

        var innerX = rect.X + 16f;
        var innerY = rect.Y + 15f;
        var contentWidth = rect.Width - 32f;
        var rightBadge = BuildRightBadge(snapshot);

        RenderPrimitives.DrawChip(g, new RectangleF(innerX, innerY, 100f, 20f), phaseText, chipFont, Color.FromArgb(238, 255, 250, 241), Color.FromArgb(175, phaseAccent), ink);
        RenderPrimitives.DrawChip(g, new RectangleF(rect.Right - 112f, innerY, 96f, 20f), rightBadge, chipFont, Color.FromArgb(225, 255, 249, 238), Color.FromArgb(118, 136, 115, 98), ink);

        DrawSingleLine(g, objective.Title, titleFont, titleBrush, new RectangleF(innerX, innerY + 31f, contentWidth, 24f));
        RenderPrimitives.DrawWrappedText(g, objective.Description, bodyFont, bodyBrush, new RectangleF(innerX, innerY + 58f, contentWidth, 39f));

        var bodyTop = innerY + 106f;
        if (snapshot.IsTutorialPhase)
        {
            DrawTutorialPanel(g, snapshot, new RectangleF(innerX, bodyTop, contentWidth, 156f));
            return;
        }

        if (!string.IsNullOrWhiteSpace(snapshot.CurrentCustomerName))
        {
            DrawGuestPanel(g, snapshot, new RectangleF(innerX, bodyTop, contentWidth, 86f), titleBrush, bodyBrush, labelFont);
            DrawNextStepCard(g, snapshot, new RectangleF(innerX, bodyTop + 96f, contentWidth, 36f), chipFont, titleBrush, bodyBrush);
            DrawOrderSteps(g, snapshot, new RectangleF(innerX, bodyTop + 142f, contentWidth, 30f), chipFont);
            return;
        }

        DrawIdlePanel(g, new RectangleF(innerX, bodyTop, contentWidth, 112f), titleBrush, bodyBrush, labelFont);
    }

    private void DrawTutorialPanel(Graphics g, GameSnapshot snapshot, RectangleF rect)
    {
        RenderPrimitives.FillRoundedPanel(g, rect, Color.FromArgb(222, 32, 27, 24), Color.FromArgb(150, GameTheme.Info), radius: 12f, borderWidth: 1f);

        using var labelFont = GameTheme.CreateBodyFont(8.6f, FontStyle.Bold);
        using var bodyFont = GameTheme.CreateBodyFont(8.7f);
        using var chipFont = GameTheme.CreateMonoFont(7.7f, FontStyle.Bold);
        using var labelBrush = new SolidBrush(GameTheme.Info);
        using var bodyBrush = new SolidBrush(Color.FromArgb(238, 228, 214));
        using var mutedBrush = new SolidBrush(Color.FromArgb(193, 176, 158));

        DrawSingleLine(g, "Шеф говорит", labelFont, labelBrush, new RectangleF(rect.X + 12f, rect.Y + 10f, rect.Width - 24f, 18f));
        RenderPrimitives.DrawWrappedText(g, snapshot.ChefMessage, bodyFont, bodyBrush, new RectangleF(rect.X + 12f, rect.Y + 32f, rect.Width - 24f, 62f));

        DrawSingleLine(g, $"Сейчас: {GetStationUiName(snapshot.TutorialTargetStation)}", chipFont, mutedBrush, new RectangleF(rect.X + 12f, rect.Bottom - 34f, rect.Width - 100f, 18f));
        RenderPrimitives.DrawChip(g, new RectangleF(rect.Right - 84f, rect.Bottom - 38f, 70f, 21f), $"{snapshot.TutorialSecondsLeft} шага", chipFont, Color.FromArgb(230, 255, 248, 238), Color.FromArgb(126, GameTheme.Info), Color.FromArgb(58, 42, 34));
    }

    private void DrawGuestPanel(Graphics g, GameSnapshot snapshot, RectangleF rect, Brush titleBrush, Brush bodyBrush, Font labelFont)
    {
        RenderPrimitives.FillRoundedPanel(g, rect, Color.FromArgb(218, 252, 245, 232), Color.FromArgb(124, 145, 122, 102), radius: 12f, borderWidth: 1f);

        using var orderFont = GameTheme.CreateHeadingFont(11f);
        using var bodyFont = GameTheme.CreateBodyFont(8.4f);

        DrawSingleLine(g, $"Гость: {snapshot.CurrentCustomerName}", labelFont, titleBrush, new RectangleF(rect.X + 12f, rect.Y + 8f, rect.Width - 24f, 18f));
        DrawSingleLine(g, snapshot.CurrentOrderName ?? "Заказ появится сейчас", orderFont, titleBrush, new RectangleF(rect.X + 12f, rect.Y + 28f, rect.Width - 24f, 21f));

        var patienceRect = new RectangleF(rect.X + 12f, rect.Y + 55f, rect.Width - 24f, 9f);
        RenderPrimitives.DrawProgressBar(g, patienceRect, GamePresentation.GetPatienceProgress(snapshot), GameTheme.Warning, Color.FromArgb(82, 129, 116, 108), Color.FromArgb(115, 156, 133, 114));
        DrawSingleLine(g, BuildQueueText(snapshot), bodyFont, bodyBrush, new RectangleF(rect.X + 12f, rect.Y + 67f, rect.Width - 24f, 16f));
    }

    private void DrawNextStepCard(Graphics g, GameSnapshot snapshot, RectangleF rect, Font chipFont, Brush titleBrush, Brush bodyBrush)
    {
        RenderPrimitives.FillRoundedPanel(g, rect, Color.FromArgb(216, 250, 242, 228), Color.FromArgb(112, 144, 120, 101), radius: 11f, borderWidth: 1f);
        DrawSingleLine(g, "Следующий шаг", chipFont, titleBrush, new RectangleF(rect.X + 12f, rect.Y + 9f, 104f, 18f));
        DrawSingleLine(g, BuildNextStepText(snapshot), chipFont, bodyBrush, new RectangleF(rect.X + 120f, rect.Y + 9f, rect.Width - 132f, 18f), StringAlignment.Far);
    }

    private void DrawOrderSteps(Graphics g, GameSnapshot snapshot, RectangleF rect, Font chipFont)
    {
        var steps = GamePresentation.BuildOrderSteps(snapshot);
        if (steps.Count == 0)
        {
            return;
        }

        var gap = 5f;
        var stepWidth = (rect.Width - gap * (steps.Count - 1)) / steps.Count;
        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            var accent = step.IsCompleted ? GameTheme.Success : Color.FromArgb(150, 126, 103, 84);
            var fill = step.IsCompleted ? Color.FromArgb(230, 235, 248, 226) : Color.FromArgb(230, 255, 248, 238);
            var label = step.IsCompleted ? $"✓ {step.Label}" : step.Label;
            RenderPrimitives.DrawChip(g, new RectangleF(rect.X + i * (stepWidth + gap), rect.Y, stepWidth, rect.Height), label, chipFont, fill, Color.FromArgb(136, accent), Color.FromArgb(57, 41, 33));
        }
    }

    private void DrawIdlePanel(Graphics g, RectangleF rect, Brush titleBrush, Brush bodyBrush, Font labelFont)
    {
        RenderPrimitives.FillRoundedPanel(g, rect, Color.FromArgb(218, 252, 245, 232), Color.FromArgb(124, 145, 122, 102), radius: 12f, borderWidth: 1f);
        using var bodyFont = GameTheme.CreateBodyFont(8.8f);
        DrawSingleLine(g, "Короткая пауза", labelFont, titleBrush, new RectangleF(rect.X + 12f, rect.Y + 10f, rect.Width - 24f, 18f));
        RenderPrimitives.DrawWrappedText(g, "Сейчас можно перевести дух. Следующий гость уже идёт к стойке.", bodyFont, bodyBrush, new RectangleF(rect.X + 12f, rect.Y + 34f, rect.Width - 24f, rect.Height - 44f));
    }

    private void DrawActionCard(Graphics g, GameSnapshot snapshot, GameObjective objective, RectangleF rect)
    {
        RenderPrimitives.FillRoundedPanel(g, rect, GameTheme.PanelFillMuted, GameTheme.PanelBorder, radius: 16f);

        using var keyFont = GameTheme.CreateDisplayFont(18f);
        using var titleFont = GameTheme.CreateHeadingFont(11.2f);
        using var bodyFont = GameTheme.CreateBodyFont(8.5f);
        using var monoFont = GameTheme.CreateMonoFont(7.8f, FontStyle.Bold);
        using var titleBrush = new SolidBrush(GameTheme.TextPrimary);
        using var bodyBrush = new SolidBrush(GameTheme.TextSecondary);
        using var accentBrush = new SolidBrush(GameTheme.Accent);

        var innerX = rect.X + 16f;
        var innerY = rect.Y + 15f;
        var contentWidth = rect.Width - 32f;
        var keyRect = new RectangleF(innerX, innerY + 2f, 44f, 44f);

        RenderPrimitives.FillRoundedPanel(g, keyRect, Color.FromArgb(216, 14, 12, 12), Color.FromArgb(172, GameTheme.Accent), radius: 12f, borderWidth: 1f);
        DrawCentered(g, "E", keyFont, accentBrush, keyRect);

        DrawSingleLine(g, GamePresentation.BuildInteractionTitle(snapshot), titleFont, titleBrush, new RectangleF(innerX + 56f, innerY + 2f, contentWidth - 56f, 20f));
        RenderPrimitives.DrawWrappedText(g, GamePresentation.BuildInteractionCaption(snapshot), bodyFont, bodyBrush, new RectangleF(innerX + 56f, innerY + 25f, contentWidth - 56f, 34f));

        var nextY = innerY + 66f;
        if (snapshot.InteractionMode != StationInteractionMode.None)
        {
            RenderPrimitives.DrawProgressBar(
                g,
                new RectangleF(innerX, nextY, contentWidth, 8f),
                snapshot.InteractionProgress,
                GameTheme.Accent,
                Color.FromArgb(70, 86, 71, 62),
                Color.FromArgb(116, 146, 123, 106));
            nextY += 18f;
        }

        RenderPrimitives.DrawChip(g, new RectangleF(innerX, nextY, contentWidth, 21f), BuildActionStatus(snapshot, objective), monoFont, Color.FromArgb(178, 18, 15, 14), Color.FromArgb(98, 142, 117, 100), GameTheme.TextSecondary);
        RenderPrimitives.DrawChip(g, new RectangleF(innerX, nextY + 29f, contentWidth, 21f), "WASD / стрелки - движение", monoFont, Color.FromArgb(152, 17, 14, 13), Color.FromArgb(86, 125, 103, 88), GameTheme.TextMuted);
    }

    private void DrawSummaryCard(Graphics g, GameSnapshot snapshot, RectangleF rect)
    {
        RenderPrimitives.FillRoundedPanel(g, rect, Color.FromArgb(210, 30, 25, 21), Color.FromArgb(112, 137, 113, 92), radius: 16f);

        using var labelFont = GameTheme.CreateMonoFont(7.8f, FontStyle.Bold);
        using var bodyFont = GameTheme.CreateBodyFont(8.5f);
        using var valueFont = GameTheme.CreateHeadingFont(9.5f);
        using var titleBrush = new SolidBrush(GameTheme.TextPrimary);
        using var bodyBrush = new SolidBrush(GameTheme.TextSecondary);
        using var mutedBrush = new SolidBrush(GameTheme.TextMuted);
        using var accentBrush = new SolidBrush(GameTheme.Accent);

        var innerX = rect.X + 16f;
        var innerY = rect.Y + 14f;
        var contentWidth = rect.Width - 32f;

        DrawSingleLine(g, "Состояние", labelFont, accentBrush, new RectangleF(innerX, innerY, contentWidth, 18f));
        RenderPrimitives.DrawWrappedText(g, snapshot.StatusMessage, bodyFont, bodyBrush, new RectangleF(innerX, innerY + 24f, contentWidth, 42f));

        var rowY = innerY + 76f;
        var rowGap = 8f;
        var rowWidth = (contentWidth - rowGap) / 2f;
        DrawInfoPill(g, new RectangleF(innerX, rowY, rowWidth, 31f), "Заказы", snapshot.ServedOrders.ToString(), GameTheme.Success, labelFont, valueFont);
        DrawInfoPill(g, new RectangleF(innerX + rowWidth + rowGap, rowY, rowWidth, 31f), "Станция", snapshot.CurrentStationName is null ? "нет" : ShortenStationName(snapshot.CurrentStationName), GameTheme.Info, labelFont, valueFont);

        if (snapshot.CurrentOrderName is null || snapshot.CustomerPatienceMaxSeconds <= 0)
        {
            var hint = snapshot.TutorialHints.FirstOrDefault();
            DrawSingleLine(g, hint ?? "Держите темп и следите за целью.", bodyFont, mutedBrush, new RectangleF(innerX, rowY + 42f, contentWidth, 22f));
            return;
        }

        DrawSingleLine(g, $"Терпение клиента: {snapshot.CustomerPatienceSecondsLeft} сек.", bodyFont, mutedBrush, new RectangleF(innerX, rowY + 42f, contentWidth, 18f));
        RenderPrimitives.DrawProgressBar(g, new RectangleF(innerX, rowY + 65f, contentWidth, 8f), GamePresentation.GetPatienceProgress(snapshot), GameTheme.Warning, Color.FromArgb(72, 86, 71, 62), Color.FromArgb(110, 146, 123, 106));
    }

    private static void DrawMiniMetric(Graphics g, RectangleF rect, string label, string value, Color accent, Font labelFont, Font valueFont)
    {
        RenderPrimitives.FillRoundedPanel(g, rect, Color.FromArgb(188, 16, 13, 13), Color.FromArgb(118, accent), radius: 10f, borderWidth: 1f);
        using var labelBrush = new SolidBrush(GameTheme.TextMuted);
        using var valueBrush = new SolidBrush(GameTheme.TextPrimary);

        DrawSingleLine(g, label, labelFont, labelBrush, new RectangleF(rect.X + 8f, rect.Y + 4f, rect.Width - 16f, 12f));
        DrawSingleLine(g, value, valueFont, valueBrush, new RectangleF(rect.X + 8f, rect.Y + 17f, rect.Width - 16f, 14f), StringAlignment.Center);
    }

    private static void DrawInfoPill(Graphics g, RectangleF rect, string label, string value, Color accent, Font labelFont, Font valueFont)
    {
        RenderPrimitives.FillRoundedPanel(g, rect, Color.FromArgb(168, 18, 15, 14), Color.FromArgb(92, accent), radius: 10f, borderWidth: 1f);
        using var labelBrush = new SolidBrush(GameTheme.TextMuted);
        using var valueBrush = new SolidBrush(GameTheme.TextPrimary);

        DrawSingleLine(g, label, labelFont, labelBrush, new RectangleF(rect.X + 8f, rect.Y + 3f, rect.Width - 16f, 11f));
        DrawSingleLine(g, value, valueFont, valueBrush, new RectangleF(rect.X + 8f, rect.Y + 15f, rect.Width - 16f, 14f), StringAlignment.Center);
    }

    private static string BuildRightBadge(GameSnapshot snapshot)
    {
        if (snapshot.IsTutorialPhase)
        {
            return $"шаг {Math.Max(1, 5 - snapshot.TutorialSecondsLeft)}/4";
        }

        return !string.IsNullOrWhiteSpace(snapshot.CurrentCustomerName)
            ? $"очередь {snapshot.WaitingCustomerNames.Count + 1}"
            : "пауза";
    }

    private static (string Text, Color Accent) GetPhaseBadge(GameSnapshot snapshot)
    {
        if (!snapshot.IsShiftStarted && !snapshot.IsGameOver)
        {
            return ("старт", GameTheme.Accent);
        }

        if (snapshot.IsGameOver)
        {
            return ("итог", snapshot.Outcome == ShiftOutcome.Victory ? GameTheme.Success : GameTheme.Danger);
        }

        if (snapshot.IsTutorialPhase)
        {
            return ("обучение", GameTheme.Info);
        }

        if (snapshot.CurrentOrderName is null)
        {
            return ("пауза", GameTheme.Info);
        }

        if (!snapshot.IsCurrentOrderAccepted)
        {
            return ("касса", GameTheme.Warning);
        }

        return snapshot.CompletedStations.Count == snapshot.RequiredStations.Count
            ? ("выдача", GameTheme.Success)
            : ("в работе", GameTheme.Accent);
    }

    private static string BuildQueueText(GameSnapshot snapshot)
    {
        return snapshot.WaitingCustomerNames.Count switch
        {
            0 => "После этого заказа очередь пустая.",
            1 => "Следом идёт ещё 1 гость.",
            _ => $"В очереди ещё {snapshot.WaitingCustomerNames.Count} гостя."
        };
    }

    private static string BuildNextStepText(GameSnapshot snapshot)
    {
        if (!snapshot.IsCurrentOrderAccepted)
        {
            return "Касса";
        }

        var completedCounts = snapshot.CompletedStations
            .GroupBy(station => station)
            .ToDictionary(group => group.Key, group => group.Count());

        foreach (var group in snapshot.RequiredStations.GroupBy(station => station))
        {
            completedCounts.TryGetValue(group.Key, out var completed);
            if (completed < group.Count())
            {
                var name = GetStationUiName(group.Key);
                return group.Count() == 1 ? name : $"{name} {completed + 1}/{group.Count()}";
            }
        }

        return "Выдача";
    }

    private static string BuildActionStatus(GameSnapshot snapshot, GameObjective objective)
    {
        if (snapshot.IsTutorialPhase && objective.Station is not null)
        {
            return $"Цель: {GetStationUiName(objective.Station)}";
        }

        if (snapshot.CurrentStationName is not null)
        {
            return $"У станции: {ShortenStationName(snapshot.CurrentStationName)}";
        }

        return "Между станциями";
    }

    private static string GetStationUiName(StationType? station)
    {
        return station switch
        {
            StationType.OrderDesk => "Касса",
            StationType.Grill => "Гриль",
            StationType.Assembly => "Сборка",
            StationType.Fryer => "Фритюр",
            StationType.Drinks => "Напитки",
            StationType.ServingCounter => "Выдача",
            _ => "Ожидание"
        };
    }

    private static string ShortenStationName(string stationName)
    {
        if (stationName == RecipeBook.GetStationName(StationType.OrderDesk))
        {
            return "Касса";
        }

        if (stationName == RecipeBook.GetStationName(StationType.ServingCounter))
        {
            return "Выдача";
        }

        if (stationName == RecipeBook.GetStationName(StationType.Drinks))
        {
            return "Напитки";
        }

        return stationName;
    }

    private static void DrawSingleLine(Graphics g, string? text, Font font, Brush brush, RectangleF rect, StringAlignment alignment = StringAlignment.Near)
    {
        if (string.IsNullOrWhiteSpace(text) || rect.Width <= 0f || rect.Height <= 0f)
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

    private static void DrawCentered(Graphics g, string text, Font font, Brush brush, RectangleF rect)
    {
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            FormatFlags = StringFormatFlags.NoWrap,
            Trimming = StringTrimming.EllipsisCharacter
        };
        g.DrawString(text, font, brush, rect, format);
    }
}
