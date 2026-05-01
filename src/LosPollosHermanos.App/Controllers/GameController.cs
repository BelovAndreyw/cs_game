using LosPollosHermanos.Model;

namespace LosPollosHermanos.App.Controllers;

public sealed class GameController
{
    private readonly GameWorld world;

    public GameController()
        : this(new GameWorld())
    {
    }

    public GameController(GameWorld world)
    {
        this.world = world;
    }

    public GameSnapshot Snapshot => world.GetSnapshot();

    public void StartShift()
    {
        world.StartShift();
    }

    public void RestartShift()
    {
        world.RestartShift();
    }

    public void Tick()
    {
        world.Tick();
    }

    public void UpdateRealtime(float deltaSeconds)
    {
        world.UpdateRealtime(deltaSeconds);
    }

    public void Move(Direction direction)
    {
        world.MovePlayer(direction);
    }

    public void Interact()
    {
        world.Interact();
    }

    public void BeginInteraction()
    {
        world.BeginInteraction();
    }

    public void EndInteraction()
    {
        world.EndInteraction();
    }

    public void BeginMiniGameAction()
    {
        world.BeginMiniGameAction();
    }

    public void EndMiniGameAction()
    {
        world.EndMiniGameAction();
    }

    public void SubmitMiniGameAction()
    {
        world.SubmitMiniGameAction();
    }

    public void MoveMiniGame(Direction direction)
    {
        world.MoveMiniGame(direction);
    }
}
