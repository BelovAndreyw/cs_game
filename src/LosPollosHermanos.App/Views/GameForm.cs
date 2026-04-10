using LosPollosHermanos.App.Controllers;
using LosPollosHermanos.App.Rendering;
using LosPollosHermanos.Model;
using System.Diagnostics;
using System.Numerics;

namespace LosPollosHermanos.App.Views;

public sealed class GameForm : Form
{
    private const int CellSize = 48;
    private const int ViewPadding = 18;
    private const int HudWidth = 360;
    private const float MoveRepeatStepMs = 170f;
    private const float MoveInitialRepeatDelayMs = 230f;
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
    private bool interactionKeyHeld;
    private bool pendingInteractionAnimation;
    private bool queuedSingleStepMove;
    private Direction? queuedSingleStepDirection;
    private Direction? preferredDirection;
    private float overlayOpacity = 0.9f;
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
        ClientSize = new Size(1460, 870);
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
        if (queuedSingleStepMove)
        {
            movedThisFrame = true;
            direction ??= queuedSingleStepDirection;
            queuedSingleStepMove = false;
            queuedSingleStepDirection = null;
        }

        controller.UpdateRealtime(elapsedSeconds);
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
        UpdateCamera(snapshot, elapsedSeconds);
        UpdatePulses(elapsedSeconds);
        playerFrame = playerAnimator.Update(elapsedSeconds, movedThisFrame, pendingInteractionAnimation, direction);
        pendingInteractionAnimation = false;

        var overlayTarget = snapshot.IsGameOver || !snapshot.IsShiftStarted ? 0.9f : 0f;
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
        while (moveAccumulatorMs >= MoveRepeatStepMs)
        {
            if (!TryGetMoveDirection(out var nextDirection))
            {
                moveAccumulatorMs = MoveRepeatStepMs;
                break;
            }

            controller.Move(nextDirection);
            moveAccumulatorMs -= MoveRepeatStepMs;
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

        foreach (var next in new[] { Direction.Up, Direction.Left, Direction.Down, Direction.Right })
        {
            if (IsPressed(next))
            {
                preferredDirection = next;
                direction = next;
                return true;
            }
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
        var snapshot = controller.Snapshot;
        if (e.KeyCode is Keys.W or Keys.A or Keys.S or Keys.D or Keys.Up or Keys.Down or Keys.Left or Keys.Right)
        {
            var isNewPress = pressedKeys.Add(e.KeyCode);
            if (isNewPress && snapshot.IsShiftRunning && TryMapKeyToDirection(e.KeyCode, out var direction))
            {
                preferredDirection = direction;
                controller.Move(direction);
                queuedSingleStepMove = true;
                queuedSingleStepDirection = direction;
                moveAccumulatorMs = -MoveInitialRepeatDelayMs;
            }
        }

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

        if (snapshot.IsShiftRunning && e.KeyCode == Keys.E && !interactionKeyHeld)
        {
            interactionKeyHeld = true;
            controller.BeginInteraction();
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

        if (e.KeyCode == Keys.E && interactionKeyHeld)
        {
            interactionKeyHeld = false;
            controller.EndInteraction();
        }
    }

    private void ResetRuntimeForNewShift()
    {
        moveAccumulatorMs = 0f;
        worldTickAccumulatorMs = 0f;
        interactionKeyHeld = false;
        pendingInteractionAnimation = false;
        queuedSingleStepMove = false;
        queuedSingleStepDirection = null;
        preferredDirection = null;
        overlayOpacity = 0f;
        pulses.Clear();
        pressedKeys.Clear();
        controller.EndInteraction();
    }

    private static bool TryMapKeyToDirection(Keys key, out Direction direction)
    {
        direction = key switch
        {
            Keys.W or Keys.Up => Direction.Up,
            Keys.S or Keys.Down => Direction.Down,
            Keys.A or Keys.Left => Direction.Left,
            Keys.D or Keys.Right => Direction.Right,
            _ => default
        };

        return key is Keys.W or Keys.A or Keys.S or Keys.D or Keys.Up or Keys.Down or Keys.Left or Keys.Right;
    }

    private void SpawnInteractionPulse(GameSnapshot snapshot)
    {
        var center = GetPlayerWorldCenter(snapshot);
        var station = snapshot.Stations.FirstOrDefault(x => x.Position == snapshot.PlayerPosition);
        var color = station is null ? Color.FromArgb(255, 198, 136) : GetStationColor(station.Type);
        pulses.Add(new InteractionPulse(new PointF(center.X, center.Y), color, 0.42f, CellSize * 1.65f));
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
        g.Clear(Color.FromArgb(11, 13, 19));
        var snapshot = controller.Snapshot;
        var viewport = GetViewportRect();
        var hudRect = GetHudRect(viewport);
        var objective = GetObjective(snapshot);

        DrawWorld(g, snapshot, viewport, objective.Station);
        DrawHud(g, snapshot, hudRect, objective.Text);
        if (overlayOpacity > 0.02f)
        {
            DrawOverlay(g, snapshot);
        }
    }

    private void DrawWorld(Graphics g, GameSnapshot snapshot, Rectangle viewport, StationType? objectiveStation)
    {
        var state = g.Save();
        g.SetClip(viewport);
        g.TranslateTransform(viewport.Left - camera.Position.X, viewport.Top - camera.Position.Y);

        for (var y = 0; y < snapshot.MapHeight; y++)
        {
            for (var x = 0; x < snapshot.MapWidth; x++)
            {
                using var brush = new SolidBrush(GetFloorColor(x, y, snapshot.KitchenStartRow));
                g.FillRectangle(brush, x * CellSize, y * CellSize, CellSize, CellSize);
            }
        }

        using (var divider = new Pen(Color.FromArgb(184, 220, 192, 130), 4f))
        {
            var y = (snapshot.KitchenStartRow - 2) * CellSize;
            g.DrawLine(divider, 0, y, snapshot.MapWidth * CellSize, y);
        }

        foreach (var station in snapshot.Stations)
        {
            var rect = new RectangleF(station.Position.X * CellSize + 6, station.Position.Y * CellSize + 6, CellSize - 12, CellSize - 12);
            using var stationBrush = new SolidBrush(GetStationColor(station.Type));
            g.FillRectangle(stationBrush, rect);
            if (objectiveStation is not null && station.Type == objectiveStation)
            {
                using var p = new Pen(Color.FromArgb(220, 151, 240, 184), 2f);
                g.DrawRectangle(p, rect.X - 6, rect.Y - 6, rect.Width + 12, rect.Height + 12);
            }
        }

        using var npcNameFont = new Font("Consolas", 8, FontStyle.Bold);
        foreach (var npc in snapshot.Npcs)
        {
            var cx = (npc.Position.X + 0.5f) * CellSize;
            var cy = (npc.Position.Y + 0.5f) * CellSize;
            using var body = new SolidBrush(npc.Role == NpcRole.Chef ? Color.FromArgb(255, 218, 126) : Color.FromArgb(166, 206, 255));
            g.FillEllipse(body, cx - 10, cy - 12, 20, 20);

            using var nameBrush = new SolidBrush(Color.FromArgb(236, 243, 255));
            using var nameShadow = new SolidBrush(Color.FromArgb(110, 0, 0, 0));
            var name = npc.Name;
            var nameSize = g.MeasureString(name, npcNameFont);
            var nameX = cx - nameSize.Width / 2f;
            var nameY = cy - 27f;
            g.DrawString(name, npcNameFont, nameShadow, nameX + 1f, nameY + 1f);
            g.DrawString(name, npcNameFont, nameBrush, nameX, nameY);
        }

        DrawPulses(g);
        DrawPlayer(g, snapshot);
        g.Restore(state);
    }

    private static Color GetFloorColor(int x, int y, int kitchenStartRow)
    {
        var noise = ((x * 73856093 ^ y * 19349663) & 15) - 7;
        if (y < kitchenStartRow - 2)
        {
            return Color.FromArgb(Math.Clamp(68 + noise, 48, 88), Math.Clamp(56 + noise, 40, 74), Math.Clamp(44 + noise, 30, 60));
        }

        if (y < kitchenStartRow)
        {
            return Color.FromArgb(Math.Clamp(86 + noise, 68, 108), Math.Clamp(74 + noise, 56, 96), Math.Clamp(52 + noise, 35, 72));
        }

        return Color.FromArgb(Math.Clamp(42 + noise, 26, 62), Math.Clamp(52 + noise, 34, 72), Math.Clamp(68 + noise, 42, 90));
    }

    private void DrawPulses(Graphics g)
    {
        foreach (var pulse in pulses)
        {
            var radius = CellSize * 0.3f + pulse.Progress * pulse.MaxRadius;
            var alpha = (int)(170 * (1f - pulse.Progress));
            using var ring = new Pen(Color.FromArgb(Math.Clamp(alpha, 0, 255), pulse.Color), 2f);
            g.DrawEllipse(ring, pulse.WorldPosition.X - radius, pulse.WorldPosition.Y - radius, radius * 2, radius * 2);
        }
    }

    private void DrawPlayer(Graphics g, GameSnapshot snapshot)
    {
        var center = GetPlayerWorldCenter(snapshot);
        var legShift = playerFrame.Mode == PlayerAnimationMode.Walk ? (playerFrame.Frame % 2 == 0 ? -2 : 2) : 0;
        using var shirt = new SolidBrush(Color.FromArgb(255, 214, 102));
        using var pants = new SolidBrush(Color.FromArgb(74, 103, 156));
        using var skin = new SolidBrush(Color.FromArgb(248, 210, 172));

        g.FillRectangle(pants, center.X - 8 + legShift, center.Y + 8, 7, 15);
        g.FillRectangle(pants, center.X + 2 - legShift, center.Y + 8, 7, 15);
        g.FillRectangle(shirt, center.X - 14, center.Y - 24, 28, 38);
        g.FillRectangle(skin, center.X - 10, center.Y - 36, 20, 16);
    }

    private (StationType? Station, string Text) GetObjective(GameSnapshot snapshot)
    {
        if (!snapshot.IsShiftRunning)
        {
            return (null, "Нажмите Enter, чтобы начать");
        }

        if (snapshot.IsTutorialPhase)
        {
            return (snapshot.TutorialTargetStation, $"Обучение ({snapshot.TutorialSecondsLeft}с): {snapshot.ChefMessage}");
        }

        if (snapshot.CurrentOrderName is null)
        {
            return (null, "Ожидание следующего клиента");
        }

        if (!snapshot.IsCurrentOrderAccepted)
        {
            return (StationType.OrderDesk, "Примите заказ на стойке");
        }

        var completed = snapshot.CompletedStations.ToHashSet();
        var next = snapshot.RequiredStations.FirstOrDefault(s => !completed.Contains(s));
        if (next != default)
        {
            return (next, $"Выполните этап: {RecipeBook.GetStationName(next)}");
        }

        return (StationType.ServingCounter, "Выдайте заказ на стойке выдачи");
    }

    private void DrawHud(Graphics g, GameSnapshot snapshot, Rectangle hudRect, string objectiveText)
    {
        using var bg = new SolidBrush(Color.FromArgb(20, 26, 38));
        g.FillRectangle(bg, hudRect);
        using var titleFont = new Font("Consolas", 13, FontStyle.Bold);
        using var textFont = new Font("Consolas", 9, FontStyle.Regular);
        using var accent = new SolidBrush(Color.FromArgb(132, 213, 255));
        using var text = new SolidBrush(Color.FromArgb(236, 239, 247));

        var x = hudRect.X + 14;
        var y = hudRect.Y + 14;
        g.DrawString("Los Pollos Hermanos", titleFont, accent, x, y);
        y += 24;
        g.DrawString($"Время: {FormatTime(snapshot.TimeRemainingSeconds)}", textFont, text, x, y);
        y += 18;
        g.DrawString($"Сложность: {FormatDifficulty(snapshot.Difficulty)}", textFont, text, x, y);
        y += 18;
        g.DrawString($"Очки: {snapshot.Score}  Рейтинг: {snapshot.Rating}", textFont, text, x, y);
        y += 18;
        g.DrawString($"Ошибки: {snapshot.Mistakes}  Выполнено: {snapshot.ServedOrders}", textFont, text, x, y);
        y += 22;
        g.DrawString("Цель:", textFont, accent, x, y);
        y += 16;
        g.DrawString(Wrap(objectiveText, 36), textFont, text, x, y);
        y += 32;
        g.DrawString($"Клиент: {snapshot.CurrentCustomerName ?? "-"}", textFont, text, x, y);
        y += 16;
        g.DrawString($"Заказ: {snapshot.CurrentOrderName ?? "-"}", textFont, text, x, y);
        y += 16;
        g.DrawString($"Терпение: {snapshot.CustomerPatienceSecondsLeft}", textFont, text, x, y);
        y += 16;
        if (snapshot.WaitingCustomerNames.Count > 0)
        {
            g.DrawString($"Очередь: {string.Join(", ", snapshot.WaitingCustomerNames)}", textFont, text, x, y);
            y += 16;
        }

        g.DrawString($"Действие: {FormatInteractionMode(snapshot.InteractionMode)}", textFont, accent, x, y);
        y += 16;
        if (!string.IsNullOrWhiteSpace(snapshot.InteractionHint))
        {
            g.DrawString(Wrap(snapshot.InteractionHint, 36), textFont, text, x, y);
            y += 30;
        }

        g.DrawString("Статус:", textFont, accent, x, y);
        y += 16;
        g.DrawString(Wrap(snapshot.StatusMessage, 36), textFont, text, x, y);
    }

    private void DrawOverlay(Graphics g, GameSnapshot snapshot)
    {
        using var veil = new SolidBrush(Color.FromArgb((int)(overlayOpacity * 190f), 10, 12, 18));
        g.FillRectangle(veil, ClientRectangle);
        using var titleFont = new Font("Consolas", 28, FontStyle.Bold);
        using var textFont = new Font("Consolas", 12, FontStyle.Regular);
        using var brush = new SolidBrush(Color.FromArgb(238, 244, 255));
        var cx = ClientSize.Width / 2f;
        var y = ClientSize.Height / 2f - 90f;

        if (!snapshot.IsShiftStarted && !snapshot.IsGameOver)
        {
            DrawCentered(g, "Los Pollos Hermanos", titleFont, brush, cx, y);
            DrawCentered(g, "Нажмите ENTER, чтобы начать", textFont, brush, cx, y + 58f);
            DrawCentered(g, "WASD/стрелки - движение | E - удержание/серия нажатий", textFont, brush, cx, y + 95f);
        }
        else if (snapshot.IsGameOver)
        {
            var title = snapshot.Outcome == ShiftOutcome.Victory ? "Смена завершена" : "Вас уволили";
            DrawCentered(g, title, titleFont, brush, cx, y);
            DrawCentered(g, $"Очки: {snapshot.Score} | Выполнено: {snapshot.ServedOrders}", textFont, brush, cx, y + 58f);
            DrawCentered(g, "Нажмите ENTER для новой смены", textFont, brush, cx, y + 95f);
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

    private static string FormatDifficulty(ShiftDifficulty difficulty)
    {
        return difficulty switch
        {
            ShiftDifficulty.Easy => "Легкая",
            ShiftDifficulty.Medium => "Средняя",
            ShiftDifficulty.Hard => "Сложная",
            _ => "Неизвестно"
        };
    }

    private static string FormatInteractionMode(StationInteractionMode mode)
    {
        return mode switch
        {
            StationInteractionMode.None => "Нет",
            StationInteractionMode.Hold => "Удержание",
            StationInteractionMode.RapidTap => "Серия нажатий",
            _ => "Неизвестно"
        };
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
            if (line.Length == 0)
            {
                line = word;
                continue;
            }

            var candidate = $"{line} {word}";
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
