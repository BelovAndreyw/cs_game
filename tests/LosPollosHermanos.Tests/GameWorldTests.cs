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
            Assert.That(snapshot.TutorialSecondsLeft, Is.EqualTo(30));
        });
    }

    [Test]
    public void TutorialEnds_SpawnsFirstCustomerAndOrder()
    {
        var settings = CreateNoisySettings(chefTutorialSeconds: 3);
        var world = new GameWorld(settings);
        world.StartShift();

        world.Tick();
        world.Tick();
        world.Tick();

        var snapshot = world.GetSnapshot();
        Assert.Multiple(() =>
        {
            Assert.That(snapshot.IsTutorialPhase, Is.False);
            Assert.That(snapshot.CurrentOrderName, Is.Not.Null.And.Not.Empty);
            Assert.That(snapshot.CurrentCustomerName, Is.Not.Null.And.Not.Empty);
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
    public void RapidTapInteraction_RequiresMultipleKeyPresses()
    {
        var world = new GameWorld(CreateNoTutorialSettings(), new[] { MenuItemType.ClassicBurger });
        world.StartShift();
        MoveToStation(world, StationType.OrderDesk);
        world.Interact();

        MoveToStation(world, StationType.Assembly);
        for (var i = 0; i < 5; i++)
        {
            world.BeginInteraction();
            world.EndInteraction();
            world.UpdateRealtime(0.12f);
        }

        var beforeFinalTap = world.GetSnapshot();
        world.BeginInteraction();
        world.EndInteraction();
        world.UpdateRealtime(0.12f);
        var afterFinalTap = world.GetSnapshot();

        Assert.Multiple(() =>
        {
            Assert.That(beforeFinalTap.CompletedStations.Contains(StationType.Assembly), Is.False);
            Assert.That(afterFinalTap.CompletedStations.Contains(StationType.Assembly), Is.True);
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
    public void CompleteOrderCycle_IncreasesScoreAndServedOrders()
    {
        var world = new GameWorld(CreateNoTutorialSettings(), new[] { MenuItemType.ClassicBurger });
        world.StartShift();

        MoveToStation(world, StationType.OrderDesk);
        world.Interact();

        var acceptedSnapshot = world.GetSnapshot();
        foreach (var stationType in acceptedSnapshot.RequiredStations)
        {
            MoveToStation(world, stationType);
            world.Interact();
        }

        MoveToStation(world, StationType.ServingCounter);
        world.Interact();

        var snapshot = world.GetSnapshot();
        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Score, Is.EqualTo(100));
            Assert.That(snapshot.ServedOrders, Is.EqualTo(1));
            Assert.That(snapshot.Mistakes, Is.EqualTo(0));
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

    private static ShiftSettings CreateNoTutorialSettings()
    {
        return CreateNoisySettings(chefTutorialSeconds: 0, shiftDurationSeconds: 240);
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

        while (true)
        {
            var current = world.GetSnapshot().PlayerPosition;
            if (current == target)
            {
                return;
            }

            if (current.X < target.X)
            {
                world.MovePlayer(Direction.Right);
                continue;
            }

            if (current.X > target.X)
            {
                world.MovePlayer(Direction.Left);
                continue;
            }

            if (current.Y < target.Y)
            {
                world.MovePlayer(Direction.Down);
                continue;
            }

            world.MovePlayer(Direction.Up);
        }
    }
}
