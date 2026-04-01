namespace LosPollosHermanos.Model;

public readonly record struct GridPosition(int X, int Y)
{
    public GridPosition Move(Direction direction)
    {
        return direction switch
        {
            Direction.Up => new GridPosition(X, Y - 1),
            Direction.Down => new GridPosition(X, Y + 1),
            Direction.Left => new GridPosition(X - 1, Y),
            Direction.Right => new GridPosition(X + 1, Y),
            _ => this
        };
    }
}
