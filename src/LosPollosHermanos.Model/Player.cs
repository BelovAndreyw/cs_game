namespace LosPollosHermanos.Model;

public sealed class Player
{
    public Player(GridPosition startPosition)
    {
        Position = startPosition;
    }

    public GridPosition Position { get; private set; }

    public void Move(Direction direction, int mapWidth, int mapHeight)
    {
        var next = Position.Move(direction);
        if (next.X < 0 || next.Y < 0 || next.X >= mapWidth || next.Y >= mapHeight)
        {
            return;
        }

        Position = next;
    }

    public void Reset(GridPosition startPosition)
    {
        Position = startPosition;
    }
}
