using LosPollosHermanos.Model;

namespace LosPollosHermanos.Tests;

public sealed class GameWorldTests
{
    [Test]
    public void StartShift_BeginsTutorial_BeforeFirstOrder()
    {
        var world = new GameWorld();

        world.StartShift();
        var snapshot = world.GetSnapshot();

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.IsShiftStarted, Is.True);
            Assert.That(snapshot.IsShiftRunning, Is.True);
            Assert.That(snapshot.IsTutorialPhase, Is.True);
            Assert.That(snapshot.CurrentOrderName, Is.Null);
            Assert.That(snapshot.TutorialTargetStation, Is.EqualTo(StationType.OrderDesk));
            Assert.That(snapshot.TutorialSecondsLeft, Is.EqualTo(4));
        });
    }

    [Test]
    public void TutorialDoesNotAdvanceOnTickOrSpendShiftTime()
    {
        var settings = CreateNoisySettings(shiftDurationSeconds: 180, chefTutorialSeconds: 30);
        var world = new GameWorld(settings);
        world.StartShift();
        var before = world.GetSnapshot();

        world.Tick();

        var after = world.GetSnapshot();
        Assert.Multiple(() =>
        {
            Assert.That(after.IsTutorialPhase, Is.True);
            Assert.That(after.TutorialTargetStation, Is.EqualTo(StationType.OrderDesk));
            Assert.That(after.TutorialSecondsLeft, Is.EqualTo(before.TutorialSecondsLeft));
            Assert.That(after.TimeRemainingSeconds, Is.EqualTo(before.TimeRemainingSeconds));
            Assert.That(after.CurrentOrderName, Is.Null);
        });
    }

    [Test]
    public void TutorialAdvancesOnlyAfterRequiredActions()
    {
        var world = new GameWorld(CreateNoisySettings(chefTutorialSeconds: 30));
        world.StartShift();

        CompleteStationInteraction(world, StationType.OrderDesk);
        Assert.That(world.GetSnapshot().TutorialTargetStation, Is.EqualTo(StationType.Grill));

        CompleteStationInteraction(world, StationType.Grill);
        Assert.That(world.GetSnapshot().TutorialTargetStation, Is.EqualTo(StationType.Assembly));

        CompleteStationInteraction(world, StationType.Assembly);
        Assert.That(world.GetSnapshot().TutorialTargetStation, Is.EqualTo(StationType.ServingCounter));

        CompleteStationInteraction(world, StationType.ServingCounter);

        var snapshot = world.GetSnapshot();
        Assert.Multiple(() =>
        {
            Assert.That(snapshot.IsTutorialPhase, Is.False);
            Assert.That(snapshot.CurrentOrderName, Is.Not.Null.And.Not.Empty);
            Assert.That(snapshot.CurrentCustomerName, Is.Not.Null.And.Not.Empty);
        });
    }

    [Test]
    public void TutorialWrongStation_DoesNotAdvanceStep()
    {
        var world = new GameWorld(CreateNoisySettings(chefTutorialSeconds: 30));
        world.StartShift();

        CompleteStationInteraction(world, StationType.Grill);

        var snapshot = world.GetSnapshot();
        Assert.Multiple(() =>
        {
            Assert.That(snapshot.IsTutorialPhase, Is.True);
            Assert.That(snapshot.TutorialTargetStation, Is.EqualTo(StationType.OrderDesk));
            Assert.That(snapshot.TutorialSecondsLeft, Is.EqualTo(4));
        });
    }

    [Test]
    public void MovePlayer_ChangesPosition_WhenInsideBounds()
    {
        var world = new GameWorld(CreateNoTutorialSettings());
        world.StartShift();
        var before = world.GetSnapshot().PlayerPosition;

        world.MovePlayer(Direction.Right);

        var after = world.GetSnapshot().PlayerPosition;
        Assert.That(after, Is.EqualTo(new GridPosition(before.X + 1, before.Y)));
    }

    [Test]
    public void KitchenMap_DoesNotBlockLowerRightWorkCorner()
    {
        var world = new GameWorld();
        var snapshot = world.GetSnapshot();
        var lowerRightWorkCorner = new GridPosition(snapshot.MapWidth / 2 + 4, snapshot.KitchenStartRow + 6);

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.SceneProps.Any(prop => prop.Position == lowerRightWorkCorner), Is.False);
            Assert.That(snapshot.BlockedTiles, Does.Not.Contain(lowerRightWorkCorner));
        });
    }

    [Test]
    public void HoldInteraction_RequiresTimeToAcceptOrder()
    {
        var world = new GameWorld(CreateNoTutorialSettings(), new[] { MenuItemType.ClassicBurger });
        world.StartShift();
        MoveToStation(world, StationType.OrderDesk);

        world.BeginInteraction();
        world.UpdateRealtime(0.7f);
        var midSnapshot = world.GetSnapshot();

        world.UpdateRealtime(0.8f);
        world.EndInteraction();
        var readySnapshot = world.GetSnapshot();

        Assert.Multiple(() =>
        {
            Assert.That(midSnapshot.IsCurrentOrderAccepted, Is.False);
            Assert.That(readySnapshot.IsCurrentOrderAccepted, Is.True);
        });
    }

    [Test]
    public void AssemblyMiniGame_CompletesStationAfterAllLayers()
    {
        var world = new GameWorld(CreateNoTutorialSettings(), new[] { MenuItemType.ClassicBurger });
        world.StartShift();
        MoveToStation(world, StationType.OrderDesk);
        world.Interact();

        MoveToStation(world, StationType.Assembly);
        world.BeginInteraction();
        var started = world.GetSnapshot();

        CompleteMiniGame(world);
        var completed = world.GetSnapshot();

        Assert.Multiple(() =>
        {
            Assert.That(started.MiniGame.IsActive, Is.True);
            Assert.That(started.MiniGame.Type, Is.EqualTo(StationMiniGameType.AssemblyStack));
            Assert.That(completed.CompletedStations.Contains(StationType.Assembly), Is.True);
        });
    }

    [Test]
    public void FailedGrillMiniGame_DoesNotCompleteStation()
    {
        var world = new GameWorld(CreateNoTutorialSettings(), new[] { MenuItemType.ClassicBurger });
        world.StartShift();
        CompleteStationInteraction(world, StationType.OrderDesk);
        MoveToStation(world, StationType.Grill);

        world.BeginInteraction();
        var started = world.GetSnapshot();
        world.SubmitMiniGameAction();
        var failed = world.GetSnapshot();

        Assert.Multiple(() =>
        {
            Assert.That(started.MiniGame.Type, Is.EqualTo(StationMiniGameType.GrillTiming));
            Assert.That(failed.MiniGame.IsActive, Is.False);
            Assert.That(failed.CompletedStations.Contains(StationType.Grill), Is.False);
            Assert.That(failed.StatusMessage, Does.Contain("сгорела"));
        });
    }

    [Test]
    public void DrinksMiniGame_CompletesOnlyWhenFillStopsInTarget()
    {
        var world = new GameWorld(CreateNoTutorialSettings(), new[] { MenuItemType.Drink });
        world.StartShift();
        CompleteStationInteraction(world, StationType.OrderDesk);
        MoveToStation(world, StationType.Drinks);

        world.BeginInteraction();
        var started = world.GetSnapshot();
        CompleteMiniGame(world);
        var completed = world.GetSnapshot();

        Assert.Multiple(() =>
        {
            Assert.That(started.MiniGame.Type, Is.EqualTo(StationMiniGameType.DrinksFill));
            Assert.That(completed.CompletedStations.Contains(StationType.Drinks), Is.True);
        });
    }

    [Test]
    public void DrinksMiniGame_IgnoresReleaseBeforeFillingStarts()
    {
        var world = new GameWorld(CreateNoTutorialSettings(), new[] { MenuItemType.Drink });
        world.StartShift();
        CompleteStationInteraction(world, StationType.OrderDesk);
        MoveToStation(world, StationType.Drinks);

        world.BeginInteraction();
        world.EndMiniGameAction();

        var snapshot = world.GetSnapshot();
        Assert.Multiple(() =>
        {
            Assert.That(snapshot.MiniGame.IsActive, Is.True);
            Assert.That(snapshot.MiniGame.Type, Is.EqualTo(StationMiniGameType.DrinksFill));
            Assert.That(snapshot.CompletedStations.Contains(StationType.Drinks), Is.False);
            Assert.That(snapshot.StatusMessage, Does.Not.Contain("перелился"));
        });
    }

    [Test]
    public void LeavingStation_CancelsHoldInteractionProgress()
    {
        var world = new GameWorld(CreateNoTutorialSettings(), new[] { MenuItemType.ClassicBurger });
        world.StartShift();
        MoveToStation(world, StationType.OrderDesk);

        world.BeginInteraction();
        world.UpdateRealtime(0.6f);
        world.MovePlayer(Direction.Left);

        var snapshot = world.GetSnapshot();
        Assert.Multiple(() =>
        {
            Assert.That(snapshot.InteractionMode, Is.EqualTo(StationInteractionMode.None));
            Assert.That(snapshot.InteractionProgress, Is.EqualTo(0f));
            Assert.That(snapshot.IsCurrentOrderAccepted, Is.False);
        });
    }

    [Test]
    public void Difficulty_ProgressesFromEasyToHardDuringShift()
    {
        var settings = CreateNoisySettings(shiftDurationSeconds: 120, chefTutorialSeconds: 0);
        var world = new GameWorld(settings);
        world.StartShift();

        var atStart = world.GetSnapshot();
        for (var i = 0; i < 50; i++)
        {
            world.Tick();
        }

        var mid = world.GetSnapshot();
        for (var i = 0; i < 50; i++)
        {
            world.Tick();
        }

        var nearEnd = world.GetSnapshot();

        Assert.Multiple(() =>
        {
            Assert.That(atStart.Difficulty, Is.EqualTo(ShiftDifficulty.Easy));
            Assert.That(mid.Difficulty, Is.EqualTo(ShiftDifficulty.Medium));
            Assert.That(nearEnd.Difficulty, Is.EqualTo(ShiftDifficulty.Hard));
        });
    }

    [Test]
    public void OrdersGrowIntoMultiItemCombos_AfterPressureRises()
    {
        var settings = CreateNoisySettings(shiftDurationSeconds: 300, chefTutorialSeconds: 0, customerPatienceSeconds: 999);
        var world = new GameWorld(settings);
        world.StartShift();

        for (var i = 0; i < 170; i++)
        {
            world.Tick();
        }

        CompleteCurrentOrder(world);

        var snapshot = world.GetSnapshot();
        var requiredCounts = snapshot.RequiredStations
            .GroupBy(station => station)
            .ToDictionary(group => group.Key, group => group.Count());

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Difficulty, Is.EqualTo(ShiftDifficulty.Hard));
            Assert.That(snapshot.CurrentOrderItems, Has.Count.GreaterThan(3));
            Assert.That(snapshot.CurrentOrderItems.Count(item => item is MenuItemType.ClassicBurger or MenuItemType.SpicyBurger), Is.EqualTo(2));
            Assert.That(snapshot.CurrentOrderItems.Count(item => item == MenuItemType.Drink), Is.EqualTo(3));
            Assert.That(requiredCounts[StationType.Grill], Is.EqualTo(2));
            Assert.That(requiredCounts[StationType.Drinks], Is.EqualTo(3));
        });
    }

    [Test]
    public void CompleteOrderCycle_IncreasesScoreAndServedOrders()
    {
        var world = new GameWorld(CreateNoTutorialSettings(), new[] { MenuItemType.ClassicBurger });
        world.StartShift();

        MoveToStation(world, StationType.OrderDesk);
        world.Interact();

        var acceptedSnapshot = world.GetSnapshot();
        foreach (var stationType in acceptedSnapshot.RequiredStations)
        {
            CompleteStationInteraction(world, stationType);
        }

        CompleteStationInteraction(world, StationType.ServingCounter);

        var snapshot = world.GetSnapshot();
        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Score, Is.EqualTo(100));
            Assert.That(snapshot.ServedOrders, Is.EqualTo(1));
            Assert.That(snapshot.Mistakes, Is.EqualTo(0));
        });
    }

    [Test]
    public void ServingOrder_AdvancesCurrentCustomerAndKeepsQueueFlowing()
    {
        var world = new GameWorld(CreateNoTutorialSettings(), new[] { MenuItemType.ClassicBurger, MenuItemType.SpicyBurger });
        world.StartShift();

        var before = world.GetSnapshot();
        var firstCustomer = before.CurrentCustomerName;
        var expectedNextCustomer = before.WaitingCustomerNames[0];

        MoveToStation(world, StationType.OrderDesk);
        world.Interact();

        var acceptedSnapshot = world.GetSnapshot();
        foreach (var stationType in acceptedSnapshot.RequiredStations)
        {
            CompleteStationInteraction(world, stationType);
        }

        CompleteStationInteraction(world, StationType.ServingCounter);

        var snapshot = world.GetSnapshot();
        Assert.Multiple(() =>
        {
            Assert.That(snapshot.CurrentCustomerName, Is.Not.EqualTo(firstCustomer));
            Assert.That(snapshot.CurrentCustomerName, Is.EqualTo(expectedNextCustomer));
            Assert.That(snapshot.WaitingCustomerNames.Count, Is.EqualTo(3));
        });
    }

    [Test]
    public void ServingIncompleteOrder_AddsMistakeAndMovesQueue()
    {
        var world = new GameWorld(CreateNoTutorialSettings(), new[] { MenuItemType.ClassicBurger, MenuItemType.SpicyBurger });
        world.StartShift();
        var firstCustomer = world.GetSnapshot().CurrentCustomerName;

        CompleteStationInteraction(world, StationType.OrderDesk);
        MoveToStation(world, StationType.ServingCounter);
        world.Interact();

        var snapshot = world.GetSnapshot();
        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Mistakes, Is.EqualTo(1));
            Assert.That(snapshot.CurrentCustomerName, Is.Not.EqualTo(firstCustomer));
            Assert.That(snapshot.StatusMessage, Does.Not.Contain("уволили"));
        });
    }

    [Test]
    public void CustomerTimeout_AddsMistakeAndMovesToNextOrder()
    {
        var settings = CreateNoisySettings(shiftDurationSeconds: 70, chefTutorialSeconds: 0, customerPatienceSeconds: 2);
        var world = new GameWorld(settings, new[] { MenuItemType.ClassicBurger, MenuItemType.SpicyBurger });
        world.StartShift();
        var firstOrder = world.GetSnapshot().CurrentOrderName;

        world.Tick();
        world.Tick();

        var snapshot = world.GetSnapshot();
        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Mistakes, Is.EqualTo(1));
            Assert.That(snapshot.CurrentOrderName, Is.Not.EqualTo(firstOrder));
        });
    }

    [Test]
    public void TooManyMistakes_EndsGameWithFiredOutcome()
    {
        var settings = CreateNoisySettings(
            shiftDurationSeconds: 70,
            chefTutorialSeconds: 0,
            customerPatienceSeconds: 1,
            maxMistakes: 2,
            timeoutPenalty: 1,
            minRatingToKeepJob: 1);

        var world = new GameWorld(settings);
        world.StartShift();
        world.Tick();
        world.Tick();

        var snapshot = world.GetSnapshot();
        Assert.Multiple(() =>
        {
            Assert.That(snapshot.IsGameOver, Is.True);
            Assert.That(snapshot.IsShiftRunning, Is.False);
            Assert.That(snapshot.Outcome, Is.EqualTo(ShiftOutcome.Fired));
        });
    }

    [Test]
    public void ShiftTimeEnds_FinishesWithVictory_WhenPlayerNotFired()
    {
        var settings = CreateNoisySettings(shiftDurationSeconds: 2, chefTutorialSeconds: 0, customerPatienceSeconds: 10);
        var world = new GameWorld(settings);
        world.StartShift();
        world.Tick();
        world.Tick();

        var snapshot = world.GetSnapshot();
        Assert.Multiple(() =>
        {
            Assert.That(snapshot.IsGameOver, Is.True);
            Assert.That(snapshot.Outcome, Is.EqualTo(ShiftOutcome.Victory));
        });
    }

    [Test]
    public void ShiftEndingOnLastSecond_DoesNotAlsoTriggerCustomerTimeout()
    {
        var settings = CreateNoisySettings(shiftDurationSeconds: 1, chefTutorialSeconds: 0, customerPatienceSeconds: 1);
        var world = new GameWorld(settings, new[] { MenuItemType.ClassicBurger });
        world.StartShift();

        world.Tick();

        var snapshot = world.GetSnapshot();
        Assert.Multiple(() =>
        {
            Assert.That(snapshot.IsGameOver, Is.True);
            Assert.That(snapshot.Outcome, Is.EqualTo(ShiftOutcome.Victory));
            Assert.That(snapshot.Mistakes, Is.EqualTo(0));
        });
    }

    [Test]
    public void RestartShift_ResetsScoreMistakesAndInteractionState()
    {
        var world = new GameWorld(CreateNoTutorialSettings(), new[] { MenuItemType.ClassicBurger });
        world.StartShift();

        MoveToStation(world, StationType.OrderDesk);
        world.Interact();
        var acceptedSnapshot = world.GetSnapshot();
        foreach (var stationType in acceptedSnapshot.RequiredStations)
        {
            CompleteStationInteraction(world, stationType);
        }

        CompleteStationInteraction(world, StationType.ServingCounter);

        MoveToStation(world, StationType.OrderDesk);
        world.BeginInteraction();
        world.UpdateRealtime(0.4f);

        world.RestartShift();

        var snapshot = world.GetSnapshot();
        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Score, Is.EqualTo(0));
            Assert.That(snapshot.Mistakes, Is.EqualTo(0));
            Assert.That(snapshot.ServedOrders, Is.EqualTo(0));
            Assert.That(snapshot.InteractionMode, Is.EqualTo(StationInteractionMode.None));
            Assert.That(snapshot.InteractionProgress, Is.EqualTo(0f));
            Assert.That(snapshot.IsShiftRunning, Is.True);
            Assert.That(snapshot.CurrentCustomerName, Is.Not.Null.And.Not.Empty);
        });
    }

    private static ShiftSettings CreateNoTutorialSettings()
    {
        return CreateNoisySettings(chefTutorialSeconds: 0, shiftDurationSeconds: 240);
    }

    private static void CompleteCurrentOrder(GameWorld world)
    {
        MoveToStation(world, StationType.OrderDesk);
        CompleteStationInteraction(world, StationType.OrderDesk);

        var acceptedSnapshot = world.GetSnapshot();
        foreach (var stationType in acceptedSnapshot.RequiredStations)
        {
            CompleteStationInteraction(world, stationType);
        }

        CompleteStationInteraction(world, StationType.ServingCounter);
    }

    private static void CompleteStationInteraction(GameWorld world, StationType stationType)
    {
        MoveToStation(world, stationType);
        var before = world.GetSnapshot();
        if (before.IsTutorialPhase || stationType == StationType.OrderDesk)
        {
            if (stationType is StationType.Assembly or StationType.Drinks)
            {
                var taps = stationType == StationType.Assembly ? 6 : 5;
                for (var i = 0; i < taps; i++)
                {
                    world.BeginInteraction();
                    world.EndInteraction();
                    world.UpdateRealtime(0.12f);
                }

                return;
            }

            world.BeginInteraction();
            world.UpdateRealtime(GetHoldDuration(stationType));
            world.EndInteraction();
            return;
        }

        world.BeginInteraction();
        CompleteMiniGame(world);
    }

    private static void CompleteMiniGame(GameWorld world)
    {
        var snapshot = world.GetSnapshot();
        Assert.That(snapshot.MiniGame.IsActive, Is.True);

        switch (snapshot.MiniGame.Type)
        {
            case StationMiniGameType.GrillTiming:
                SubmitWhenCursorIsInTarget(world);
                return;
            case StationMiniGameType.FryerDrop:
            case StationMiniGameType.ServingPack:
                AlignMiniGameCursor(world);
                world.SubmitMiniGameAction();
                return;
            case StationMiniGameType.AssemblyStack:
                while (world.GetSnapshot().MiniGame.IsActive)
                {
                    world.SubmitMiniGameAction();
                }

                return;
            case StationMiniGameType.DrinksFill:
                world.BeginMiniGameAction();
                while (world.GetSnapshot().MiniGame.IsActive)
                {
                    var current = world.GetSnapshot().MiniGame;
                    if (current.Fill >= current.TargetStart)
                    {
                        world.EndMiniGameAction();
                        return;
                    }

                    world.UpdateRealtime(0.05f);
                }

                return;
            default:
                Assert.Fail("Неизвестный тип мини-игры.");
                return;
        }
    }

    private static void SubmitWhenCursorIsInTarget(GameWorld world)
    {
        for (var i = 0; i < 80; i++)
        {
            var miniGame = world.GetSnapshot().MiniGame;
            if (miniGame.Cursor >= miniGame.TargetStart && miniGame.Cursor <= miniGame.TargetEnd)
            {
                world.SubmitMiniGameAction();
                return;
            }

            world.UpdateRealtime(0.05f);
        }

        Assert.Fail("Не удалось дождаться зелёной зоны мини-игры.");
    }

    private static void AlignMiniGameCursor(GameWorld world)
    {
        for (var i = 0; i < 30; i++)
        {
            var miniGame = world.GetSnapshot().MiniGame;
            var targetCenter = (miniGame.TargetStart + miniGame.TargetEnd) / 2f;
            if (Math.Abs(miniGame.Cursor - targetCenter) <= 0.04f)
            {
                return;
            }

            world.MoveMiniGame(miniGame.Cursor < targetCenter ? Direction.Right : Direction.Left);
        }

        Assert.Fail("Не удалось совместить предмет с целью мини-игры.");
    }

    private static float GetHoldDuration(StationType stationType)
    {
        return stationType switch
        {
            StationType.OrderDesk => 1.3f,
            StationType.Grill => 2.0f,
            StationType.Fryer => 2.3f,
            StationType.ServingCounter => 1.5f,
            _ => 0f
        };
    }

    private static ShiftSettings CreateNoisySettings(
        int shiftDurationSeconds = 300,
        int chefTutorialSeconds = 30,
        int customerPatienceSeconds = 55,
        int maxMistakes = 4,
        int timeoutPenalty = 15,
        int minRatingToKeepJob = 35)
    {
        return new ShiftSettings
        {
            ShiftDurationSeconds = shiftDurationSeconds,
            ChefTutorialSeconds = chefTutorialSeconds,
            CustomerPatienceSeconds = customerPatienceSeconds,
            EasyPatienceBonusSeconds = 0,
            MaxMistakes = maxMistakes,
            TimeoutPenalty = timeoutPenalty,
            MinRatingToKeepJob = minRatingToKeepJob
        };
    }

    private static void MoveToStation(GameWorld world, StationType stationType)
    {
        var snapshot = world.GetSnapshot();
        var target = snapshot.Stations.Single(x => x.Type == stationType).Position;
        var path = FindPath(snapshot, target);
        foreach (var direction in path)
        {
            world.MovePlayer(direction);
        }

        Assert.That(world.GetSnapshot().PlayerPosition, Is.EqualTo(target));
    }

    private static IReadOnlyList<Direction> FindPath(GameSnapshot snapshot, GridPosition target)
    {
        var start = snapshot.PlayerPosition;
        if (start == target)
        {
            return Array.Empty<Direction>();
        }

        var blocked = snapshot.BlockedTiles.ToHashSet();
        var queue = new Queue<GridPosition>();
        var visited = new HashSet<GridPosition> { start };
        var cameFrom = new Dictionary<GridPosition, (GridPosition Previous, Direction Direction)>();

        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var (next, direction) in EnumerateNeighbors(current))
            {
                if (next.X < 0 || next.Y < 0 || next.X >= snapshot.MapWidth || next.Y >= snapshot.MapHeight)
                {
                    continue;
                }

                if (blocked.Contains(next) || !visited.Add(next))
                {
                    continue;
                }

                cameFrom[next] = (current, direction);
                if (next == target)
                {
                    return ReconstructPath(start, target, cameFrom);
                }

                queue.Enqueue(next);
            }
        }

        Assert.Fail($"Не удалось построить путь до станции {target}.");
        return Array.Empty<Direction>();
    }

    private static IReadOnlyList<Direction> ReconstructPath(
        GridPosition start,
        GridPosition target,
        IReadOnlyDictionary<GridPosition, (GridPosition Previous, Direction Direction)> cameFrom)
    {
        var path = new List<Direction>();
        var current = target;

        while (current != start)
        {
            var step = cameFrom[current];
            path.Add(step.Direction);
            current = step.Previous;
        }

        path.Reverse();
        return path;
    }

    private static IEnumerable<(GridPosition Position, Direction Direction)> EnumerateNeighbors(GridPosition current)
    {
        yield return (current.Move(Direction.Up), Direction.Up);
        yield return (current.Move(Direction.Right), Direction.Right);
        yield return (current.Move(Direction.Down), Direction.Down);
        yield return (current.Move(Direction.Left), Direction.Left);
    }
}
