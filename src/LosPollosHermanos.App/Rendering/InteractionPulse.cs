namespace LosPollosHermanos.App.Rendering;

public sealed class InteractionPulse
{
    public InteractionPulse(PointF worldPosition, Color color, float durationSeconds, float maxRadius)
    {
        WorldPosition = worldPosition;
        Color = color;
        DurationSeconds = durationSeconds;
        MaxRadius = maxRadius;
    }

    public PointF WorldPosition { get; }

    public Color Color { get; }

    public float DurationSeconds { get; }

    public float MaxRadius { get; }

    public float AgeSeconds { get; private set; }

    public bool IsExpired => AgeSeconds >= DurationSeconds;

    public float Progress => DurationSeconds <= 0f ? 1f : Math.Clamp(AgeSeconds / DurationSeconds, 0f, 1f);

    public void Update(float deltaSeconds)
    {
        AgeSeconds += Math.Max(0f, deltaSeconds);
    }
}
