namespace LosPollosHermanos.Model;

public sealed record NpcSnapshot(string Name, NpcRole Role, GridPosition Position, string? Speech);
