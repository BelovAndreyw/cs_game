using LosPollosHermanos.App.Controllers;
using LosPollosHermanos.App.Rendering;
using LosPollosHermanos.Model;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Numerics;

namespace LosPollosHermanos.App.Views;

public sealed class GameForm : Form
{
    private const int CellSize = 48;
    private const int ViewPadding = 18;
    private const int HudWidth = 340;
    private const float MoveStepMs = 90f;
    private const float WorldTickMs = 1000f;

    private readonly GameController controller;
    private readonly System.Windows.Forms.Timer frameTimer;
    private readonly Stopwatch frameClock = new();
    private readonly Camera2D camera = new();
    private readonly PlayerAnimator playerAnimator = new();
    private readonly HashSet<Keys> pressedKeys = new();
    private readonly List<InteractionPulse> pulses = new();

    private float moveAccumulatorMs;
    private float worldTickAccumulatorMs;
    private float uiClock;
    private float overlayOpacity = 0.88f;
    private bool pendingInteractionAnimation;
    private Direction? preferredDirection;
    private PlayerAnimationFrame playerFrame = new(PlayerAnimationMode.Idle, 0, Direction.Down);

    public GameForm(GameController controller)
    {
        this.controller = controller;

        DoubleBuffered = true;
        KeyPreview = true;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        Text = "Los Pollos Hermanos: Ночная смена";
        ClientSize = new Size(1440, 860);
        BackColor = Color.FromArgb(11, 13, 19);

        frameTimer = new System.Windows.Forms.Timer { Interval = 16 };
        frameTimer.Tick += HandleFrameTick;

        Shown += (_, _) =>
        {
            SnapCameraToPlayer(controller.Snapshot);
            frameClock.Restart();
            frameTimer.Start();
        };

        Paint += HandlePaint;
        KeyDown += HandleKeyDown;
        KeyUp += HandleKeyUp;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            frameTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    private void HandleFrameTick(object? sender, EventArgs e)
    {
        var elapsedMs = (float)frameClock.Elapsed.TotalMilliseconds;
        if (elapsedMs <= 0f)
        {
            return;
        }

        frameClock.Restart();
        elapsedMs = Math.Min(elapsedMs, 120f);
        var elapsedSeconds = elapsedMs / 1000f;
        uiClock += elapsedSeconds;

        var before = controller.Snapshot;
        var movedThisFrame = ProcessMovement(before, elapsedMs, out var direction);

        if (before.IsShiftRunning)
        {
            worldTickAccumulatorMs += elapsedMs;
            while (worldTickAccumulatorMs >= WorldTickMs)
            {
                controller.Tick();
                worldTickAccumulatorMs -= WorldTickMs;
            }
        }

        var snapshot = controller.Snapshot;
        UpdatePulses(elapsedSeconds);
        UpdateCamera(snapshot, elapsedSeconds);

        playerFrame = playerAnimator.Update(
            elapsedSeconds,
            movedThisFrame,
            pendingInteractionAnimation,
            direction);

        pendingInteractionAnimation = false;

        var overlayTarget = snapshot.IsGameOver || !snapshot.IsShiftStarted ? 0.88f : 0f;
        overlayOpacity += (overlayTarget - overlayOpacity) * Math.Clamp(elapsedSeconds * 8f, 0f, 1f);

        Invalidate();
    }

    private bool ProcessMovement(GameSnapshot snapshot, float elapsedMs, out Direction? direction)
    {
        direction = null;
        if (!snapshot.IsShiftRunning)
        {
            return false;
        }

        moveAccumulatorMs += elapsedMs;
        var moved = false;

        while (moveAccumulatorMs >= MoveStepMs)
        {
            if (!TryGetMoveDirection(out var nextDirection))
            {
                break;
            }

            controller.Move(nextDirection);
            moveAccumulatorMs -= MoveStepMs;
            direction = nextDirection;
            moved = true;
        }

        return moved;
    }

    private bool TryGetMoveDirection(out Direction direction)
    {
        if (preferredDirection is not null && IsPressed(preferredDirection.Value))
        {
            direction = preferredDirection.Value;
            return true;
        }

        var order = new[] { Direction.Up, Direction.Left, Direction.Down, Direction.Right };
        foreach (var next in order)
        {
            if (!IsPressed(next))
            {
                continue;
            }

            preferredDirection = next;
            direction = next;
            return true;
        }

        direction = default;
        return false;
    }

    private bool IsPressed(Direction direction)
    {
        return direction switch
        {
            Direction.Up => pressedKeys.Contains(Keys.W) || pressedKeys.Contains(Keys.Up),
            Direction.Down => pressedKeys.Contains(Keys.S) || pressedKeys.Contains(Keys.Down),
            Direction.Left => pressedKeys.Contains(Keys.A) || pressedKeys.Contains(Keys.Left),
            Direction.Right => pressedKeys.Contains(Keys.D) || pressedKeys.Contains(Keys.Right),
            _ => false
        };
    }

    private void HandleKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode is Keys.W or Keys.A or Keys.S or Keys.D or Keys.Up or Keys.Down or Keys.Left or Keys.Right)
        {
            pressedKeys.Add(e.KeyCode);
        }

        var snapshot = controller.Snapshot;

        if (!snapshot.IsShiftStarted && !snapshot.IsGameOver)
        {
            if (e.KeyCode is Keys.Enter or Keys.Space)
            {
                controller.StartShift();
                ResetRuntimeForNewShift();
                SnapCameraToPlayer(controller.Snapshot);
            }

            return;
        }

        if (snapshot.IsGameOver)
        {
            if (e.KeyCode is Keys.Enter or Keys.Space)
            {
                controller.RestartShift();
                ResetRuntimeForNewShift();
                SnapCameraToPlayer(controller.Snapshot);
            }

            return;
        }

        if (snapshot.IsShiftRunning && e.KeyCode == Keys.E)
        {
            controller.Interact();
            pendingInteractionAnimation = true;
            SpawnInteractionPulse(controller.Snapshot);
        }
    }

    private void HandleKeyUp(object? sender, KeyEventArgs e)
    {
        pressedKeys.Remove(e.KeyCode);
        if (preferredDirection is not null && !IsPressed(preferredDirection.Value))
        {
            preferredDirection = null;
        }
    }

    private void ResetRuntimeForNewShift()
    {
        moveAccumulatorMs = 0f;
        worldTickAccumulatorMs = 0f;
        pendingInteractionAnimation = false;
        overlayOpacity = 0f;
        pulses.Clear();
        preferredDirection = null;
        pressedKeys.Clear();
    }

    private void UpdatePulses(float elapsedSeconds)
    {
        for (var i = pulses.Count - 1; i >= 0; i--)
        {
            pulses[i].Update(elapsedSeconds);
            if (pulses[i].IsExpired)
            {
                pulses.RemoveAt(i);
            }
        }
    }

    private void SpawnInteractionPulse(GameSnapshot snapshot)
    {
        var center = GetPlayerWorldCenter(snapshot);
        var station = snapshot.Stations.FirstOrDefault(x => x.Position == snapshot.PlayerPosition);
        var color = station is null ? Color.FromArgb(255, 198, 136) : GetStationColor(station.Type);
        pulses.Add(new InteractionPulse(new PointF(center.X, center.Y), color, 0.42f, CellSize * 1.65f));
    }

    private void UpdateCamera(GameSnapshot snapshot, float elapsedSeconds)
    {
        var viewport = GetViewportRect();
        camera.Update(GetPlayerWorldCenter(snapshot), viewport.Size, GetWorldSize(snapshot), elapsedSeconds);
    }

    private void SnapCameraToPlayer(GameSnapshot snapshot)
    {
        var viewport = GetViewportRect();
        camera.SnapTo(GetPlayerWorldCenter(snapshot), viewport.Size, GetWorldSize(snapshot));
    }

    private void HandlePaint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.FromArgb(11, 13, 19));

        var snapshot = controller.Snapshot;
        var viewport = GetViewportRect();
        var hudRect = GetHudRect(viewport);

        DrawWorld(g, snapshot, viewport);
        DrawHud(g, snapshot, hudRect);

        using (var borderPen = new Pen(Color.FromArgb(85, 111, 149), 2f))
        {
            g.DrawRectangle(borderPen, viewport);
        }

        if (overlayOpacity > 0.02f)
        {
            DrawOverlay(g, snapshot);
        }
    }

    private void DrawWorld(Graphics g, GameSnapshot snapshot, Rectangle viewport)
    {
        var state = g.Save();
        g.SetClip(viewport);
        g.TranslateTransform(viewport.Left - camera.Position.X, viewport.Top - camera.Position.Y);

        using (var bg = new LinearGradientBrush(
            new RectangleF(0, 0, snapshot.MapWidth * CellSize, snapshot.MapHeight * CellSize),
            Color.FromArgb(30, 38, 54),
            Color.FromArgb(16, 21, 31),
            90f))
        {
            g.FillRectangle(bg, new RectangleF(0, 0, snapshot.MapWidth * CellSize, snapshot.MapHeight * CellSize));
        }

        var startX = Math.Max(0, (int)Math.Floor(camera.Position.X / CellSize) - 2);
        var endX = Math.Min(snapshot.MapWidth - 1, (int)Math.Ceiling((camera.Position.X + viewport.Width) / CellSize) + 2);
        var startY = Math.Max(0, (int)Math.Floor(camera.Position.Y / CellSize) - 2);
        var endY = Math.Min(snapshot.MapHeight - 1, (int)Math.Ceiling((camera.Position.Y + viewport.Height) / CellSize) + 2);

        using var tilePen = new Pen(Color.FromArgb(22, 220, 230, 255), 1f);
        for (var y = startY; y <= endY; y++)
        {
            for (var x = startX; x <= endX; x++)
            {
                var tile = new Rectangle(x * CellSize, y * CellSize, CellSize, CellSize);
                using var tileBrush = new SolidBrush(GetFloorColor(x, y));
                g.FillRectangle(tileBrush, tile);
                g.DrawRectangle(tilePen, tile);

                var hash = x * 73856093 ^ y * 19349663;
                if ((hash & 31) == 0)
                {
                    using var decoBrush = new SolidBrush(Color.FromArgb(35, 0, 0, 0));
                    g.FillEllipse(decoBrush, tile.X + 10, tile.Y + 12, 9, 9);
                }
            }
        }

        foreach (var station in snapshot.Stations)
        {
            var bob = MathF.Sin(uiClock * 2f + station.Position.X * 0.4f) * 1.8f;
            var rect = new RectangleF(
                station.Position.X * CellSize + 6,
                station.Position.Y * CellSize + 6 + bob,
                CellSize - 12,
                CellSize - 12);

            using var stationBrush = new SolidBrush(GetStationColor(station.Type));
            using var shadowBrush = new SolidBrush(Color.FromArgb(60, 0, 0, 0));
            using var labelBrush = new SolidBrush(Color.FromArgb(30, 34, 42));
            using var font = new Font("Consolas", 9, FontStyle.Bold);

            g.FillRectangle(shadowBrush, rect.X, rect.Y + 3, rect.Width, rect.Height);
            g.FillRectangle(stationBrush, rect);

            var label = RecipeBook.GetStationLabel(station.Type);
            var size = g.MeasureString(label, font);
            g.DrawString(label, font, labelBrush, rect.X + (rect.Width - size.Width) / 2f, rect.Y + (rect.Height - size.Height) / 2f + 2f);

            if (station.Position == snapshot.PlayerPosition)
            {
                using var highlightPen = new Pen(Color.FromArgb(220, 253, 240, 174), 2f);
                g.DrawEllipse(highlightPen, rect.X - 4, rect.Y - 4, rect.Width + 8, rect.Height + 8);
            }
        }

        DrawPulses(g);
        DrawPlayer(g, snapshot);

        g.Restore(state);
    }

    private void DrawPulses(Graphics g)
    {
        foreach (var pulse in pulses)
        {
            var radius = CellSize * 0.3f + pulse.Progress * pulse.MaxRadius;
            var alpha = (int)(180 * (1f - pulse.Progress));
            using var ring = new Pen(Color.FromArgb(Math.Clamp(alpha, 0, 255), pulse.Color), 2f);
            g.DrawEllipse(ring, pulse.WorldPosition.X - radius, pulse.WorldPosition.Y - radius, radius * 2, radius * 2);
        }
    }

    private void DrawPlayer(Graphics g, GameSnapshot snapshot)
    {
        var center = GetPlayerWorldCenter(snapshot);
        var body = new RectangleF(center.X - 14, center.Y - 24, 28, 38);
        var head = new RectangleF(center.X - 10, center.Y - 36, 20, 16);

        var legShift = playerFrame.Mode == PlayerAnimationMode.Walk
            ? (playerFrame.Frame % 2 == 0 ? -2 : 2)
            : 0;

        using var skinBrush = new SolidBrush(Color.FromArgb(248, 210, 172));
        using var shirtBrush = new SolidBrush(Color.FromArgb(255, 214, 102));
        using var pantsBrush = new SolidBrush(Color.FromArgb(74, 103, 156));
        using var shadowBrush = new SolidBrush(Color.FromArgb(75, 0, 0, 0));

        g.FillEllipse(shadowBrush, center.X - 12, center.Y + 14, 24, 8);
        g.FillRectangle(pantsBrush, center.X - 9 + legShift, center.Y + 8, 7, 15);
        g.FillRectangle(pantsBrush, center.X + 2 - legShift, center.Y + 8, 7, 15);
        g.FillRectangle(shirtBrush, body);
        g.FillRectangle(skinBrush, head);
    }

    private void DrawHud(Graphics g, GameSnapshot snapshot, Rectangle hudRect)
    {
        using var panelBrush = new LinearGradientBrush(hudRect, Color.FromArgb(23, 29, 41), Color.FromArgb(15, 19, 28), 90f);
        g.FillRectangle(panelBrush, hudRect);
        using (var borderPen = new Pen(Color.FromArgb(90, 112, 146), 2f))
        {
            g.DrawRectangle(borderPen, hudRect);
        }

        using var titleFont = new Font("Consolas", 13, FontStyle.Bold);
        using var textFont = new Font("Consolas", 9, FontStyle.Regular);
        using var accentBrush = new SolidBrush(Color.FromArgb(132, 213, 255));
        using var textBrush = new SolidBrush(Color.FromArgb(236, 239, 247));

        var x = hudRect.X + 14;
        var y = hudRect.Y + 14;
        g.DrawString("Los Pollos Hermanos", titleFont, accentBrush, x, y);
        y += 26;
        g.DrawString("Ночная смена", textFont, textBrush, x, y);
        y += 24;

        g.DrawString($"Время: {FormatTime(snapshot.TimeRemainingSeconds)}", textFont, textBrush, x, y);
        y += 18;
        DrawBar(g, new RectangleF(x, y, hudRect.Width - 28, 10), snapshot.ShiftDurationSeconds <= 0 ? 0f : snapshot.TimeRemainingSeconds / (float)snapshot.ShiftDurationSeconds, Color.FromArgb(123, 209, 255));
        y += 18;
        g.DrawString($"Очки: {snapshot.Score}", textFont, textBrush, x, y);
        y += 18;
        g.DrawString($"Рейтинг: {snapshot.Rating}", textFont, textBrush, x, y);
        y += 18;
        DrawBar(g, new RectangleF(x, y, hudRect.Width - 28, 10), snapshot.Rating / 100f, Color.FromArgb(135, 229, 171));
        y += 20;
        g.DrawString($"Ошибки: {snapshot.Mistakes}  Выполнено: {snapshot.ServedOrders}", textFont, textBrush, x, y);
        y += 28;

        g.DrawString("Текущий заказ:", textFont, accentBrush, x, y);
        y += 18;
        g.DrawString(snapshot.CurrentOrderName ?? "Нет активного заказа", textFont, textBrush, x, y);
        y += 18;
        if (snapshot.CurrentOrderName is not null)
        {
            g.DrawString($"Терпение: {snapshot.CustomerPatienceSecondsLeft}", textFont, textBrush, x, y);
            y += 18;
        }

        var completed = snapshot.CompletedStations.ToHashSet();
        foreach (var station in snapshot.RequiredStations)
        {
            g.DrawString($"{(completed.Contains(station) ? "[x]" : "[ ]")} {RecipeBook.GetStationName(station)}", textFont, textBrush, x, y);
            y += 16;
        }

        y += 10;
        g.DrawString("Статус:", textFont, accentBrush, x, y);
        y += 16;
        g.DrawString(Wrap(snapshot.StatusMessage, 34), textFont, textBrush, new RectangleF(x, y, hudRect.Width - 28, 96));
    }

    private void DrawBar(Graphics g, RectangleF rect, float value, Color color)
    {
        var clamped = Math.Clamp(value, 0f, 1f);
        using var bg = new SolidBrush(Color.FromArgb(40, 50, 66));
        using var fill = new SolidBrush(color);
        g.FillRectangle(bg, rect);
        g.FillRectangle(fill, rect.X + 1, rect.Y + 1, (rect.Width - 2) * clamped, rect.Height - 2);
    }

    private void DrawOverlay(Graphics g, GameSnapshot snapshot)
    {
        using var veil = new SolidBrush(Color.FromArgb((int)(overlayOpacity * 190f), 10, 12, 18));
        g.FillRectangle(veil, ClientRectangle);

        using var titleFont = new Font("Consolas", 28, FontStyle.Bold);
        using var textFont = new Font("Consolas", 12, FontStyle.Regular);
        using var titleBrush = new SolidBrush(Color.FromArgb(238, 244, 255));

        var cx = ClientSize.Width / 2f;
        var y = ClientSize.Height / 2f - 90f;

        if (!snapshot.IsShiftStarted && !snapshot.IsGameOver)
        {
            DrawCentered(g, "Los Pollos Hermanos", titleFont, titleBrush, cx, y);
            DrawCentered(g, "Ночная смена", textFont, titleBrush, cx, y + 46f);
            DrawCentered(g, "Нажми ENTER, чтобы начать", textFont, titleBrush, cx, y + 84f);
            DrawCentered(g, "WASD/стрелки - движение | E - взаимодействие", textFont, titleBrush, cx, y + 120f);
        }
        else if (snapshot.IsGameOver)
        {
            var title = snapshot.Outcome == ShiftOutcome.Victory ? "Смена закрыта" : "Вас уволили";
            DrawCentered(g, title, titleFont, titleBrush, cx, y);
            DrawCentered(g, $"Очки: {snapshot.Score} | Выполнено заказов: {snapshot.ServedOrders}", textFont, titleBrush, cx, y + 52f);
            DrawCentered(g, "Нажми ENTER для новой смены", textFont, titleBrush, cx, y + 92f);
        }
    }

    private Rectangle GetViewportRect()
    {
        return new Rectangle(ViewPadding, ViewPadding, ClientSize.Width - HudWidth - ViewPadding * 3, ClientSize.Height - ViewPadding * 2);
    }

    private Rectangle GetHudRect(Rectangle viewport)
    {
        return new Rectangle(viewport.Right + ViewPadding, ViewPadding, HudWidth, ClientSize.Height - ViewPadding * 2);
    }

    private static Vector2 GetPlayerWorldCenter(GameSnapshot snapshot)
    {
        return new Vector2((snapshot.PlayerPosition.X + 0.5f) * CellSize, (snapshot.PlayerPosition.Y + 0.5f) * CellSize);
    }

    private static SizeF GetWorldSize(GameSnapshot snapshot)
    {
        return new SizeF(snapshot.MapWidth * CellSize, snapshot.MapHeight * CellSize);
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

    private static Color GetFloorColor(int x, int y)
    {
        var noise = ((x * 73856093 ^ y * 19349663) & 15) - 7;
        return Color.FromArgb(
            Math.Clamp(40 + noise, 26, 62),
            Math.Clamp(48 + noise, 30, 74),
            Math.Clamp(62 + noise, 36, 86));
    }

    private static string FormatTime(int totalSeconds)
    {
        var time = TimeSpan.FromSeconds(Math.Max(0, totalSeconds));
        return $"{time.Minutes:00}:{time.Seconds:00}";
    }

    private static string Wrap(string text, int maxLen)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length <= maxLen)
        {
            return text;
        }

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var line = string.Empty;
        foreach (var word in words)
        {
            var candidate = string.IsNullOrEmpty(line) ? word : $"{line} {word}";
            if (candidate.Length > maxLen)
            {
                lines.Add(line);
                line = word;
            }
            else
            {
                line = candidate;
            }
        }

        if (!string.IsNullOrWhiteSpace(line))
        {
            lines.Add(line);
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static void DrawCentered(Graphics g, string text, Font font, Brush brush, float centerX, float y)
    {
        var size = g.MeasureString(text, font);
        g.DrawString(text, font, brush, centerX - size.Width / 2f, y);
    }
}
