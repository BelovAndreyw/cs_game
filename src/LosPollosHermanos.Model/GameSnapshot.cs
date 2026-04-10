namespace LosPollosHermanos.Model;

public sealed class GameSnapshot
{
    public bool IsShiftStarted { get; init; }

    public bool IsShiftRunning { get; init; }

    public bool IsGameOver { get; init; }

    public ShiftOutcome Outcome { get; init; }

    public int MapWidth { get; init; }

    public int MapHeight { get; init; }

    public int KitchenStartRow { get; init; }

    public GridPosition PlayerPosition { get; init; }

    public IReadOnlyList<StationSnapshot> Stations { get; init; } = Array.Empty<StationSnapshot>();

    public int Score { get; init; }

    public int Rating { get; init; }

    public int Mistakes { get; init; }

    public int ServedOrders { get; init; }

    public int TimeRemainingSeconds { get; init; }

    public int ShiftDurationSeconds { get; init; }

    public int CustomerPatienceSecondsLeft { get; init; }

    public ShiftDifficulty Difficulty { get; init; }

    public bool IsTutorialPhase { get; init; }

    public int TutorialSecondsLeft { get; init; }

    public string ChefMessage { get; init; } = string.Empty;

    public StationType? TutorialTargetStation { get; init; }

    public string StatusMessage { get; init; } = string.Empty;

    public string? CurrentOrderName { get; init; }

    public string? CurrentCustomerName { get; init; }

    public string? CurrentCustomerSpeech { get; init; }

    public IReadOnlyList<string> WaitingCustomerNames { get; init; } = Array.Empty<string>();

    public bool IsCurrentOrderAccepted { get; init; }

    public IReadOnlyList<StationType> RequiredStations { get; init; } = Array.Empty<StationType>();

    public IReadOnlyList<StationType> CompletedStations { get; init; } = Array.Empty<StationType>();

    public IReadOnlyList<string> TutorialHints { get; init; } = Array.Empty<string>();

    public string? CurrentStationName { get; init; }

    public StationInteractionMode InteractionMode { get; init; }

    public StationType? InteractionStation { get; init; }

    public string InteractionHint { get; init; } = string.Empty;

    public float InteractionProgress { get; init; }

    public int InteractionTapCount { get; init; }

    public int InteractionTapTarget { get; init; }

    public float InteractionWindowSecondsLeft { get; init; }

    public IReadOnlyList<NpcSnapshot> Npcs { get; init; } = Array.Empty<NpcSnapshot>();
}
