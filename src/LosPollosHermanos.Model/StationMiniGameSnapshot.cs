namespace LosPollosHermanos.Model;

public sealed class StationMiniGameSnapshot
{
    public static StationMiniGameSnapshot None { get; } = new();

    public bool IsActive { get; init; }

    public StationMiniGameType Type { get; init; }

    public StationType? Station { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Instruction { get; init; } = string.Empty;

    public string PrimaryAction { get; init; } = string.Empty;

    public string SecondaryAction { get; init; } = string.Empty;

    public string ItemLabel { get; init; } = string.Empty;

    public float Cursor { get; init; }

    public float TargetStart { get; init; }

    public float TargetEnd { get; init; }

    public float Fill { get; init; }

    public int StepIndex { get; init; }

    public int StepCount { get; init; }

    public string Feedback { get; init; } = string.Empty;
}
