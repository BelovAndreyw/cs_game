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
    private readonly GameSpriteLibrary spriteLibrary;
    private readonly WorldRenderer worldRenderer;
    private readonly HudRenderer hudRenderer = new();
    private readonly OverlayRenderer overlayRenderer = new();
    private readonly StationMiniGameRenderer miniGameRenderer = new();

    private float moveAccumulatorMs;
    private float worldTickAccumulatorMs;
    private bool interactionKeyHeld;
    private bool miniGameActionHeld;
    private bool pendingInteractionAnimation;
    private bool queuedSingleStepMove;
    private Direction? queuedSingleStepDirection;
    private Direction? preferredDirection;
    private float overlayOpacity = 0.9f;
    private PlayerAnimationFrame playerFrame = new(PlayerAnimationMode.Idle, 0, Direction.Down);

    public GameForm(GameController controller)
    {
        this.controller = controller;
        spriteLibrary = new GameSpriteLibrary(Path.Combine(AppContext.BaseDirectory, "Assets"));
        worldRenderer = new WorldRenderer(CellSize, spriteLibrary);

        DoubleBuffered = true;
        KeyPreview = true;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        ClientSize = new Size(1460, 870);
        Text = "Лос Поллос Эрманос: ночная смена";
        BackColor = GameTheme.WindowBackground;

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
            spriteLibrary.Dispose();
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

        var overlayTarget = snapshot.IsGameOver || !snapshot.IsShiftStarted ? 0.92f : 0f;
        overlayOpacity += (overlayTarget - overlayOpacity) * Math.Clamp(elapsedSeconds * 8f, 0f, 1f);
        Invalidate();
    }

    private bool ProcessMovement(GameSnapshot snapshot, float elapsedMs, out Direction? direction)
    {
        direction = null;
        if (!snapshot.IsShiftRunning || snapshot.MiniGame.IsActive)
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
        if (snapshot.MiniGame.IsActive)
        {
            HandleMiniGameKeyDown(e);
            return;
        }

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
        if (controller.Snapshot.MiniGame.IsActive)
        {
            if (e.KeyCode == Keys.E)
            {
                if (miniGameActionHeld)
                {
                    controller.EndMiniGameAction();
                }

                interactionKeyHeld = false;
                miniGameActionHeld = false;
                e.Handled = true;
            }

            return;
        }

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

    private void HandleMiniGameKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode is Keys.A or Keys.Left)
        {
            controller.MoveMiniGame(Direction.Left);
            e.Handled = true;
            Invalidate();
            return;
        }

        if (e.KeyCode is Keys.D or Keys.Right)
        {
            controller.MoveMiniGame(Direction.Right);
            e.Handled = true;
            Invalidate();
            return;
        }

        if (e.KeyCode == Keys.E && !interactionKeyHeld)
        {
            interactionKeyHeld = true;
            miniGameActionHeld = true;
            controller.BeginMiniGameAction();
            pendingInteractionAnimation = true;
            e.Handled = true;
            Invalidate();
            return;
        }

        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            controller.SubmitMiniGameAction();
            pendingInteractionAnimation = true;
            e.Handled = true;
            Invalidate();
        }
    }

    private void ResetRuntimeForNewShift()
    {
        moveAccumulatorMs = 0f;
        worldTickAccumulatorMs = 0f;
        interactionKeyHeld = false;
        miniGameActionHeld = false;
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
        var color = station is null ? GameTheme.Warning : GameTheme.GetStationAccent(station.Type);
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
        g.Clear(GameTheme.WindowBackground);

        var snapshot = controller.Snapshot;
        var viewport = GetViewportRect();
        var hudRect = GetHudRect(viewport);
        var objective = GamePresentation.GetObjective(snapshot);

        DrawViewportFrame(g, viewport);
        worldRenderer.Draw(g, snapshot, viewport, camera.Position, pulses, playerFrame, snapshot.IsTutorialPhase ? objective.Station : null);
        miniGameRenderer.Draw(g, snapshot.MiniGame, viewport);
        hudRenderer.Draw(g, snapshot, hudRect, objective);
        overlayRenderer.Draw(g, snapshot, ClientRectangle, overlayOpacity);
    }

    private void DrawViewportFrame(Graphics g, Rectangle viewport)
    {
        using var shadow = new SolidBrush(Color.FromArgb(34, 0, 0, 0));
        using var frame = new SolidBrush(Color.FromArgb(18, 22, 30));
        using var border = new Pen(GameTheme.ViewportBorder, 1.4f);
        g.FillRectangle(shadow, viewport.X + 6, viewport.Y + 8, viewport.Width, viewport.Height);
        g.FillRectangle(frame, viewport);
        g.DrawRectangle(border, viewport);
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
}
