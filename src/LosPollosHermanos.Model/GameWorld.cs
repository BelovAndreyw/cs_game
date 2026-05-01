namespace LosPollosHermanos.Model;

public sealed class GameWorld
{
    private readonly ShiftSettings settings;
    private readonly IReadOnlyList<Station> stations;
    private readonly IReadOnlyList<ScenePropSnapshot> sceneProps;
    private readonly HashSet<GridPosition> blockedTiles;
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
    private int customerPatienceMaxSeconds;

    private bool isShiftStarted;
    private bool isShiftRunning;
    private bool isGameOver;
    private ShiftOutcome outcome;

    private bool isTutorialActive;
    private int tutorialSecondsLeft;
    private TutorialStep tutorialStep;
    private string chefMessage = "Нажмите Enter, чтобы начать смену.";
    private StationType? tutorialTargetStation;
    private string? currentCustomerSpeech;
    private string statusMessage = "Нажмите Enter, чтобы начать смену.";

    private StationType? interactionStation;
    private StationInteractionMode interactionMode = StationInteractionMode.None;
    private bool holdInteractionPressed;
    private float holdProgressSeconds;
    private int rapidTapCount;
    private float rapidTapWindowSeconds;
    private string interactionHint = string.Empty;
    private StationMiniGameState? miniGame;

    public GameWorld(ShiftSettings? settings = null, IEnumerable<MenuItemType>? orderPattern = null)
    {
        this.settings = settings ?? new ShiftSettings();
        scriptedOrderPattern = (orderPattern ?? Array.Empty<MenuItemType>()).ToArray();

        stations = BuildStations(this.settings);
        sceneProps = BuildSceneProps(this.settings);
        blockedTiles = BuildBlockedTiles(this.settings, sceneProps, stations);
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
        customerPatienceMaxSeconds = 0;

        ResetInteractionState(clearHint: true);
        miniGame = null;
        player.Reset(GetPlayerStartPosition(settings));

        isTutorialActive = settings.ChefTutorialSeconds > 0;
        tutorialStep = isTutorialActive ? TutorialStep.OrderDesk : TutorialStep.None;
        tutorialSecondsLeft = isTutorialActive ? GetRemainingTutorialSteps() : 0;
        UpdateTutorialState();

        FillWaitingCustomers(minCount: 4);
        if (!isTutorialActive)
        {
            SpawnNextOrder();
        }

        statusMessage = isTutorialActive ? chefMessage : "Смена началась. Первый гость уже у кассы.";
    }

    public void RestartShift()
    {
        StartShift();
    }

    public void MovePlayer(Direction direction)
    {
        if (!isShiftRunning || miniGame is not null)
        {
            return;
        }

        var next = player.Position.Move(direction);
        if (next.X < 0 || next.Y < 0 || next.X >= settings.MapWidth || next.Y >= settings.MapHeight || blockedTiles.Contains(next))
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
                interactionHint = "Действие отменено: вернитесь к станции.";
            }
        }
    }

    public void Interact()
    {
        if (!isShiftRunning)
        {
            return;
        }

        if (miniGame is not null)
        {
            SubmitMiniGameAction();
            return;
        }

        var station = GetStationAtPlayer();
        if (station is null)
        {
            statusMessage = "Здесь нет рабочей станции.";
            return;
        }

        if (TryStartStationMiniGame(station))
        {
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

        if (miniGame is not null)
        {
            BeginMiniGameAction();
            return;
        }

        var station = GetStationAtPlayer();
        if (station is null)
        {
            interactionHint = "Сначала подойдите к станции.";
            return;
        }

        if (TryStartStationMiniGame(station))
        {
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
            interactionHint = $"Держите E: {Math.Round((holdProgressSeconds / Math.Max(0.1f, rule.HoldDurationSeconds)) * 100f)}%.";
            return;
        }

        RegisterRapidTap(station, rule);
    }

    public void EndInteraction()
    {
        holdInteractionPressed = false;
    }

    public void BeginMiniGameAction()
    {
        if (miniGame is null)
        {
            return;
        }

        if (miniGame.Type == StationMiniGameType.DrinksFill)
        {
            miniGame.IsHolding = true;
            miniGame.Feedback = "Отпустите E в зелёной зоне.";
            return;
        }

        SubmitMiniGameAction();
    }

    public void EndMiniGameAction()
    {
        if (miniGame is null)
        {
            return;
        }

        if (miniGame.Type == StationMiniGameType.DrinksFill)
        {
            if (!miniGame.IsHolding)
            {
                return;
            }

            miniGame.IsHolding = false;
            SubmitMiniGameAction();
        }
    }

    public void MoveMiniGame(Direction direction)
    {
        if (miniGame is null)
        {
            return;
        }

        var delta = direction switch
        {
            Direction.Left => -0.08f,
            Direction.Right => 0.08f,
            _ => 0f
        };

        if (delta == 0f)
        {
            return;
        }

        if (miniGame.Type is StationMiniGameType.FryerDrop or StationMiniGameType.ServingPack)
        {
            miniGame.Cursor = Math.Clamp(miniGame.Cursor + delta, 0f, 1f);
            miniGame.Feedback = "Совместите предмет с зелёной зоной и нажмите E.";
        }
    }

    public void SubmitMiniGameAction()
    {
        if (miniGame is null)
        {
            return;
        }

        switch (miniGame.Type)
        {
            case StationMiniGameType.GrillTiming:
            case StationMiniGameType.FryerDrop:
            case StationMiniGameType.ServingPack:
                if (IsMiniGameCursorInTarget(miniGame))
                {
                    CompleteMiniGame(success: true);
                    return;
                }

                CompleteMiniGame(success: false);
                return;
            case StationMiniGameType.AssemblyStack:
                miniGame.StepIndex++;
                miniGame.Feedback = miniGame.StepIndex >= miniGame.StepCount
                    ? "Готово."
                    : $"Слой {miniGame.StepIndex}/{miniGame.StepCount}. Продолжайте.";
                if (miniGame.StepIndex >= miniGame.StepCount)
                {
                    CompleteMiniGame(success: true);
                }

                return;
            case StationMiniGameType.DrinksFill:
                if (miniGame.Fill >= miniGame.TargetStart && miniGame.Fill <= miniGame.TargetEnd)
                {
                    CompleteMiniGame(success: true);
                    return;
                }

                CompleteMiniGame(success: false);
                return;
        }
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

        if (miniGame is not null)
        {
            UpdateMiniGame(dt);
            return;
        }

        if (interactionStation is not null)
        {
            var stationAtPlayer = GetStationAtPlayer();
            if (stationAtPlayer is null || stationAtPlayer.Type != interactionStation.Value)
            {
                ResetInteractionState(clearHint: false);
                interactionHint = "Действие отменено: вернитесь к станции.";
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
            interactionHint = $"Держите E: {Math.Round(Math.Clamp(holdProgressSeconds / Math.Max(0.1f, rule.HoldDurationSeconds), 0f, 1f) * 100f)}%.";
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
                interactionHint = "Ритм сбился, начните снова.";
            }
        }
    }

    public void Tick()
    {
        if (!isShiftRunning)
        {
            return;
        }

        if (isTutorialActive)
        {
            tutorialSecondsLeft = GetRemainingTutorialSteps();
            UpdateTutorialState();
            return;
        }

        if (TimeRemainingSeconds > 0)
        {
            TimeRemainingSeconds--;
        }

        if (!isGameOver && TimeRemainingSeconds == 0)
        {
            FinishWithVictory();
            return;
        }

        serviceElapsedSeconds++;
        if (currentOrder is null)
        {
            return;
        }

        customerPatienceSecondsLeftFloat = Math.Max(0f, customerPatienceSecondsLeftFloat - 1f);
        customerPatienceSecondsLeft = (int)Math.Ceiling(customerPatienceSecondsLeftFloat);
        if (customerPatienceSecondsLeft > 0)
        {
            return;
        }

        var complaint = currentCustomer is null
            ? "Клиент ушел."
            : $"{currentCustomer.Name}: {PickLine(currentCustomer.TimeoutLines)}";
        ApplyMistake(settings.TimeoutPenalty, complaint);
        if (!isGameOver)
        {
            SpawnNextOrder();
        }
    }

    public GameSnapshot GetSnapshot()
    {
        var requiredStations = currentProgress is not null
            ? currentProgress.RequiredStations.ToArray()
            : currentOrder is not null
                ? RecipeBook.GetRequiredStationSequence(currentOrder.Items).ToArray()
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
            SceneProps = sceneProps,
            BlockedTiles = blockedTiles.OrderBy(x => x.Y).ThenBy(x => x.X).ToArray(),
            Npcs = BuildNpcSnapshots(),

            Score = Score,
            Rating = Rating,
            Mistakes = Mistakes,
            MaxMistakes = settings.MaxMistakes,
            ServedOrders = ServedOrders,
            TimeRemainingSeconds = TimeRemainingSeconds,
            ShiftDurationSeconds = settings.ShiftDurationSeconds,
            CustomerPatienceSecondsLeft = customerPatienceSecondsLeft,
            CustomerPatienceMaxSeconds = customerPatienceMaxSeconds,
            Difficulty = difficulty,

            IsTutorialPhase = isTutorialActive,
            TutorialSecondsLeft = tutorialSecondsLeft,
            ChefMessage = chefMessage,
            TutorialTargetStation = tutorialTargetStation,

            StatusMessage = statusMessage,
            CurrentOrderItem = currentOrder?.Item,
            CurrentOrderItems = currentOrder?.Items.ToArray() ?? Array.Empty<MenuItemType>(),
            CurrentOrderName = currentOrder is null ? null : RecipeBook.GetOrderName(currentOrder.Items),
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
            InteractionWindowSecondsLeft = rapidTapWindowSeconds,
            MiniGame = BuildMiniGameSnapshot()
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
        interactionHint = $"Ритм: {rapidTapCount}/{rule.RapidTapTarget}.";

        if (rapidTapCount >= rule.RapidTapTarget)
        {
            ExecuteStationAction(station);
            ResetInteractionState(clearHint: true);
        }
    }

    private bool TryStartStationMiniGame(Station station)
    {
        if (isTutorialActive || station.Type == StationType.OrderDesk)
        {
            return false;
        }

        if (station.Type == StationType.ServingCounter)
        {
            if (currentProgress is null || !currentProgress.IsReady)
            {
                return false;
            }

            StartMiniGame(station.Type);
            return true;
        }

        if (currentProgress is null)
        {
            statusMessage = "Сначала примите заказ на стойке.";
            return true;
        }

        if (!currentProgress.RequiresStation(station.Type))
        {
            statusMessage = $"Станция \"{station.Name}\" не нужна для текущего заказа.";
            return true;
        }

        if (!currentProgress.RequiresMore(station.Type))
        {
            statusMessage = $"Этап на станции \"{station.Name}\" уже выполнен.";
            return true;
        }

        StartMiniGame(station.Type);
        return true;
    }

    private void StartMiniGame(StationType stationType)
    {
        ResetInteractionState(clearHint: true);
        miniGame = StationMiniGameState.Create(stationType);
        statusMessage = $"{RecipeBook.GetStationName(stationType)}: выполните мини-игру.";
    }

    private void UpdateMiniGame(float deltaSeconds)
    {
        if (miniGame is null)
        {
            return;
        }

        switch (miniGame.Type)
        {
            case StationMiniGameType.GrillTiming:
                miniGame.Cursor += miniGame.Direction * deltaSeconds * 0.78f;
                if (miniGame.Cursor >= 1f)
                {
                    miniGame.Cursor = 1f;
                    miniGame.Direction = -1f;
                }
                else if (miniGame.Cursor <= 0f)
                {
                    miniGame.Cursor = 0f;
                    miniGame.Direction = 1f;
                }

                break;
            case StationMiniGameType.DrinksFill:
                if (miniGame.IsHolding)
                {
                    miniGame.Fill = Math.Clamp(miniGame.Fill + deltaSeconds * 0.52f, 0f, 1.08f);
                    if (miniGame.Fill >= 1f)
                    {
                        CompleteMiniGame(success: false);
                    }
                }

                break;
        }
    }

    private void CompleteMiniGame(bool success)
    {
        if (miniGame is null)
        {
            return;
        }

        var stationType = miniGame.Station;
        if (!success)
        {
            statusMessage = BuildMiniGameFailureMessage(miniGame.Type);
            miniGame = null;
            ResetInteractionState(clearHint: true);
            return;
        }

        miniGame = null;
        ResetInteractionState(clearHint: true);
        ExecuteStationAction(stations.Single(station => station.Type == stationType));
    }

    private StationMiniGameSnapshot BuildMiniGameSnapshot()
    {
        if (miniGame is null)
        {
            return StationMiniGameSnapshot.None;
        }

        var (title, instruction, primary, secondary, item) = miniGame.Type switch
        {
            StationMiniGameType.GrillTiming => (
                "Гриль",
                "Поймайте момент: нажмите E или Space, когда котлета в зелёной зоне.",
                "E / Space - снять с огня",
                "Промах - котлета сгорит",
                "котлета"),
            StationMiniGameType.FryerDrop => (
                "Фритюр",
                "Передвиньте картошку в корзину и опустите её в масло.",
                "A/D - двигать, E - опустить",
                "Попадите в зелёную корзину",
                "картошка"),
            StationMiniGameType.AssemblyStack => (
                "Сборка",
                "Соберите заказ слоями: нажимайте E или Space по подсказке.",
                "E / Space - положить слой",
                "Нужно собрать все слои",
                "бургер"),
            StationMiniGameType.DrinksFill => (
                "Напитки",
                "Удерживайте E, чтобы наполнить стакан. Отпустите в зелёной зоне.",
                "Держать E - наливать",
                "Перелив - начать заново",
                "стакан"),
            StationMiniGameType.ServingPack => (
                "Выдача",
                "Совместите заказ с пакетом и положите его внутрь.",
                "A/D - двигать, E - упаковать",
                "Промах - перепаковать",
                "заказ"),
            _ => (string.Empty, string.Empty, string.Empty, string.Empty, string.Empty)
        };

        return new StationMiniGameSnapshot
        {
            IsActive = true,
            Type = miniGame.Type,
            Station = miniGame.Station,
            Title = title,
            Instruction = instruction,
            PrimaryAction = primary,
            SecondaryAction = secondary,
            ItemLabel = item,
            Cursor = miniGame.Cursor,
            TargetStart = miniGame.TargetStart,
            TargetEnd = miniGame.TargetEnd,
            Fill = miniGame.Fill,
            StepIndex = miniGame.StepIndex,
            StepCount = miniGame.StepCount,
            Feedback = miniGame.Feedback
        };
    }

    private static bool IsMiniGameCursorInTarget(StationMiniGameState state)
    {
        return state.Cursor >= state.TargetStart && state.Cursor <= state.TargetEnd;
    }

    private static string BuildMiniGameFailureMessage(StationMiniGameType type)
    {
        return type switch
        {
            StationMiniGameType.GrillTiming => "Котлета сгорела. Подойдите к грилю и попробуйте этап заново.",
            StationMiniGameType.FryerDrop => "Картошка упала мимо корзины. Попробуйте фритюр заново.",
            StationMiniGameType.DrinksFill => "Напиток перелился. Налейте новый стакан.",
            StationMiniGameType.ServingPack => "Заказ не попал в пакет. Перепакуйте выдачу.",
            _ => "Мини-игра провалена. Попробуйте ещё раз."
        };
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
            TryAdvanceTutorial(StationType.OrderDesk, "Шеф: касса приняла тестовый заказ.");
            return;
        }

        if (currentOrder is null)
        {
            statusMessage = "Сейчас нет активного клиента.";
            return;
        }

        if (currentProgress is not null)
        {
            statusMessage = "Заказ уже принят. Продолжайте готовить.";
            return;
        }

        currentProgress = new OrderProgress(currentOrder);
        var required = RecipeBook.FormatStationCounts(currentProgress.RequiredStations);
        statusMessage = $"Заказ принят: {RecipeBook.GetOrderName(currentOrder.Items)} ({required}).";
    }

    private void WorkAtStation(StationType stationType, string stationName)
    {
        if (isTutorialActive)
        {
            TryAdvanceTutorial(stationType, $"Шеф: отлично, {stationName.ToLowerInvariant()} отработана.");
            return;
        }

        if (currentProgress is null)
        {
            statusMessage = "Сначала примите заказ на стойке.";
            return;
        }

        var result = currentProgress.ApplyStation(stationType);
        switch (result)
        {
            case StationWorkResult.NotRequired:
                statusMessage = $"Станция \"{stationName}\" не нужна для текущего заказа.";
                return;
            case StationWorkResult.AlreadyCompleted:
                statusMessage = $"Этап на станции \"{stationName}\" уже выполнен.";
                return;
            case StationWorkResult.Completed:
                var completed = currentProgress.GetCompletedCount(stationType);
                var required = currentProgress.GetRequiredCount(stationType);
                var countText = required > 1 ? $" ({completed}/{required})" : string.Empty;
                statusMessage = $"Этап выполнен: {stationName}{countText}.";
                if (currentProgress.IsReady)
                {
                    statusMessage += " Заказ готов, несите на выдачу.";
                }

                return;
            default:
                statusMessage = "Неизвестный результат действия на станции.";
                return;
        }
    }

    private void ServeCurrentOrder()
    {
        if (isTutorialActive)
        {
            TryAdvanceTutorial(StationType.ServingCounter, "Шеф: выдача готова. Теперь открываемся по-настоящему.");
            return;
        }

        if (currentOrder is null)
        {
            statusMessage = "Нет заказа для выдачи.";
            return;
        }

        if (currentProgress is null)
        {
            statusMessage = "Заказ еще не принят на стойке.";
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

            scoreBonus += Math.Max(0, currentOrder.Items.Count - 1) * 15;
            Score += scoreBonus;
            ServedOrders++;
            Rating = Math.Min(100, Rating + settings.SuccessfulServeRatingBonus);

            var reaction = currentCustomer is null
                ? "Заказ выдан правильно."
                : $"{currentCustomer.Name}: {PickLine(currentCustomer.SuccessLines)}";
            statusMessage = reaction;

            SpawnNextOrder();
            return;
        }

        var complaint = currentCustomer is null
            ? "Выдан неполный заказ."
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
            customerPatienceMaxSeconds = 0;
            return;
        }

        FillWaitingCustomers(minCount: 4);
        currentCustomer = waitingCustomers.Dequeue();
        FillWaitingCustomers(minCount: 4);

        var difficulty = GetCurrentDifficulty();
        var items = NextOrderItemsForCurrentCustomer(difficulty);

        currentOrder = new OrderTicket(nextOrderId++, items);
        currentProgress = null;
        customerPatienceMaxSeconds = GetPatienceForDifficulty(difficulty);
        customerPatienceSecondsLeftFloat = customerPatienceMaxSeconds;
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

    private IReadOnlyList<MenuItemType> NextOrderItemsForCurrentCustomer(ShiftDifficulty difficulty)
    {
        if (scriptedOrderPattern.Count > 0)
        {
            var item = scriptedOrderPattern[orderPatternIndex % scriptedOrderPattern.Count];
            orderPatternIndex++;
            return new[] { item };
        }

        var templates = GetOrderTemplatesByDifficulty(difficulty);
        var templateIndex = orderPatternIndex % templates.Count;
        var items = templates[templateIndex];
        orderPatternIndex++;
        return items;
    }

    private static IReadOnlyList<IReadOnlyList<MenuItemType>> GetOrderTemplatesByDifficulty(ShiftDifficulty difficulty)
    {
        return difficulty switch
        {
            ShiftDifficulty.Easy => new IReadOnlyList<MenuItemType>[]
            {
                new[] { MenuItemType.ClassicBurger },
                new[] { MenuItemType.Fries },
                new[] { MenuItemType.Drink },
                new[] { MenuItemType.ClassicBurger, MenuItemType.Drink }
            },
            ShiftDifficulty.Medium => new IReadOnlyList<MenuItemType>[]
            {
                new[] { MenuItemType.ClassicBurger, MenuItemType.Drink },
                new[] { MenuItemType.SpicyBurger, MenuItemType.Fries },
                new[] { MenuItemType.ClassicBurger, MenuItemType.ClassicBurger },
                new[] { MenuItemType.Fries, MenuItemType.Drink, MenuItemType.Drink },
                new[] { MenuItemType.ComboMeal }
            },
            ShiftDifficulty.Hard => new IReadOnlyList<MenuItemType>[]
            {
                new[] { MenuItemType.SpicyBurger, MenuItemType.Fries, MenuItemType.Drink },
                new[] { MenuItemType.ClassicBurger, MenuItemType.SpicyBurger, MenuItemType.Fries, MenuItemType.Drink, MenuItemType.Drink, MenuItemType.Drink },
                new[] { MenuItemType.ComboMeal, MenuItemType.Drink },
                new[] { MenuItemType.SpicyBurger, MenuItemType.SpicyBurger, MenuItemType.Drink },
                new[] { MenuItemType.Fries, MenuItemType.Fries, MenuItemType.Drink, MenuItemType.Drink, MenuItemType.Drink },
                new[] { MenuItemType.ClassicBurger, MenuItemType.SpicyBurger, MenuItemType.Fries, MenuItemType.Drink, MenuItemType.Drink }
            },
            _ => new IReadOnlyList<MenuItemType>[] { new[] { MenuItemType.ClassicBurger } }
        };
    }

    private void ApplyMistake(int baseRatingPenalty, string reason)
    {
        Mistakes++;
        var penalty = ScalePenaltyByDifficulty(baseRatingPenalty);
        Rating = Math.Max(0, Rating - penalty);
        statusMessage = $"{reason} Рейтинг -{penalty}.";

        if (Mistakes >= settings.MaxMistakes || Rating < settings.MinRatingToKeepJob)
        {
            isShiftRunning = false;
            isGameOver = true;
            outcome = ShiftOutcome.Fired;
            statusMessage = "Слишком много ошибок. Вас уволили.";
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
        statusMessage = $"Смена завершена. Выполнено заказов: {ServedOrders}.";
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
            tutorialSecondsLeft = 0;
            return;
        }

        tutorialSecondsLeft = GetRemainingTutorialSteps();
        switch (tutorialStep)
        {
            case TutorialStep.OrderDesk:
                tutorialTargetStation = StationType.OrderDesk;
                chefMessage = "Шеф: начинаем с кассы. Подойдите к стойке заказа и удерживайте E.";
                break;
            case TutorialStep.Grill:
                tutorialTargetStation = StationType.Grill;
                chefMessage = "Шеф: теперь гриль. Прогрейте линию, удерживая E.";
                break;
            case TutorialStep.Assembly:
                tutorialTargetStation = StationType.Assembly;
                chefMessage = "Шеф: на сборке нужен темп. Жмите E быстро, как на запаре.";
                break;
            case TutorialStep.ServingCounter:
                tutorialTargetStation = StationType.ServingCounter;
                chefMessage = "Шеф: финальный шаг — выдача. Подойдите к окну и завершите цикл.";
                break;
            default:
                tutorialTargetStation = null;
                chefMessage = "Шеф: отлично, теперь можно открываться.";
                break;
        }
    }

    private void TryAdvanceTutorial(StationType stationType, string successMessage)
    {
        if (!isTutorialActive)
        {
            return;
        }

        if (tutorialTargetStation != stationType)
        {
            var expected = tutorialTargetStation is null
                ? "следующий шаг"
                : RecipeBook.GetStationName(tutorialTargetStation.Value).ToLowerInvariant();
            statusMessage = $"Шеф: не сюда. Сначала {expected}.";
            return;
        }

        statusMessage = successMessage;
        tutorialStep = tutorialStep switch
        {
            TutorialStep.OrderDesk => TutorialStep.Grill,
            TutorialStep.Grill => TutorialStep.Assembly,
            TutorialStep.Assembly => TutorialStep.ServingCounter,
            TutorialStep.ServingCounter => TutorialStep.Complete,
            _ => TutorialStep.Complete
        };

        if (tutorialStep == TutorialStep.Complete)
        {
            CompleteTutorial();
            return;
        }

        ResetInteractionState(clearHint: true);
        UpdateTutorialState();
    }

    private void CompleteTutorial()
    {
        isTutorialActive = false;
        tutorialStep = TutorialStep.None;
        tutorialTargetStation = null;
        tutorialSecondsLeft = 0;
        chefMessage = "Шеф: вот теперь работаем по-настоящему.";
        statusMessage = "Обучение закончено. Первый гость уже у кассы.";
        SpawnNextOrder();
    }

    private int GetRemainingTutorialSteps()
    {
        return tutorialStep switch
        {
            TutorialStep.OrderDesk => 4,
            TutorialStep.Grill => 3,
            TutorialStep.Assembly => 2,
            TutorialStep.ServingCounter => 1,
            _ => 0
        };
    }

    private IReadOnlyList<string> BuildTutorialHints()
    {
        if (!isShiftStarted)
        {
            return new[]
            {
                "WASD/стрелки: движение",
                "E/Space: действия и мини-игры станций",
                "Enter: начать смену"
            };
        }

        if (isTutorialActive)
        {
            return new[]
            {
                "Идет пошаговое обучение",
                $"Шагов осталось: {tutorialSecondsLeft}",
                $"Сейчас: {RecipeBook.GetStationName(tutorialTargetStation ?? StationType.OrderDesk)}"
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
                "1) Принять заказ на стойке",
                "2) Пройти мини-игры нужных станций",
                "3) Упаковать заказ до конца терпения клиента"
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

        var serviceWindow = Math.Max(1, settings.ShiftDurationSeconds);
        var timeProgress = serviceElapsedSeconds / (float)serviceWindow;
        var hasMediumPressure = ServedOrders >= 2 || timeProgress >= 0.18f;
        var hasHardPressure = ServedOrders >= 4 || timeProgress >= 0.55f;

        if (!hasMediumPressure)
        {
            return ShiftDifficulty.Easy;
        }

        if (!hasHardPressure)
        {
            return ShiftDifficulty.Medium;
        }

        return ShiftDifficulty.Hard;
    }

    private IReadOnlyList<NpcSnapshot> BuildNpcSnapshots()
    {
        var npcs = new List<NpcSnapshot>
        {
            new("Шеф Густаво", NpcRole.Chef, GetChefPosition(settings), isTutorialActive ? chefMessage : "Шеф: держите темп, не останавливайтесь.")
        };

        var deskPosition = stations.Single(x => x.Type == StationType.OrderDesk).Position;
        if (currentCustomer is not null)
        {
            npcs.Add(new NpcSnapshot(currentCustomer.Name, NpcRole.Customer, new GridPosition(deskPosition.X, deskPosition.Y - 2), currentCustomerSpeech));
        }

        var queueY = deskPosition.Y - 2;
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
        return new GridPosition(settings.MapWidth / 2 - 1, settings.KitchenStartRow + 6);
    }

    private static GridPosition GetChefPosition(ShiftSettings settings)
    {
        return new GridPosition(settings.MapWidth / 2 - 5, settings.KitchenStartRow + 4);
    }

    private static IReadOnlyList<Station> BuildStations(ShiftSettings settings)
    {
        var centerX = settings.MapWidth / 2;
        var kitchenRow = settings.KitchenStartRow;
        return new[]
        {
            new Station(StationType.OrderDesk, RecipeBook.GetStationName(StationType.OrderDesk), new GridPosition(centerX - 3, kitchenRow - 1)),
            new Station(StationType.ServingCounter, RecipeBook.GetStationName(StationType.ServingCounter), new GridPosition(centerX + 2, kitchenRow - 1)),
            new Station(StationType.Grill, RecipeBook.GetStationName(StationType.Grill), new GridPosition(centerX - 4, kitchenRow + 3)),
            new Station(StationType.Fryer, RecipeBook.GetStationName(StationType.Fryer), new GridPosition(centerX - 2, kitchenRow + 3)),
            new Station(StationType.Assembly, RecipeBook.GetStationName(StationType.Assembly), new GridPosition(centerX, kitchenRow + 3)),
            new Station(StationType.Drinks, RecipeBook.GetStationName(StationType.Drinks), new GridPosition(centerX + 2, kitchenRow + 3))
        };
    }

    private static IReadOnlyList<ScenePropSnapshot> BuildSceneProps(ShiftSettings settings)
    {
        var props = new List<ScenePropSnapshot>();
        var maxX = settings.MapWidth - 1;
        var maxY = settings.MapHeight - 1;
        var centerX = settings.MapWidth / 2;
        var counterY = settings.KitchenStartRow - 2;

        for (var x = 1; x < maxX; x++)
        {
            props.Add(new ScenePropSnapshot(ScenePropType.Wall, new GridPosition(x, 1)));
        }

        for (var y = 2; y < maxY; y++)
        {
            props.Add(new ScenePropSnapshot(ScenePropType.Wall, new GridPosition(1, y), 1));
            props.Add(new ScenePropSnapshot(ScenePropType.Wall, new GridPosition(maxX - 1, y), 1));
        }

        for (var x = 3; x <= 5; x++)
        {
            props.Add(new ScenePropSnapshot(ScenePropType.Window, new GridPosition(x, 1)));
        }

        for (var x = 27; x <= 29; x++)
        {
            props.Add(new ScenePropSnapshot(ScenePropType.Window, new GridPosition(x, 1)));
        }

        props.Add(new ScenePropSnapshot(ScenePropType.NeonSign, new GridPosition(centerX - 1, 2)));
        props.Add(new ScenePropSnapshot(ScenePropType.MenuBoard, new GridPosition(centerX - 4, 3)));
        props.Add(new ScenePropSnapshot(ScenePropType.MenuBoard, new GridPosition(centerX + 1, 3), 1));

        for (var x = 10; x <= 23; x++)
        {
            props.Add(new ScenePropSnapshot(ScenePropType.Counter, new GridPosition(x, counterY)));
        }

        props.Add(new ScenePropSnapshot(ScenePropType.Booth, new GridPosition(3, 4), 0));
        props.Add(new ScenePropSnapshot(ScenePropType.Booth, new GridPosition(3, 6), 0));
        props.Add(new ScenePropSnapshot(ScenePropType.Booth, new GridPosition(29, 4), 1));
        props.Add(new ScenePropSnapshot(ScenePropType.Booth, new GridPosition(29, 6), 1));
        props.Add(new ScenePropSnapshot(ScenePropType.Table, new GridPosition(6, 4), 0));
        props.Add(new ScenePropSnapshot(ScenePropType.Table, new GridPosition(6, 6), 0));
        props.Add(new ScenePropSnapshot(ScenePropType.Table, new GridPosition(26, 4), 1));
        props.Add(new ScenePropSnapshot(ScenePropType.Table, new GridPosition(26, 6), 1));
        props.Add(new ScenePropSnapshot(ScenePropType.Table, new GridPosition(12, 4), 2));
        props.Add(new ScenePropSnapshot(ScenePropType.Table, new GridPosition(22, 4), 2));
        props.Add(new ScenePropSnapshot(ScenePropType.Chair, new GridPosition(12, 3), 0));
        props.Add(new ScenePropSnapshot(ScenePropType.Chair, new GridPosition(11, 4), 1));
        props.Add(new ScenePropSnapshot(ScenePropType.Chair, new GridPosition(13, 4), 2));
        props.Add(new ScenePropSnapshot(ScenePropType.Chair, new GridPosition(22, 3), 0));
        props.Add(new ScenePropSnapshot(ScenePropType.Chair, new GridPosition(21, 4), 1));
        props.Add(new ScenePropSnapshot(ScenePropType.Chair, new GridPosition(23, 4), 2));
        props.Add(new ScenePropSnapshot(ScenePropType.Plant, new GridPosition(2, 3), 0));
        props.Add(new ScenePropSnapshot(ScenePropType.Plant, new GridPosition(31, 3), 1));
        props.Add(new ScenePropSnapshot(ScenePropType.Plant, new GridPosition(2, 8), 2));
        props.Add(new ScenePropSnapshot(ScenePropType.Plant, new GridPosition(31, 8), 3));
        props.Add(new ScenePropSnapshot(ScenePropType.QueuePost, new GridPosition(centerX - 3, 6)));
        props.Add(new ScenePropSnapshot(ScenePropType.QueuePost, new GridPosition(centerX - 1, 6)));
        props.Add(new ScenePropSnapshot(ScenePropType.QueuePost, new GridPosition(centerX + 1, 6)));
        props.Add(new ScenePropSnapshot(ScenePropType.QueuePost, new GridPosition(centerX + 3, 6)));

        for (var y = settings.KitchenStartRow; y <= settings.KitchenStartRow + 6; y++)
        {
            props.Add(new ScenePropSnapshot(ScenePropType.Wall, new GridPosition(centerX - 7, y), 2));
            props.Add(new ScenePropSnapshot(ScenePropType.Wall, new GridPosition(centerX + 5, y), 2));
        }

        for (var x = centerX - 6; x <= centerX + 4; x++)
        {
            var propType = x == centerX - 3 || x == centerX + 2
                ? ScenePropType.Door
                : ScenePropType.Wall;
            props.Add(new ScenePropSnapshot(propType, new GridPosition(x, settings.KitchenStartRow), 2));
        }

        for (var x = centerX - 5; x <= centerX + 3; x++)
        {
            if (x == centerX - 3 || x == centerX + 2)
            {
                continue;
            }

            props.Add(new ScenePropSnapshot(ScenePropType.PrepTable, new GridPosition(x, settings.KitchenStartRow + 2), Math.Abs(x % 2)));
        }

        props.Add(new ScenePropSnapshot(ScenePropType.ExhaustHood, new GridPosition(centerX - 4, settings.KitchenStartRow + 1), 0));
        props.Add(new ScenePropSnapshot(ScenePropType.ExhaustHood, new GridPosition(centerX - 2, settings.KitchenStartRow + 1), 1));
        props.Add(new ScenePropSnapshot(ScenePropType.ExhaustHood, new GridPosition(centerX, settings.KitchenStartRow + 1), 0));
        props.Add(new ScenePropSnapshot(ScenePropType.ExhaustHood, new GridPosition(centerX + 2, settings.KitchenStartRow + 1), 1));

        props.Add(new ScenePropSnapshot(ScenePropType.Shelf, new GridPosition(centerX - 6, settings.KitchenStartRow + 1), 0));
        props.Add(new ScenePropSnapshot(ScenePropType.Shelf, new GridPosition(centerX - 6, settings.KitchenStartRow + 2), 1));
        props.Add(new ScenePropSnapshot(ScenePropType.Fridge, new GridPosition(centerX + 4, settings.KitchenStartRow + 1), 0));
        props.Add(new ScenePropSnapshot(ScenePropType.Fridge, new GridPosition(centerX + 4, settings.KitchenStartRow + 2), 1));
        props.Add(new ScenePropSnapshot(ScenePropType.PrepTable, new GridPosition(centerX - 6, settings.KitchenStartRow + 4), 0));
        props.Add(new ScenePropSnapshot(ScenePropType.PrepTable, new GridPosition(centerX - 6, settings.KitchenStartRow + 5), 1));
        props.Add(new ScenePropSnapshot(ScenePropType.PrepTable, new GridPosition(centerX + 3, settings.KitchenStartRow + 4), 1));
        props.Add(new ScenePropSnapshot(ScenePropType.CoffeeMachine, new GridPosition(centerX + 4, settings.KitchenStartRow + 4)));

        props.Add(new ScenePropSnapshot(ScenePropType.KitchenBench, new GridPosition(centerX - 5, settings.KitchenStartRow + 5), 0));
        props.Add(new ScenePropSnapshot(ScenePropType.KitchenBench, new GridPosition(centerX - 4, settings.KitchenStartRow + 5), 1));
        props.Add(new ScenePropSnapshot(ScenePropType.KitchenBench, new GridPosition(centerX + 3, settings.KitchenStartRow + 5), 1));
        props.Add(new ScenePropSnapshot(ScenePropType.TrashCan, new GridPosition(centerX - 6, settings.KitchenStartRow + 6)));

        for (var x = 10; x <= 23; x++)
        {
            props.Add(new ScenePropSnapshot(ScenePropType.KitchenBench, new GridPosition(x, settings.KitchenStartRow + 7)));
        }

        props.Add(new ScenePropSnapshot(ScenePropType.Shelf, new GridPosition(5, settings.KitchenStartRow + 2), 0));
        props.Add(new ScenePropSnapshot(ScenePropType.Fridge, new GridPosition(6, settings.KitchenStartRow + 2), 0));
        props.Add(new ScenePropSnapshot(ScenePropType.Shelf, new GridPosition(28, settings.KitchenStartRow + 2), 1));
        props.Add(new ScenePropSnapshot(ScenePropType.Fridge, new GridPosition(27, settings.KitchenStartRow + 2), 1));
        props.Add(new ScenePropSnapshot(ScenePropType.TrashCan, new GridPosition(4, settings.KitchenStartRow + 6)));
        props.Add(new ScenePropSnapshot(ScenePropType.TrashCan, new GridPosition(29, settings.KitchenStartRow + 6), 1));

        props.Add(new ScenePropSnapshot(ScenePropType.FloorMat, new GridPosition(centerX - 4, settings.KitchenStartRow + 3), 0));
        props.Add(new ScenePropSnapshot(ScenePropType.FloorMat, new GridPosition(centerX - 2, settings.KitchenStartRow + 3), 0));
        props.Add(new ScenePropSnapshot(ScenePropType.FloorMat, new GridPosition(centerX, settings.KitchenStartRow + 3), 1));
        props.Add(new ScenePropSnapshot(ScenePropType.FloorMat, new GridPosition(centerX + 2, settings.KitchenStartRow + 3), 1));
        props.Add(new ScenePropSnapshot(ScenePropType.FloorMat, new GridPosition(centerX - 3, settings.KitchenStartRow - 1), 2));
        props.Add(new ScenePropSnapshot(ScenePropType.FloorMat, new GridPosition(centerX + 2, settings.KitchenStartRow - 1), 2));

        for (var x = centerX - 2; x <= centerX + 1; x++)
        {
            props.Add(new ScenePropSnapshot(ScenePropType.Door, new GridPosition(x, maxY - 1)));
        }

        return props;
    }

    private static HashSet<GridPosition> BuildBlockedTiles(
        ShiftSettings settings,
        IReadOnlyList<ScenePropSnapshot> sceneProps,
        IReadOnlyList<Station> stations)
    {
        var blocked = new HashSet<GridPosition>();

        for (var x = 0; x < settings.MapWidth; x++)
        {
            blocked.Add(new GridPosition(x, 0));
            blocked.Add(new GridPosition(x, 1));
            blocked.Add(new GridPosition(x, settings.MapHeight - 2));
            blocked.Add(new GridPosition(x, settings.MapHeight - 1));
        }

        for (var y = 0; y < settings.MapHeight; y++)
        {
            blocked.Add(new GridPosition(0, y));
            blocked.Add(new GridPosition(1, y));
            blocked.Add(new GridPosition(settings.MapWidth - 2, y));
            blocked.Add(new GridPosition(settings.MapWidth - 1, y));
        }

        foreach (var prop in sceneProps)
        {
            if (IsBlockingProp(prop.Type))
            {
                blocked.Add(prop.Position);
            }
        }

        foreach (var station in stations)
        {
            blocked.Remove(station.Position);
        }

        blocked.Remove(GetPlayerStartPosition(settings));
        blocked.Remove(GetChefPosition(settings));
        return blocked;
    }

    private static bool IsBlockingProp(ScenePropType type)
    {
        return type is ScenePropType.Wall
            or ScenePropType.Counter
            or ScenePropType.Booth
            or ScenePropType.Table
            or ScenePropType.Chair
            or ScenePropType.Plant
            or ScenePropType.QueuePost
            or ScenePropType.KitchenBench
            or ScenePropType.PrepTable
            or ScenePropType.Shelf
            or ScenePropType.Fridge
            or ScenePropType.TrashCan
            or ScenePropType.CoffeeMachine;
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
                "Алекс",
                new[] { MenuItemType.ClassicBurger, MenuItemType.Fries, MenuItemType.Drink },
                new[] { "Один бургер, только побыстрее.", "Нужна еда для ночной смены." },
                new[] { "Это не мой заказ.", "Я просил полный набор." },
                new[] { "Отлично, спасибо.", "Быстро сработали." },
                new[] { "Я не могу ждать бесконечно.", "Очередь слишком длинная." }),
            new CustomerProfile(
                "Мия",
                new[] { MenuItemType.SpicyBurger, MenuItemType.Drink, MenuItemType.ComboMeal },
                new[] { "Можно что-то острое, пожалуйста?", "Хочу поярче, ночь только начинается." },
                new[] { "Нет, это неверный заказ.", "Остроты нет, это промах." },
                new[] { "Вот это уже правильно.", "Хороший темп, повар." },
                new[] { "Мое время вышло.", "Ухожу голодной." }),
            new CustomerProfile(
                "Виктор",
                new[] { MenuItemType.ComboMeal, MenuItemType.Fries, MenuItemType.Drink },
                new[] { "Комбо и напиток, пожалуйста.", "У меня всего пять минут." },
                new[] { "Поднос неполный, это плохо.", "Сервис просел." },
                new[] { "Сделано четко.", "Отлично, доволен." },
                new[] { "Я опаздываю, пока.", "Времени не осталось, ухожу." }),
            new CustomerProfile(
                "Нора",
                new[] { MenuItemType.ClassicBurger, MenuItemType.ComboMeal, MenuItemType.Fries },
                new[] { "Привет, удивите скоростью.", "Сегодня я оценю это место." },
                new[] { "Это грубая ошибка.", "Я ожидала лучшего." },
                new[] { "За это пять звезд.", "Быстро и аккуратно." },
                new[] { "Я больше не жду.", "Эта очередь меня победила." })
        };
    }

    private readonly record struct StationInteractionRule(
        StationInteractionMode Mode,
        float HoldDurationSeconds,
        int RapidTapTarget,
        float RapidTapWindowSeconds);

    private sealed class StationMiniGameState
    {
        private StationMiniGameState(
            StationType station,
            StationMiniGameType type,
            float cursor,
            float targetStart,
            float targetEnd,
            int stepCount)
        {
            Station = station;
            Type = type;
            Cursor = cursor;
            TargetStart = targetStart;
            TargetEnd = targetEnd;
            StepCount = stepCount;
        }

        public StationType Station { get; }

        public StationMiniGameType Type { get; }

        public float Cursor { get; set; }

        public float Direction { get; set; } = 1f;

        public float TargetStart { get; }

        public float TargetEnd { get; }

        public float Fill { get; set; }

        public bool IsHolding { get; set; }

        public int StepIndex { get; set; }

        public int StepCount { get; }

        public string Feedback { get; set; } = string.Empty;

        public static StationMiniGameState Create(StationType station)
        {
            return station switch
            {
                StationType.Grill => new StationMiniGameState(station, StationMiniGameType.GrillTiming, 0.08f, 0.58f, 0.76f, 1)
                {
                    Feedback = "Ждите зелёную зону."
                },
                StationType.Fryer => new StationMiniGameState(station, StationMiniGameType.FryerDrop, 0.18f, 0.45f, 0.61f, 1)
                {
                    Feedback = "A/D двигают картошку."
                },
                StationType.Assembly => new StationMiniGameState(station, StationMiniGameType.AssemblyStack, 0f, 0f, 1f, 4)
                {
                    Feedback = "Положите первый слой."
                },
                StationType.Drinks => new StationMiniGameState(station, StationMiniGameType.DrinksFill, 0f, 0.68f, 0.86f, 1)
                {
                    Feedback = "Держите E для наполнения."
                },
                StationType.ServingCounter => new StationMiniGameState(station, StationMiniGameType.ServingPack, 0.82f, 0.39f, 0.61f, 1)
                {
                    Feedback = "A/D двигают заказ."
                },
                _ => new StationMiniGameState(station, StationMiniGameType.None, 0f, 0f, 1f, 1)
            };
        }
    }

    private enum TutorialStep
    {
        None,
        OrderDesk,
        Grill,
        Assembly,
        ServingCounter,
        Complete
    }

    private sealed record CustomerProfile(
        string Name,
        IReadOnlyList<MenuItemType> PreferredOrders,
        IReadOnlyList<string> GreetingLines,
        IReadOnlyList<string> FailureLines,
        IReadOnlyList<string> SuccessLines,
        IReadOnlyList<string> TimeoutLines);
}
