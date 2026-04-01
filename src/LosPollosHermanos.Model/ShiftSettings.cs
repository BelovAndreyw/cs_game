namespace LosPollosHermanos.Model;

public sealed class ShiftSettings
{
    public int MapWidth { get; init; } = 14;

    public int MapHeight { get; init; } = 10;

    public int ShiftDurationSeconds { get; init; } = 240;

    public int CustomerPatienceSeconds { get; init; } = 35;

    public int MaxMistakes { get; init; } = 4;

    public int MinRatingToKeepJob { get; init; } = 35;

    public int InitialRating { get; init; } = 100;

    public int CorrectServeScore { get; init; } = 100;

    public int WrongServePenalty { get; init; } = 20;

    public int TimeoutPenalty { get; init; } = 15;

    public int SuccessfulServeRatingBonus { get; init; } = 4;
}
