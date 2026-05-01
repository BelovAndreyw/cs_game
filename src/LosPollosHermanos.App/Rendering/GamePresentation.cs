using LosPollosHermanos.Model;

namespace LosPollosHermanos.App.Rendering;

public readonly record struct GameObjective(StationType? Station, string Title, string Description);

public readonly record struct OrderStepStatus(string Label, bool IsCompleted);

public static class GamePresentation
{
    public static GameObjective GetObjective(GameSnapshot snapshot)
    {
        if (!snapshot.IsShiftStarted && !snapshot.IsGameOver)
        {
            return new GameObjective(null, "Открыть смену", "Нажмите Enter.");
        }

        if (snapshot.IsGameOver)
        {
            return new GameObjective(null, "Смена закрыта", snapshot.StatusMessage);
        }

        if (snapshot.IsTutorialPhase)
        {
            var title = snapshot.TutorialTargetStation is null
                ? "Обучение"
                : $"Шаг: {RecipeBook.GetStationName(snapshot.TutorialTargetStation.Value)}";
            return new GameObjective(snapshot.TutorialTargetStation, title, snapshot.ChefMessage);
        }

        if (snapshot.MiniGame.IsActive)
        {
            return new GameObjective(snapshot.MiniGame.Station, snapshot.MiniGame.Title, snapshot.MiniGame.Instruction);
        }

        if (snapshot.CurrentOrderName is null)
        {
            return new GameObjective(null, "Ждём гостя", "Следующий клиент уже идёт.");
        }

        if (!snapshot.IsCurrentOrderAccepted)
        {
            return new GameObjective(StationType.OrderDesk, "Принять заказ", "Подойдите к кассе и держите E.");
        }

        var completedCounts = snapshot.CompletedStations
            .GroupBy(station => station)
            .ToDictionary(group => group.Key, group => group.Count());
        foreach (var group in snapshot.RequiredStations.GroupBy(station => station))
        {
            completedCounts.TryGetValue(group.Key, out var completed);
            if (completed < group.Count())
            {
                var title = group.Count() == 1
                    ? RecipeBook.GetStationName(group.Key)
                    : $"{RecipeBook.GetStationName(group.Key)} {completed + 1}/{group.Count()}";
                return new GameObjective(group.Key, title, "Следующий шаг по линии.");
            }
        }

        return new GameObjective(StationType.ServingCounter, "Выдать заказ", "Заказ готов. Несите его на выдачу.");
    }

    public static IReadOnlyList<OrderStepStatus> BuildOrderSteps(GameSnapshot snapshot)
    {
        if (snapshot.CurrentOrderName is null)
        {
            return Array.Empty<OrderStepStatus>();
        }

        var completedCounts = snapshot.CompletedStations
            .GroupBy(station => station)
            .ToDictionary(group => group.Key, group => group.Count());
        return snapshot.RequiredStations
            .GroupBy(station => station)
            .Select(group =>
            {
                completedCounts.TryGetValue(group.Key, out var completed);
                var required = group.Count();
                var label = required == 1
                    ? RecipeBook.GetStationName(group.Key)
                    : $"{RecipeBook.GetStationName(group.Key)} {completed}/{required}";
                return new OrderStepStatus(label, completed >= required);
            })
            .ToArray();
    }

    public static float GetShiftProgress(GameSnapshot snapshot)
    {
        if (snapshot.ShiftDurationSeconds <= 0)
        {
            return 0f;
        }

        return Math.Clamp(snapshot.TimeRemainingSeconds / (float)snapshot.ShiftDurationSeconds, 0f, 1f);
    }

    public static float GetPatienceProgress(GameSnapshot snapshot)
    {
        if (snapshot.CustomerPatienceMaxSeconds <= 0)
        {
            return 0f;
        }

        return Math.Clamp(snapshot.CustomerPatienceSecondsLeft / (float)snapshot.CustomerPatienceMaxSeconds, 0f, 1f);
    }

    public static string FormatDifficulty(ShiftDifficulty difficulty)
    {
        return difficulty switch
        {
            ShiftDifficulty.Easy => "Разогрев",
            ShiftDifficulty.Medium => "Запара",
            ShiftDifficulty.Hard => "Пекло",
            _ => "Неизвестно"
        };
    }

    public static string FormatTime(int totalSeconds)
    {
        var time = TimeSpan.FromSeconds(Math.Max(0, totalSeconds));
        return $"{time.Minutes:00}:{time.Seconds:00}";
    }

    public static string BuildInteractionTitle(GameSnapshot snapshot)
    {
        if (snapshot.MiniGame.IsActive)
        {
            return snapshot.MiniGame.Title;
        }

        if (snapshot.InteractionMode == StationInteractionMode.Hold)
        {
            return "Держите E";
        }

        if (snapshot.InteractionMode == StationInteractionMode.RapidTap)
        {
            return "Жмите E";
        }

        return snapshot.IsTutorialPhase ? "Слушайте шефа" : "Подойдите к станции";
    }

    public static string BuildInteractionCaption(GameSnapshot snapshot)
    {
        if (snapshot.MiniGame.IsActive)
        {
            return snapshot.MiniGame.PrimaryAction;
        }

        if (snapshot.InteractionMode == StationInteractionMode.Hold)
        {
            return $"Готово: {Math.Round(snapshot.InteractionProgress * 100f)}%.";
        }

        if (snapshot.InteractionMode == StationInteractionMode.RapidTap && snapshot.InteractionTapTarget > 0)
        {
            return $"{snapshot.InteractionTapCount}/{snapshot.InteractionTapTarget} нажатий.";
        }

        if (!string.IsNullOrWhiteSpace(snapshot.InteractionHint))
        {
            return snapshot.InteractionHint;
        }

        return snapshot.IsShiftRunning
            ? "E — действие на станции."
            : "Смена пока не началась.";
    }
}
