using LosPollosHermanos.Model;

namespace LosPollosHermanos.Tests;

public sealed class GameWorldTests
{
    [Test]
    public void StartShift_InitializesGameState()
    {
        var world = new GameWorld();

        world.StartShift();
        var snapshot = world.GetSnapshot();

        Assert.Multiple(() =>
        {
            Assert.That(snapshot.IsShiftStarted, Is.True);
            Assert.That(snapshot.IsShiftRunning, Is.True);
            Assert.That(snapshot.IsGameOver, Is.False);
            Assert.That(snapshot.CurrentOrderName, Is.Not.Null.And.Not.Empty);
            Assert.That(snapshot.TimeRemainingSeconds, Is.EqualTo(240));
        });
    }

    [Test]
    public void MovePlayer_ChangesPosition_WhenInsideBounds()
    {
        var world = new GameWorld();
        world.StartShift();
        var before = world.GetSnapshot().PlayerPosition;

        world.MovePlayer(Direction.Right);

        var after = world.GetSnapshot().PlayerPosition;
        Assert.That(after, Is.EqualTo(new GridPosition(before.X + 1, before.Y)));
    }

    [Test]
    public void CompleteOrderCycle_IncreasesScoreAndServedOrders()
    {
        var world = new GameWorld(orderPattern: new[] { MenuItemType.ClassicBurger });
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
    public void ServeIncompleteOrder_AddsMistake()
    {
        var world = new GameWorld(orderPattern: new[] { MenuItemType.ComboMeal });
        world.StartShift();

        MoveToStation(world, StationType.OrderDesk);
        world.Interact();
        MoveToStation(world, StationType.ServingCounter);
        world.Interact();

        var snapshot = world.GetSnapshot();
        Assert.Multiple(() =>
        {
            Assert.That(snapshot.Score, Is.EqualTo(0));
            Assert.That(snapshot.Mistakes, Is.EqualTo(1));
        });
    }

    [Test]
    public void CustomerTimeout_AddsMistakeAndMovesToNextOrder()
    {
        var settings = new ShiftSettings
        {
            ShiftDurationSeconds = 60,
            CustomerPatienceSeconds = 2
        };

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
        var settings = new ShiftSettings
        {
            ShiftDurationSeconds = 60,
            CustomerPatienceSeconds = 1,
            MaxMistakes = 2,
            TimeoutPenalty = 1,
            MinRatingToKeepJob = 1
        };

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
        var settings = new ShiftSettings
        {
            ShiftDurationSeconds = 2,
            CustomerPatienceSeconds = 10
        };

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
