using System.Numerics;

namespace LosPollosHermanos.App.Rendering;

public sealed class Camera2D
{
    public Vector2 Position { get; private set; }

    public void SnapTo(Vector2 targetCenter, SizeF viewportSize, SizeF worldSize)
    {
        Position = ClampToWorld(targetCenter - ToVector(viewportSize) / 2f, viewportSize, worldSize);
    }

    public void Update(Vector2 targetCenter, SizeF viewportSize, SizeF worldSize, float deltaSeconds, float followStrength = 7f)
    {
        var desired = ClampToWorld(targetCenter - ToVector(viewportSize) / 2f, viewportSize, worldSize);
        var blend = 1f - MathF.Exp(-followStrength * MathF.Max(0f, deltaSeconds));
        Position = Vector2.Lerp(Position, desired, blend);
    }

    private static Vector2 ClampToWorld(Vector2 position, SizeF viewportSize, SizeF worldSize)
    {
        var maxX = MathF.Max(0f, worldSize.Width - viewportSize.Width);
        var maxY = MathF.Max(0f, worldSize.Height - viewportSize.Height);
        return new Vector2(
            Math.Clamp(position.X, 0f, maxX),
            Math.Clamp(position.Y, 0f, maxY));
    }

    private static Vector2 ToVector(SizeF size)
    {
        return new Vector2(size.Width, size.Height);
    }
}
