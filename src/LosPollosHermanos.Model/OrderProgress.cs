namespace LosPollosHermanos.Model;

public sealed class OrderProgress
{
    private readonly Dictionary<StationType, int> completedCounts = new();
    private readonly IReadOnlyDictionary<StationType, int> requiredCounts;
    private readonly IReadOnlyList<StationType> requiredStationSequence;

    public OrderProgress(OrderTicket ticket)
    {
        Ticket = ticket;
        requiredStationSequence = RecipeBook.GetRequiredStationSequence(ticket.Items);
        requiredCounts = RecipeBook.GetRequiredStationCounts(ticket.Items);
    }

    public OrderTicket Ticket { get; }

    public IReadOnlyCollection<StationType> CompletedStations => ExpandCounts(completedCounts);

    public IReadOnlyCollection<StationType> RequiredStations => requiredStationSequence.ToArray();

    public bool IsReady => requiredCounts.All(pair => GetCompletedCount(pair.Key) >= pair.Value);

    public bool RequiresStation(StationType stationType)
    {
        return requiredCounts.ContainsKey(stationType);
    }

    public bool RequiresMore(StationType stationType)
    {
        return requiredCounts.TryGetValue(stationType, out var required)
            && GetCompletedCount(stationType) < required;
    }

    public int GetRequiredCount(StationType stationType)
    {
        return requiredCounts.TryGetValue(stationType, out var required) ? required : 0;
    }

    public int GetCompletedCount(StationType stationType)
    {
        return completedCounts.TryGetValue(stationType, out var completed) ? completed : 0;
    }

    public StationWorkResult ApplyStation(StationType stationType)
    {
        if (!requiredCounts.ContainsKey(stationType))
        {
            return StationWorkResult.NotRequired;
        }

        if (!RequiresMore(stationType))
        {
            return StationWorkResult.AlreadyCompleted;
        }

        completedCounts[stationType] = GetCompletedCount(stationType) + 1;
        return StationWorkResult.Completed;
    }

    private static IReadOnlyCollection<StationType> ExpandCounts(IReadOnlyDictionary<StationType, int> counts)
    {
        return counts
            .SelectMany(pair => Enumerable.Repeat(pair.Key, pair.Value))
            .ToArray();
    }
}
