namespace LosPollosHermanos.Model;

public sealed class GameWorld
{
    private static readonly GridPosition PlayerStartPosition = new(5, 17);

    private readonly ShiftSettings settings;
    private readonly IReadOnlyList<Station> stations;
    private readonly IReadOnlyList<MenuItemType> orderPattern;
    private readonly Player player;
    private int orderPatternIndex;
    private int nextOrderId = 1;

    private OrderTicket? currentOrder;
    private OrderProgress? currentProgress;
    private int customerPatienceSecondsLeft;
    private bool isShiftStarted;
    private bool isShiftRunning;
    private bool isGameOver;
    private ShiftOutcome outcome;
    private string statusMessage = "Нажмите Enter, чтобы начать смену.";

    public GameWorld(ShiftSettings? settings = null, IEnumerable<MenuItemType>? orderPattern = null)
    {
        this.settings = settings ?? new ShiftSettings();
        this.orderPattern = (orderPattern ?? DefaultOrderPattern()).ToArray();
        if (this.orderPattern.Count == 0)
        {
            throw new ArgumentException("Паттерн заказов не должен быть пустым.", nameof(orderPattern));
        }

        stations = BuildStations();
        player = new Player(PlayerStartPosition);
        TimeRemainingSeconds = this.settings.ShiftDurationSeconds;
        Rating = this.settings.InitialRating;
    }

    public int Score { get; private set; }

    public int Rating { get; private set; }

    public int Mistakes { get; private set; }

    public int ServedOrders { get; private set; }

    public int TimeRemainingSeconds { get; private set; }

    public bool IsShiftRunning => isShiftRunning;

    public bool IsGameOver => isGameOver;

    public void StartShift()
    {
        isShiftStarted = true;
        isShiftRunning = true;
        isGameOver = false;
        outcome = ShiftOutcome.None;
        statusMessage = "Смена началась. Прими заказ на стойке.";
        Score = 0;
        Mistakes = 0;
        ServedOrders = 0;
        Rating = settings.InitialRating;
        TimeRemainingSeconds = settings.ShiftDurationSeconds;
        orderPatternIndex = 0;
        nextOrderId = 1;
        player.Reset(PlayerStartPosition);
        SpawnNextOrder();
    }

    public void RestartShift()
    {
        StartShift();
    }

    public void MovePlayer(Direction direction)
    {
        if (!isShiftRunning)
        {
            return;
        }

        player.Move(direction, settings.MapWidth, settings.MapHeight);
    }

    public void Interact()
    {
        if (!isShiftRunning)
        {
            return;
        }

        var station = GetStationAtPlayer();
        if (station is null)
        {
            statusMessage = "Здесь нет рабочей станции.";
            return;
        }

        switch (station.Type)
        {
            case StationType.OrderDesk:
                AcceptCurrentOrder();
                break;
            case StationType.ServingCounter:
                ServeCurrentOrder();
                break;
            default:
                WorkAtStation(station.Type, station.Name);
                break;
        }
    }

    public void Tick()
    {
        if (!isShiftRunning)
        {
            return;
        }

        if (TimeRemainingSeconds > 0)
        {
            TimeRemainingSeconds--;
        }

        if (currentOrder is not null)
        {
            customerPatienceSecondsLeft = Math.Max(0, customerPatienceSecondsLeft - 1);
            if (customerPatienceSecondsLeft == 0)
            {
                ApplyMistake(settings.TimeoutPenalty, "Клиент не дождался заказа.");
                if (!isGameOver)
                {
                    SpawnNextOrder();
                }
            }
        }

        if (!isGameOver && TimeRemainingSeconds == 0)
        {
            FinishWithVictory();
        }
    }

    public GameSnapshot GetSnapshot()
    {
        var requiredStations = currentProgress is not null
            ? currentProgress.RequiredStations.ToArray()
            : currentOrder is not null
                ? RecipeBook.GetRequiredStations(currentOrder.Item).ToArray()
                : Array.Empty<StationType>();

        var completedStations = currentProgress?.CompletedStations.ToArray() ?? Array.Empty<StationType>();
        var currentStation = GetStationAtPlayer()?.Name;

        return new GameSnapshot
        {
            IsShiftStarted = isShiftStarted,
            IsShiftRunning = isShiftRunning,
            IsGameOver = isGameOver,
            Outcome = outcome,
            MapWidth = settings.MapWidth,
            MapHeight = settings.MapHeight,
            PlayerPosition = player.Position,
            Stations = stations.Select(x => new StationSnapshot(x.Type, x.Name, x.Position)).ToArray(),
            Score = Score,
            Rating = Rating,
            Mistakes = Mistakes,
            ServedOrders = ServedOrders,
            TimeRemainingSeconds = TimeRemainingSeconds,
            ShiftDurationSeconds = settings.ShiftDurationSeconds,
            CustomerPatienceSecondsLeft = customerPatienceSecondsLeft,
            StatusMessage = statusMessage,
            CurrentOrderName = currentOrder is null ? null : RecipeBook.GetMenuItemName(currentOrder.Item),
            IsCurrentOrderAccepted = currentProgress is not null,
            RequiredStations = requiredStations,
            CompletedStations = completedStations,
            TutorialHints = BuildTutorialHints(),
            CurrentStationName = currentStation
        };
    }

    private void AcceptCurrentOrder()
    {
        if (currentOrder is null)
        {
            statusMessage = "Сейчас нет активного клиента.";
            return;
        }

        if (currentProgress is not null)
        {
            statusMessage = "Заказ уже принят. Готовь на станциях.";
            return;
        }

        currentProgress = new OrderProgress(currentOrder);
        var required = string.Join(", ", currentProgress.RequiredStations.Select(RecipeBook.GetStationName));
        statusMessage = $"Принят заказ: {RecipeBook.GetMenuItemName(currentOrder.Item)} ({required}).";
    }

    private void WorkAtStation(StationType stationType, string stationName)
    {
        if (currentProgress is null)
        {
            statusMessage = "Сначала прими заказ у стойки.";
            return;
        }

        var result = currentProgress.ApplyStation(stationType);
        switch (result)
        {
            case StationWorkResult.NotRequired:
                statusMessage = $"Для текущего заказа станция \"{stationName}\" не нужна.";
                return;
            case StationWorkResult.AlreadyCompleted:
                statusMessage = $"Этап на станции \"{stationName}\" уже сделан.";
                return;
            case StationWorkResult.Completed:
                statusMessage = $"Этап \"{stationName}\" выполнен.";
                if (currentProgress.IsReady)
                {
                    statusMessage += " Заказ готов, неси на выдачу.";
                }

                return;
            default:
                statusMessage = "Неизвестный результат обработки станции.";
                return;
        }
    }

    private void ServeCurrentOrder()
    {
        if (currentOrder is null)
        {
            statusMessage = "Нет заказа для выдачи.";
            return;
        }

        if (currentProgress is null)
        {
            statusMessage = "Заказ еще не принят у стойки.";
            return;
        }

        if (currentProgress.IsReady)
        {
            Score += settings.CorrectServeScore;
            ServedOrders++;
            Rating = Math.Min(100, Rating + settings.SuccessfulServeRatingBonus);
            statusMessage = "Заказ выдан правильно. Клиент доволен.";
            SpawnNextOrder();
            return;
        }

        ApplyMistake(settings.WrongServePenalty, "Выдан неполный заказ.");
        if (!isGameOver)
        {
            SpawnNextOrder();
        }
    }

    private void SpawnNextOrder()
    {
        var nextItem = orderPattern[orderPatternIndex % orderPattern.Count];
        orderPatternIndex++;

        currentOrder = new OrderTicket(nextOrderId++, nextItem);
        currentProgress = null;
        customerPatienceSecondsLeft = settings.CustomerPatienceSeconds;
    }

    private void ApplyMistake(int ratingPenalty, string reason)
    {
        Mistakes++;
        Rating = Math.Max(0, Rating - ratingPenalty);
        statusMessage = $"{reason} Рейтинг -{ratingPenalty}.";

        if (Mistakes >= settings.MaxMistakes || Rating < settings.MinRatingToKeepJob)
        {
            isShiftRunning = false;
            isGameOver = true;
            outcome = ShiftOutcome.Fired;
            statusMessage = "Слишком много ошибок. Вас уволили.";
        }
    }

    private void FinishWithVictory()
    {
        isShiftRunning = false;
        isGameOver = true;
        outcome = ShiftOutcome.Victory;
        statusMessage = $"Смена закончена. Выполнено заказов: {ServedOrders}.";
    }

    private Station? GetStationAtPlayer()
    {
        return stations.FirstOrDefault(x => x.Position == player.Position);
    }

    private IReadOnlyList<string> BuildTutorialHints()
    {
        if (!isShiftStarted)
        {
            return new[]
            {
                "WASD/стрелки: движение",
                "E: взаимодействие со станцией",
                "Enter: начать смену"
            };
        }

        if (!isShiftRunning)
        {
            return Array.Empty<string>();
        }

        var elapsedSeconds = settings.ShiftDurationSeconds - TimeRemainingSeconds;
        if (elapsedSeconds > 45)
        {
            return Array.Empty<string>();
        }

        return new[]
        {
            "1) Стойка заказа -> принять заказ",
            "2) Выполнить этапы на нужных станциях",
            "3) Выдача -> отдать заказ клиенту"
        };
    }

    private static IReadOnlyList<MenuItemType> DefaultOrderPattern()
    {
        return new[]
        {
            MenuItemType.ClassicBurger,
            MenuItemType.SpicyBurger,
            MenuItemType.ComboMeal,
            MenuItemType.ClassicBurger,
            MenuItemType.ComboMeal,
            MenuItemType.SpicyBurger
        };
    }

    private static IReadOnlyList<Station> BuildStations()
    {
        return new[]
        {
            new Station(StationType.OrderDesk, RecipeBook.GetStationName(StationType.OrderDesk), new GridPosition(4, 4)),
            new Station(StationType.Grill, RecipeBook.GetStationName(StationType.Grill), new GridPosition(11, 5)),
            new Station(StationType.Assembly, RecipeBook.GetStationName(StationType.Assembly), new GridPosition(17, 9)),
            new Station(StationType.Fryer, RecipeBook.GetStationName(StationType.Fryer), new GridPosition(24, 6)),
            new Station(StationType.Drinks, RecipeBook.GetStationName(StationType.Drinks), new GridPosition(27, 12)),
            new Station(StationType.ServingCounter, RecipeBook.GetStationName(StationType.ServingCounter), new GridPosition(30, 17))
        };
    }
}
