using LosPollosHermanos.App.Controllers;
using LosPollosHermanos.Model;
using System.Drawing.Drawing2D;

namespace LosPollosHermanos.App.Views;

public sealed class GameForm : Form
{
    private const int CellSize = 44;
    private const int BoardPadding = 16;
    private const int HudWidth = 320;

    private readonly GameController controller;
    private readonly System.Windows.Forms.Timer gameTimer;

    public GameForm(GameController controller)
    {
        this.controller = controller;
        var snapshot = controller.Snapshot;

        DoubleBuffered = true;
        KeyPreview = true;
        Text = "Los Pollos Hermanos: Ночная смена";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        BackColor = Color.FromArgb(20, 23, 30);
        ClientSize = new Size(
            BoardPadding * 3 + snapshot.MapWidth * CellSize + HudWidth,
            BoardPadding * 2 + snapshot.MapHeight * CellSize);

        gameTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        gameTimer.Tick += HandleTimerTick;
        KeyDown += HandleKeyDown;
        Paint += HandlePaint;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            gameTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    private void HandleTimerTick(object? sender, EventArgs e)
    {
        controller.Tick();
        SyncTimer();
        Invalidate();
    }

    private void HandleKeyDown(object? sender, KeyEventArgs e)
    {
        var snapshot = controller.Snapshot;

        if (!snapshot.IsShiftStarted && !snapshot.IsGameOver)
        {
            if (e.KeyCode is Keys.Enter or Keys.Space)
            {
                controller.StartShift();
                SyncTimer();
                Invalidate();
                e.Handled = true;
            }

            return;
        }

        if (snapshot.IsGameOver)
        {
            if (e.KeyCode is Keys.Enter or Keys.Space)
            {
                controller.RestartShift();
                SyncTimer();
                Invalidate();
                e.Handled = true;
            }

            return;
        }

        switch (e.KeyCode)
        {
            case Keys.W:
            case Keys.Up:
                controller.Move(Direction.Up);
                break;
            case Keys.S:
            case Keys.Down:
                controller.Move(Direction.Down);
                break;
            case Keys.A:
            case Keys.Left:
                controller.Move(Direction.Left);
                break;
            case Keys.D:
            case Keys.Right:
                controller.Move(Direction.Right);
                break;
            case Keys.E:
                controller.Interact();
                break;
            default:
                return;
        }

        SyncTimer();
        Invalidate();
        e.Handled = true;
    }

    private void HandlePaint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.None;
        g.Clear(BackColor);

        var snapshot = controller.Snapshot;
        DrawBoard(g, snapshot);
        DrawHud(g, snapshot);

        if (!snapshot.IsShiftStarted && !snapshot.IsGameOver)
        {
            DrawStartOverlay(g, snapshot);
        }
        else if (snapshot.IsGameOver)
        {
            DrawEndOverlay(g, snapshot);
        }
    }

    private void DrawBoard(Graphics g, GameSnapshot snapshot)
    {
        for (var y = 0; y < snapshot.MapHeight; y++)
        {
            for (var x = 0; x < snapshot.MapWidth; x++)
            {
                var rect = GetCellRect(x, y);
                var fill = (x + y) % 2 == 0
                    ? Color.FromArgb(35, 41, 53)
                    : Color.FromArgb(30, 36, 47);

                using var brush = new SolidBrush(fill);
                g.FillRectangle(brush, rect);
            }
        }

        foreach (var station in snapshot.Stations)
        {
            var rect = GetCellRect(station.Position.X, station.Position.Y);
            rect.Inflate(-4, -4);
            using var brush = new SolidBrush(GetStationColor(station.Type));
            g.FillRectangle(brush, rect);

            using var stationFont = new Font("Consolas", 8, FontStyle.Bold);
            using var textBrush = new SolidBrush(Color.FromArgb(22, 24, 30));
            var text = RecipeBook.GetStationLabel(station.Type);
            var textSize = g.MeasureString(text, stationFont);
            var textPosition = new PointF(
                rect.Left + (rect.Width - textSize.Width) / 2f,
                rect.Top + (rect.Height - textSize.Height) / 2f);
            g.DrawString(text, stationFont, textBrush, textPosition);
        }

        var playerRect = GetCellRect(snapshot.PlayerPosition.X, snapshot.PlayerPosition.Y);
        playerRect.Inflate(-8, -8);
        using (var playerBrush = new SolidBrush(Color.FromArgb(255, 214, 102)))
        {
            g.FillEllipse(playerBrush, playerRect);
        }

        using (var playerPen = new Pen(Color.FromArgb(40, 42, 49), 2))
        {
            g.DrawEllipse(playerPen, playerRect);
        }

        using var borderPen = new Pen(Color.FromArgb(70, 80, 102), 2);
        g.DrawRectangle(
            borderPen,
            BoardPadding - 2,
            BoardPadding - 2,
            snapshot.MapWidth * CellSize + 4,
            snapshot.MapHeight * CellSize + 4);
    }

    private void DrawHud(Graphics g, GameSnapshot snapshot)
    {
        var hudX = BoardPadding * 2 + snapshot.MapWidth * CellSize;
        var hudY = BoardPadding;
        var hudHeight = snapshot.MapHeight * CellSize;
        var panelRect = new Rectangle(hudX, hudY, HudWidth, hudHeight);

        using (var panelBrush = new SolidBrush(Color.FromArgb(27, 31, 40)))
        {
            g.FillRectangle(panelBrush, panelRect);
        }

        using (var panelBorder = new Pen(Color.FromArgb(80, 90, 112), 2))
        {
            g.DrawRectangle(panelBorder, panelRect);
        }

        using var titleFont = new Font("Consolas", 13, FontStyle.Bold);
        using var bodyFont = new Font("Consolas", 10, FontStyle.Regular);
        using var smallFont = new Font("Consolas", 9, FontStyle.Regular);
        using var textBrush = new SolidBrush(Color.FromArgb(236, 238, 245));
        using var accentBrush = new SolidBrush(Color.FromArgb(120, 206, 255));
        using var warningBrush = new SolidBrush(Color.FromArgb(255, 183, 93));

        var x = hudX + 12;
        var y = hudY + 12;

        g.DrawString("Los Pollos Hermanos", titleFont, accentBrush, x, y);
        y += 32;
        g.DrawString("Ночная смена", bodyFont, textBrush, x, y);
        y += 28;

        g.DrawString($"Время: {FormatTime(snapshot.TimeRemainingSeconds)}", bodyFont, textBrush, x, y);
        y += 22;
        g.DrawString($"Очки: {snapshot.Score}", bodyFont, textBrush, x, y);
        y += 22;
        g.DrawString($"Рейтинг: {snapshot.Rating}", bodyFont, textBrush, x, y);
        y += 22;
        g.DrawString($"Ошибки: {snapshot.Mistakes}", bodyFont, warningBrush, x, y);
        y += 22;
        g.DrawString($"Выполнено: {snapshot.ServedOrders}", bodyFont, textBrush, x, y);
        y += 28;

        g.DrawString("Текущий заказ:", bodyFont, accentBrush, x, y);
        y += 22;

        if (snapshot.CurrentOrderName is null)
        {
            g.DrawString("Нет активного заказа", bodyFont, textBrush, x, y);
            y += 22;
        }
        else
        {
            g.DrawString(snapshot.CurrentOrderName, bodyFont, textBrush, x, y);
            y += 22;
            g.DrawString($"Терпение клиента: {snapshot.CustomerPatienceSecondsLeft}", smallFont, warningBrush, x, y);
            y += 20;
            g.DrawString("Этапы:", bodyFont, accentBrush, x, y);
            y += 20;

            var done = snapshot.CompletedStations.ToHashSet();
            foreach (var stationType in snapshot.RequiredStations)
            {
                var marker = done.Contains(stationType) ? "[x]" : "[ ]";
                var stationLine = $"{marker} {RecipeBook.GetStationName(stationType)}";
                g.DrawString(stationLine, smallFont, textBrush, x, y);
                y += 18;
            }
        }

        y += 8;
        g.DrawString("Статус:", bodyFont, accentBrush, x, y);
        y += 20;
        var statusText = WrapText(snapshot.StatusMessage, 40);
        g.DrawString(statusText, smallFont, textBrush, new RectangleF(x, y, HudWidth - 24, 60));
        y += 66;

        if (!string.IsNullOrWhiteSpace(snapshot.CurrentStationName))
        {
            g.DrawString($"Станция: {snapshot.CurrentStationName}", smallFont, warningBrush, x, y);
            y += 24;
        }

        g.DrawString("Управление:", bodyFont, accentBrush, x, y);
        y += 20;
        g.DrawString("WASD/стрелки - движение", smallFont, textBrush, x, y);
        y += 18;
        g.DrawString("E - действие на станции", smallFont, textBrush, x, y);
    }

    private void DrawStartOverlay(Graphics g, GameSnapshot snapshot)
    {
        using var veilBrush = new SolidBrush(Color.FromArgb(190, 10, 12, 17));
        g.FillRectangle(veilBrush, ClientRectangle);

        using var titleFont = new Font("Consolas", 28, FontStyle.Bold);
        using var subtitleFont = new Font("Consolas", 12, FontStyle.Regular);
        using var textBrush = new SolidBrush(Color.FromArgb(240, 242, 250));
        using var accentBrush = new SolidBrush(Color.FromArgb(120, 206, 255));

        var centerX = ClientSize.Width / 2f;
        var topY = ClientSize.Height / 2f - 110f;

        DrawCentered(g, "Los Pollos Hermanos", titleFont, accentBrush, centerX, topY);
        DrawCentered(g, "Ночная смена", subtitleFont, textBrush, centerX, topY + 42);
        DrawCentered(g, "Нажми ENTER, чтобы начать", subtitleFont, textBrush, centerX, topY + 80);

        var hints = string.Join(Environment.NewLine, snapshot.TutorialHints);
        DrawCentered(g, hints, subtitleFont, textBrush, centerX, topY + 130);
    }

    private void DrawEndOverlay(Graphics g, GameSnapshot snapshot)
    {
        using var veilBrush = new SolidBrush(Color.FromArgb(195, 12, 15, 21));
        g.FillRectangle(veilBrush, ClientRectangle);

        var title = snapshot.Outcome == ShiftOutcome.Victory ? "Смена закрыта" : "Вас уволили";
        var accentColor = snapshot.Outcome == ShiftOutcome.Victory
            ? Color.FromArgb(120, 232, 156)
            : Color.FromArgb(255, 128, 128);

        using var titleFont = new Font("Consolas", 28, FontStyle.Bold);
        using var subtitleFont = new Font("Consolas", 12, FontStyle.Regular);
        using var accentBrush = new SolidBrush(accentColor);
        using var textBrush = new SolidBrush(Color.FromArgb(240, 242, 250));

        var centerX = ClientSize.Width / 2f;
        var topY = ClientSize.Height / 2f - 95f;

        DrawCentered(g, title, titleFont, accentBrush, centerX, topY);
        DrawCentered(g, $"Очки: {snapshot.Score} | Выполнено: {snapshot.ServedOrders}", subtitleFont, textBrush, centerX, topY + 46);
        DrawCentered(g, "Нажми ENTER, чтобы начать новую смену", subtitleFont, textBrush, centerX, topY + 84);
    }

    private Rectangle GetCellRect(int x, int y)
    {
        return new Rectangle(
            BoardPadding + x * CellSize,
            BoardPadding + y * CellSize,
            CellSize,
            CellSize);
    }

    private static Color GetStationColor(StationType type)
    {
        return type switch
        {
            StationType.OrderDesk => Color.FromArgb(131, 197, 190),
            StationType.Grill => Color.FromArgb(255, 179, 102),
            StationType.Assembly => Color.FromArgb(255, 141, 161),
            StationType.Fryer => Color.FromArgb(255, 215, 117),
            StationType.Drinks => Color.FromArgb(158, 185, 243),
            StationType.ServingCounter => Color.FromArgb(168, 230, 161),
            _ => Color.FromArgb(180, 180, 180)
        };
    }

    private static string FormatTime(int totalSeconds)
    {
        var time = TimeSpan.FromSeconds(Math.Max(0, totalSeconds));
        return $"{time.Minutes:00}:{time.Seconds:00}";
    }

    private static string WrapText(string text, int maxLineLength)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length <= maxLineLength)
        {
            return text;
        }

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var currentLine = string.Empty;

        foreach (var word in words)
        {
            var candidate = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
            if (candidate.Length > maxLineLength)
            {
                lines.Add(currentLine);
                currentLine = word;
            }
            else
            {
                currentLine = candidate;
            }
        }

        if (!string.IsNullOrWhiteSpace(currentLine))
        {
            lines.Add(currentLine);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static void DrawCentered(Graphics g, string text, Font font, Brush brush, float centerX, float y)
    {
        var size = g.MeasureString(text, font);
        g.DrawString(text, font, brush, centerX - size.Width / 2f, y);
    }

    private void SyncTimer()
    {
        var shouldRun = controller.Snapshot.IsShiftRunning;
        if (shouldRun && !gameTimer.Enabled)
        {
            gameTimer.Start();
        }
        else if (!shouldRun && gameTimer.Enabled)
        {
            gameTimer.Stop();
        }
    }
}
