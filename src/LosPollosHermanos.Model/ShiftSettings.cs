namespace LosPollosHermanos.Model;

public sealed class ShiftSettings
{
    public int MapWidth { get; init; } = 42;

    public int MapHeight { get; init; } = 24;

    public int KitchenStartRow { get; init; } = 12;

    public int ShiftDurationSeconds { get; init; } = 300;

    public int ChefTutorialSeconds { get; init; } = 30;

    public int CustomerPatienceSeconds { get; init; } = 55;

    public int EasyPatienceBonusSeconds { get; init; } = 14;

    public int HardPatiencePenaltySeconds { get; init; } = 14;

    public int MaxMistakes { get; init; } = 4;

    public int MinRatingToKeepJob { get; init; } = 35;

    public int InitialRating { get; init; } = 100;

    public int CorrectServeScore { get; init; } = 100;

    public int WrongServePenalty { get; init; } = 20;

    public int TimeoutPenalty { get; init; } = 15;

    public int SuccessfulServeRatingBonus { get; init; } = 4;

    public float MediumPenaltyMultiplier { get; init; } = 1.2f;

    public float HardPenaltyMultiplier { get; init; } = 1.45f;
}
