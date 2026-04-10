namespace LosPollosHermanos.Model;

public sealed class GameWorld
{
    private readonly ShiftSettings settings;
    private readonly IReadOnlyList<Station> stations;
    private readonly IReadOnlyList<MenuItemType> scriptedOrderPattern;
    private readonly IReadOnlyList<CustomerProfile> customerProfiles;
    private readonly Dictionary<StationType, StationInteractionRule> interactionRules;
    private readonly Queue<CustomerProfile> waitingCustomers = new();
    private readonly Player player;

    private int orderPatternIndex;
    private int nextOrderId = 1;
    private int customerCycleIndex;
    private int customerLineIndex;
    private int serviceElapsedSeconds;

    private OrderTicket? currentOrder;
    private OrderProgress? currentProgress;
    private CustomerProfile? currentCustomer;

    private float customerPatienceSecondsLeftFloat;
    private int customerPatienceSecondsLeft;

    private bool isShiftStarted;
    private bool isShiftRunning;
    private bool isGameOver;
    private ShiftOutcome outcome;

    private bool isTutorialActive;
    private int tutorialSecondsLeft;
    private string chefMessage = "Press Enter to start your shift.";
    private StationType? tutorialTargetStation;
    private string? currentCustomerSpeech;
    private string statusMessage = "Press Enter to start your shift.";

    private StationType? interactionStation;
    private StationInteractionMode interactionMode = StationInteractionMode.None;
    private bool holdInteractionPressed;
    private float holdProgressSeconds;
    private int rapidTapCount;
    private float rapidTapWindowSeconds;
    private string interactionHint = string.Empty;

    public GameWorld(ShiftSettings? settings = null, IEnumerable<MenuItemType>? orderPattern = null)
    {
        this.settings = settings ?? new ShiftSettings();
        scriptedOrderPattern = (orderPattern ?? Array.Empty<MenuItemType>()).ToArray();

        stations = BuildStations(this.settings);
        interactionRules = BuildInteractionRules();
        customerProfiles = BuildCustomerProfiles();
        player = new Player(GetPlayerStartPosition(this.settings));
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

        Score = 0;
        Mistakes = 0;
        ServedOrders = 0;
        Rating = settings.InitialRating;
        TimeRemainingSeconds = settings.ShiftDurationSeconds;
        serviceElapsedSeconds = 0;

        orderPatternIndex = 0;
        nextOrderId = 1;
        customerCycleIndex = 0;
        customerLineIndex = 0;

        waitingCustomers.Clear();
        currentOrder = null;
        currentProgress = null;
        currentCustomer = null;
        currentCustomerSpeech = null;
        customerPatienceSecondsLeftFloat = 0f;
        customerPatienceSecondsLeft = 0;

        ResetInteractionState(clearHint: true);
        player.Reset(GetPlayerStartPosition(settings));

        isTutorialActive = settings.ChefTutorialSeconds > 0;
        tutorialSecondsLeft = settings.ChefTutorialSeconds;
        UpdateTutorialState();

        FillWaitingCustomers(minCount: 4);
        if (!isTutorialActive)
        {
            SpawnNextOrder();
        }
        else
        {
            statusMessage = "Chef: follow quick tutorial before customers arrive.";
        }
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

        var before = player.Position;
        player.Move(direction, settings.MapWidth, settings.MapHeight);
        if (player.Position != before && interactionStation is not null)
        {
            var station = GetStationAtPlayer();
            if (station is null || station.Type != interactionStation.Value)
            {
                ResetInteractionState(clearHint: false);
                interactionHint = "Interaction canceled: move back to station.";
            }
        }
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
            statusMessage = "No station here.";
            return;
        }

        ExecuteStationAction(station);
    }

    public void BeginInteraction()
    {
        if (!isShiftRunning)
        {
            return;
        }

        var station = GetStationAtPlayer();
        if (station is null)
        {
            interactionHint = "Move to station first.";
            return;
        }

        var rule = interactionRules[station.Type];
        if (rule.Mode == StationInteractionMode.Hold)
        {
            if (interactionStation != station.Type || interactionMode != StationInteractionMode.Hold)
            {
                ResetInteractionState(clearHint: true);
                interactionStation = station.Type;
                interactionMode = StationInteractionMode.Hold;
            }

            holdInteractionPressed = true;
            interactionHint = $"Hold E at {station.Name}: {holdProgressSeconds:0.0}/{rule.HoldDurationSeconds:0.0}s";
            return;
        }

        RegisterRapidTap(station, rule);
    }

    public void EndInteraction()
    {
        holdInteractionPressed = false;
    }

    public void UpdateRealtime(float deltaSeconds)
    {
        if (!isShiftRunning)
        {
            return;
        }

        var dt = Math.Max(0f, deltaSeconds);
        if (dt <= 0f)
        {
            return;
        }

        if (interactionStation is not null)
        {
            var stationAtPlayer = GetStationAtPlayer();
            if (stationAtPlayer is null || stationAtPlayer.Type != interactionStation.Value)
            {
                ResetInteractionState(clearHint: false);
                interactionHint = "Interaction canceled: move back to station.";
            }
        }

        if (interactionStation is null)
        {
            return;
        }

        var station = GetStationAtPlayer();
        if (station is null)
        {
            return;
        }

        var rule = interactionRules[station.Type];
        if (interactionMode == StationInteractionMode.Hold && holdInteractionPressed)
        {
            holdProgressSeconds += dt;
            interactionHint = $"Hold E at {station.Name}: {holdProgressSeconds:0.0}/{rule.HoldDurationSeconds:0.0}s";
            if (holdProgressSeconds >= rule.HoldDurationSeconds)
            {
                ExecuteStationAction(station);
                ResetInteractionState(clearHint: true);
            }
        }
        else if (interactionMode == StationInteractionMode.RapidTap && rapidTapCount > 0)
        {
            rapidTapWindowSeconds = Math.Max(0f, rapidTapWindowSeconds - dt);
            if (rapidTapWindowSeconds <= 0f)
            {
                rapidTapCount = 0;
                interactionHint = "Tap chain expired. Start again.";
            }
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

        if (isTutorialActive)
        {
            tutorialSecondsLeft = Math.Max(0, tutorialSecondsLeft - 1);
            UpdateTutorialState();
            if (tutorialSecondsLeft == 0)
            {
                isTutorialActive = false;
                tutorialTargetStation = null;
                chefMessage = "Service starts now. Keep your station sharp.";
                statusMessage = "Tutorial complete. First customer is waiting.";
                SpawnNextOrder();
            }
        }
        else
        {
            serviceElapsedSeconds++;
            if (currentOrder is not null)
            {
                customerPatienceSecondsLeftFloat = Math.Max(0f, customerPatienceSecondsLeftFloat - 1f);
                customerPatienceSecondsLeft = (int)Math.Ceiling(customerPatienceSecondsLeftFloat);
                if (customerPatienceSecondsLeft <= 0)
                {
                    var complaint = currentCustomer is null
                        ? "Customer left."
                        : $"{currentCustomer.Name}: {PickLine(currentCustomer.TimeoutLines)}";
                    ApplyMistake(settings.TimeoutPenalty, complaint);
                    if (!isGameOver)
                    {
                        SpawnNextOrder();
                    }
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
        var difficulty = GetCurrentDifficulty();

        var rule = interactionStation is not null
            ? interactionRules[interactionStation.Value]
            : default;

        var interactionProgress = interactionMode switch
        {
            StationInteractionMode.Hold when rule.HoldDurationSeconds > 0f =>
                Math.Clamp(holdProgressSeconds / rule.HoldDurationSeconds, 0f, 1f),
            StationInteractionMode.RapidTap when rule.RapidTapTarget > 0 =>
                Math.Clamp(rapidTapCount / (float)rule.RapidTapTarget, 0f, 1f),
            _ => 0f
        };

        return new GameSnapshot
        {
            IsShiftStarted = isShiftStarted,
            IsShiftRunning = isShiftRunning,
            IsGameOver = isGameOver,
            Outcome = outcome,

            MapWidth = settings.MapWidth,
            MapHeight = settings.MapHeight,
            KitchenStartRow = settings.KitchenStartRow,
            PlayerPosition = player.Position,
            Stations = stations.Select(x => new StationSnapshot(x.Type, x.Name, x.Position)).ToArray(),
            Npcs = BuildNpcSnapshots(),

            Score = Score,
            Rating = Rating,
            Mistakes = Mistakes,
            ServedOrders = ServedOrders,
            TimeRemainingSeconds = TimeRemainingSeconds,
            ShiftDurationSeconds = settings.ShiftDurationSeconds,
            CustomerPatienceSecondsLeft = customerPatienceSecondsLeft,
            Difficulty = difficulty,

            IsTutorialPhase = isTutorialActive,
            TutorialSecondsLeft = tutorialSecondsLeft,
            ChefMessage = chefMessage,
            TutorialTargetStation = tutorialTargetStation,

            StatusMessage = statusMessage,
            CurrentOrderName = currentOrder is null ? null : RecipeBook.GetMenuItemName(currentOrder.Item),
            CurrentCustomerName = currentCustomer?.Name,
            CurrentCustomerSpeech = currentCustomerSpeech,
            WaitingCustomerNames = waitingCustomers.Take(3).Select(x => x.Name).ToArray(),
            IsCurrentOrderAccepted = currentProgress is not null,
            RequiredStations = requiredStations,
            CompletedStations = completedStations,
            TutorialHints = BuildTutorialHints(),
            CurrentStationName = currentStation,

            InteractionMode = interactionMode,
            InteractionStation = interactionStation,
            InteractionHint = interactionHint,
            InteractionProgress = interactionProgress,
            InteractionTapCount = rapidTapCount,
            InteractionTapTarget = rule.RapidTapTarget,
            InteractionWindowSecondsLeft = rapidTapWindowSeconds
        };
    }

    private void RegisterRapidTap(Station station, StationInteractionRule rule)
    {
        if (interactionStation != station.Type || interactionMode != StationInteractionMode.RapidTap)
        {
            ResetInteractionState(clearHint: true);
            interactionStation = station.Type;
            interactionMode = StationInteractionMode.RapidTap;
        }

        if (rapidTapWindowSeconds <= 0f)
        {
            rapidTapCount = 0;
        }

        rapidTapCount++;
        rapidTapWindowSeconds = rule.RapidTapWindowSeconds;
        interactionHint = $"Tap E quickly at {station.Name}: {rapidTapCount}/{rule.RapidTapTarget}";

        if (rapidTapCount >= rule.RapidTapTarget)
        {
            ExecuteStationAction(station);
            ResetInteractionState(clearHint: true);
        }
    }

    private void ExecuteStationAction(Station station)
    {
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

    private void AcceptCurrentOrder()
    {
        if (isTutorialActive)
        {
            statusMessage = "Chef: good. Tutorial still running, keep moving.";
            return;
        }

        if (currentOrder is null)
        {
            statusMessage = "No active customer now.";
            return;
        }

        if (currentProgress is not null)
        {
            statusMessage = "Order already accepted. Continue cooking.";
            return;
        }

        currentProgress = new OrderProgress(currentOrder);
        var required = string.Join(", ", currentProgress.RequiredStations.Select(RecipeBook.GetStationName));
        statusMessage = $"Order accepted: {RecipeBook.GetMenuItemName(currentOrder.Item)} ({required}).";
    }

    private void WorkAtStation(StationType stationType, string stationName)
    {
        if (isTutorialActive)
        {
            statusMessage = $"Chef: station {stationName} checked.";
            return;
        }

        if (currentProgress is null)
        {
            statusMessage = "Accept order at desk first.";
            return;
        }

        var result = currentProgress.ApplyStation(stationType);
        switch (result)
        {
            case StationWorkResult.NotRequired:
                statusMessage = $"{stationName} is not required for this order.";
                return;
            case StationWorkResult.AlreadyCompleted:
                statusMessage = $"{stationName} is already completed.";
                return;
            case StationWorkResult.Completed:
                statusMessage = $"Stage completed: {stationName}.";
                if (currentProgress.IsReady)
                {
                    statusMessage += " Order is ready for serving counter.";
                }

                return;
            default:
                statusMessage = "Unknown station result.";
                return;
        }
    }

    private void ServeCurrentOrder()
    {
        if (isTutorialActive)
        {
            statusMessage = "Chef: serving counter is where finished orders go.";
            return;
        }

        if (currentOrder is null)
        {
            statusMessage = "No order to serve.";
            return;
        }

        if (currentProgress is null)
        {
            statusMessage = "Order is not accepted yet.";
            return;
        }

        if (currentProgress.IsReady)
        {
            var difficulty = GetCurrentDifficulty();
            var scoreBonus = difficulty switch
            {
                ShiftDifficulty.Easy => settings.CorrectServeScore,
                ShiftDifficulty.Medium => settings.CorrectServeScore + 20,
                ShiftDifficulty.Hard => settings.CorrectServeScore + 40,
                _ => settings.CorrectServeScore
            };

            Score += scoreBonus;
            ServedOrders++;
            Rating = Math.Min(100, Rating + settings.SuccessfulServeRatingBonus);

            var reaction = currentCustomer is null
                ? "Order served successfully."
                : $"{currentCustomer.Name}: {PickLine(currentCustomer.SuccessLines)}";
            statusMessage = reaction;

            SpawnNextOrder();
            return;
        }

        var complaint = currentCustomer is null
            ? "Incomplete order served."
            : $"{currentCustomer.Name}: {PickLine(currentCustomer.FailureLines)}";
        ApplyMistake(settings.WrongServePenalty, complaint);
        if (!isGameOver)
        {
            SpawnNextOrder();
        }
    }

    private void SpawnNextOrder()
    {
        if (isTutorialActive)
        {
            currentOrder = null;
            currentProgress = null;
            currentCustomer = null;
            currentCustomerSpeech = null;
            customerPatienceSecondsLeftFloat = 0f;
            customerPatienceSecondsLeft = 0;
            return;
        }

        FillWaitingCustomers(minCount: 4);
        currentCustomer = waitingCustomers.Dequeue();
        FillWaitingCustomers(minCount: 4);

        var difficulty = GetCurrentDifficulty();
        var item = NextMenuItemForCurrentCustomer(difficulty);

        currentOrder = new OrderTicket(nextOrderId++, item);
        currentProgress = null;
        customerPatienceSecondsLeftFloat = GetPatienceForDifficulty(difficulty);
        customerPatienceSecondsLeft = (int)Math.Ceiling(customerPatienceSecondsLeftFloat);
        currentCustomerSpeech = PickLine(currentCustomer.GreetingLines);
        statusMessage = $"{currentCustomer.Name}: {currentCustomerSpeech}";
    }

    private void FillWaitingCustomers(int minCount)
    {
        while (waitingCustomers.Count < minCount)
        {
            waitingCustomers.Enqueue(customerProfiles[customerCycleIndex % customerProfiles.Count]);
            customerCycleIndex++;
        }
    }

    private MenuItemType NextMenuItemForCurrentCustomer(ShiftDifficulty difficulty)
    {
        if (scriptedOrderPattern.Count > 0)
        {
            var item = scriptedOrderPattern[orderPatternIndex % scriptedOrderPattern.Count];
            orderPatternIndex++;
            return item;
        }

        var allowedByDifficulty = GetAllowedOrdersByDifficulty(difficulty);
        if (currentCustomer is null)
        {
            var fallback = allowedByDifficulty[orderPatternIndex % allowedByDifficulty.Count];
            orderPatternIndex++;
            return fallback;
        }

        var preferred = currentCustomer.PreferredOrders
            .Where(allowedByDifficulty.Contains)
            .ToArray();
        var source = preferred.Length > 0 ? preferred : allowedByDifficulty.ToArray();
        var itemIndex = orderPatternIndex % source.Length;
        orderPatternIndex++;
        return source[itemIndex];
    }

    private IReadOnlyList<MenuItemType> GetAllowedOrdersByDifficulty(ShiftDifficulty difficulty)
    {
        return difficulty switch
        {
            ShiftDifficulty.Easy => new[] { MenuItemType.ClassicBurger },
            ShiftDifficulty.Medium => new[] { MenuItemType.ClassicBurger, MenuItemType.SpicyBurger },
            ShiftDifficulty.Hard => new[] { MenuItemType.SpicyBurger, MenuItemType.ComboMeal },
            _ => new[] { MenuItemType.ClassicBurger }
        };
    }

    private void ApplyMistake(int baseRatingPenalty, string reason)
    {
        Mistakes++;
        var penalty = ScalePenaltyByDifficulty(baseRatingPenalty);
        Rating = Math.Max(0, Rating - penalty);
        statusMessage = $"{reason} Rating -{penalty}.";

        if (Mistakes >= settings.MaxMistakes || Rating < settings.MinRatingToKeepJob)
        {
            isShiftRunning = false;
            isGameOver = true;
            outcome = ShiftOutcome.Fired;
            statusMessage = "Too many mistakes. You are fired.";
        }
    }

    private int ScalePenaltyByDifficulty(int basePenalty)
    {
        var multiplier = GetCurrentDifficulty() switch
        {
            ShiftDifficulty.Easy => 1f,
            ShiftDifficulty.Medium => settings.MediumPenaltyMultiplier,
            ShiftDifficulty.Hard => settings.HardPenaltyMultiplier,
            _ => 1f
        };

        return (int)Math.Round(basePenalty * multiplier, MidpointRounding.AwayFromZero);
    }

    private int GetPatienceForDifficulty(ShiftDifficulty difficulty)
    {
        return difficulty switch
        {
            ShiftDifficulty.Easy => settings.CustomerPatienceSeconds + settings.EasyPatienceBonusSeconds,
            ShiftDifficulty.Medium => settings.CustomerPatienceSeconds,
            ShiftDifficulty.Hard => Math.Max(14, settings.CustomerPatienceSeconds - settings.HardPatiencePenaltySeconds),
            _ => settings.CustomerPatienceSeconds
        };
    }

    private void FinishWithVictory()
    {
        isShiftRunning = false;
        isGameOver = true;
        outcome = ShiftOutcome.Victory;
        statusMessage = $"Shift finished. Served orders: {ServedOrders}.";
    }

    private Station? GetStationAtPlayer()
    {
        return stations.FirstOrDefault(x => x.Position == player.Position);
    }

    private void UpdateTutorialState()
    {
        if (!isTutorialActive)
        {
            tutorialTargetStation = null;
            return;
        }

        if (tutorialSecondsLeft > 22)
        {
            chefMessage = "Chef: move to order desk and get comfortable.";
            tutorialTargetStation = StationType.OrderDesk;
            return;
        }

        if (tutorialSecondsLeft > 14)
        {
            chefMessage = "Chef: each station needs hold or rapid taps on E.";
            tutorialTargetStation = StationType.Grill;
            return;
        }

        if (tutorialSecondsLeft > 7)
        {
            chefMessage = "Chef: assembly is fast tapping, keep rhythm.";
            tutorialTargetStation = StationType.Assembly;
            return;
        }

        chefMessage = "Chef: final check at serving counter. Service starts soon.";
        tutorialTargetStation = StationType.ServingCounter;
    }

    private IReadOnlyList<string> BuildTutorialHints()
    {
        if (!isShiftStarted)
        {
            return new[]
            {
                "WASD/arrows: move",
                "E: hold or rapid tap to interact",
                "Enter: start shift"
            };
        }

        if (isTutorialActive)
        {
            return new[]
            {
                "Chef tutorial in progress",
                $"Time left: {tutorialSecondsLeft}s",
                "Follow highlighted station"
            };
        }

        if (!isShiftRunning)
        {
            return Array.Empty<string>();
        }

        if (serviceElapsedSeconds < 35)
        {
            return new[]
            {
                "1) Accept order at desk",
                "2) Complete required stations",
                "3) Serve at counter before patience runs out"
            };
        }

        return Array.Empty<string>();
    }

    private ShiftDifficulty GetCurrentDifficulty()
    {
        if (isTutorialActive || !isShiftStarted)
        {
            return ShiftDifficulty.Easy;
        }

        var serviceWindow = Math.Max(1, settings.ShiftDurationSeconds - settings.ChefTutorialSeconds);
        var progress = serviceElapsedSeconds / (float)serviceWindow;
        if (progress < 0.35f)
        {
            return ShiftDifficulty.Easy;
        }

        if (progress < 0.75f)
        {
            return ShiftDifficulty.Medium;
        }

        return ShiftDifficulty.Hard;
    }

    private IReadOnlyList<NpcSnapshot> BuildNpcSnapshots()
    {
        var npcs = new List<NpcSnapshot>
        {
            new("Chef Gustavo", NpcRole.Chef, GetChefPosition(settings), isTutorialActive ? chefMessage : "Chef: keep tickets flowing.")
        };

        var deskPosition = stations.Single(x => x.Type == StationType.OrderDesk).Position;
        if (currentCustomer is not null)
        {
            npcs.Add(new NpcSnapshot(currentCustomer.Name, NpcRole.Customer, deskPosition, currentCustomerSpeech));
        }

        var queueY = deskPosition.Y;
        var queueStartX = deskPosition.X + 3;
        var index = 0;
        foreach (var customer in waitingCustomers.Take(3))
        {
            npcs.Add(new NpcSnapshot(customer.Name, NpcRole.Customer, new GridPosition(queueStartX + index * 3, queueY), null));
            index++;
        }

        return npcs;
    }

    private string PickLine(IReadOnlyList<string> lines)
    {
        if (lines.Count == 0)
        {
            return string.Empty;
        }

        var line = lines[customerLineIndex % lines.Count];
        customerLineIndex++;
        return line;
    }

    private void ResetInteractionState(bool clearHint)
    {
        interactionStation = null;
        interactionMode = StationInteractionMode.None;
        holdInteractionPressed = false;
        holdProgressSeconds = 0f;
        rapidTapCount = 0;
        rapidTapWindowSeconds = 0f;
        if (clearHint)
        {
            interactionHint = string.Empty;
        }
    }

    private static GridPosition GetPlayerStartPosition(ShiftSettings settings)
    {
        return new GridPosition(settings.MapWidth / 2, settings.KitchenStartRow + 8);
    }

    private static GridPosition GetChefPosition(ShiftSettings settings)
    {
        return new GridPosition(settings.MapWidth / 2 - 8, settings.KitchenStartRow + 2);
    }

    private static IReadOnlyList<Station> BuildStations(ShiftSettings settings)
    {
        var centerX = settings.MapWidth / 2;
        var kitchenRow = settings.KitchenStartRow;
        return new[]
        {
            new Station(StationType.OrderDesk, RecipeBook.GetStationName(StationType.OrderDesk), new GridPosition(centerX, kitchenRow - 3)),
            new Station(StationType.ServingCounter, RecipeBook.GetStationName(StationType.ServingCounter), new GridPosition(centerX, kitchenRow - 1)),
            new Station(StationType.Grill, RecipeBook.GetStationName(StationType.Grill), new GridPosition(centerX - 9, kitchenRow + 4)),
            new Station(StationType.Fryer, RecipeBook.GetStationName(StationType.Fryer), new GridPosition(centerX - 4, kitchenRow + 4)),
            new Station(StationType.Assembly, RecipeBook.GetStationName(StationType.Assembly), new GridPosition(centerX + 1, kitchenRow + 4)),
            new Station(StationType.Drinks, RecipeBook.GetStationName(StationType.Drinks), new GridPosition(centerX + 7, kitchenRow + 4))
        };
    }

    private static Dictionary<StationType, StationInteractionRule> BuildInteractionRules()
    {
        return new Dictionary<StationType, StationInteractionRule>
        {
            [StationType.OrderDesk] = new(StationInteractionMode.Hold, 1.2f, 0, 0f),
            [StationType.Grill] = new(StationInteractionMode.Hold, 1.9f, 0, 0f),
            [StationType.Fryer] = new(StationInteractionMode.Hold, 2.2f, 0, 0f),
            [StationType.ServingCounter] = new(StationInteractionMode.Hold, 1.4f, 0, 0f),
            [StationType.Assembly] = new(StationInteractionMode.RapidTap, 0f, 6, 2.0f),
            [StationType.Drinks] = new(StationInteractionMode.RapidTap, 0f, 5, 2.0f)
        };
    }

    private static IReadOnlyList<CustomerProfile> BuildCustomerProfiles()
    {
        return new[]
        {
            new CustomerProfile(
                "Alex",
                new[] { MenuItemType.ClassicBurger, MenuItemType.SpicyBurger },
                new[] { "One burger and make it quick.", "Night shift fuel, please." },
                new[] { "This is not what I ordered.", "I asked for complete order." },
                new[] { "Perfect, thanks.", "That was quick." },
                new[] { "I cannot wait forever.", "Queue is too long." }),
            new CustomerProfile(
                "Mia",
                new[] { MenuItemType.SpicyBurger, MenuItemType.ComboMeal },
                new[] { "Can I get spicy combo vibes?", "Need something hot and loud." },
                new[] { "Nope, this order is wrong.", "Spice missing. Big miss." },
                new[] { "Now that is a proper order.", "Good tempo, chef." },
                new[] { "Timer ran out for me.", "I am leaving hungry." }),
            new CustomerProfile(
                "Victor",
                new[] { MenuItemType.ComboMeal, MenuItemType.ClassicBurger },
                new[] { "Combo and drink, please.", "I have five minutes only." },
                new[] { "Incomplete tray, not good.", "This service slipped." },
                new[] { "Solid execution.", "Great, I am happy." },
                new[] { "I am late. Goodbye.", "No time left, I am out." }),
            new CustomerProfile(
                "Nora",
                new[] { MenuItemType.ClassicBurger, MenuItemType.ComboMeal },
                new[] { "Hi, surprise me with speed.", "I will rate this place tonight." },
                new[] { "That is a rough mistake.", "I expected better." },
                new[] { "Five stars for this one.", "Fast and clean service." },
                new[] { "I am done waiting.", "This queue defeated me." })
        };
    }

    private readonly record struct StationInteractionRule(
        StationInteractionMode Mode,
        float HoldDurationSeconds,
        int RapidTapTarget,
        float RapidTapWindowSeconds);

    private sealed record CustomerProfile(
        string Name,
        IReadOnlyList<MenuItemType> PreferredOrders,
        IReadOnlyList<string> GreetingLines,
        IReadOnlyList<string> FailureLines,
        IReadOnlyList<string> SuccessLines,
        IReadOnlyList<string> TimeoutLines);
}
