using LosPollosHermanos.Model;

namespace LosPollosHermanos.App.Rendering;

public enum PlayerAnimationMode
{
    Idle,
    Walk,
    Work
}

public readonly record struct PlayerAnimationFrame(PlayerAnimationMode Mode, int Frame, Direction Facing);

public sealed class PlayerAnimator
{
    private float idleClock;
    private float walkClock;
    private float workClock;
    private float workTimer;
    private Direction facing = Direction.Down;

    public PlayerAnimationFrame Update(
        float deltaSeconds,
        bool movedThisFrame,
        bool triggeredInteraction,
        Direction? movementDirection)
    {
        if (movementDirection is not null)
        {
            facing = movementDirection.Value;
        }

        var dt = Math.Max(0f, deltaSeconds);
        idleClock += dt;

        if (movedThisFrame)
        {
            walkClock += dt;
        }

        if (triggeredInteraction)
        {
            workTimer = 0.32f;
            workClock = 0f;
        }

        if (workTimer > 0f)
        {
            workTimer = Math.Max(0f, workTimer - dt);
            workClock += dt;
            return new PlayerAnimationFrame(PlayerAnimationMode.Work, FrameByClock(workClock, 10f, 3), facing);
        }

        if (movedThisFrame)
        {
            return new PlayerAnimationFrame(PlayerAnimationMode.Walk, FrameByClock(walkClock, 8f, 4), facing);
        }

        return new PlayerAnimationFrame(PlayerAnimationMode.Idle, FrameByClock(idleClock, 1.8f, 2), facing);
    }

    private static int FrameByClock(float clock, float fps, int frameCount)
    {
        if (frameCount <= 0)
        {
            return 0;
        }

        return (int)(MathF.Floor(clock * fps) % frameCount);
    }
}
