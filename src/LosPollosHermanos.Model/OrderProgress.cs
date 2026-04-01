namespace LosPollosHermanos.Model;

public sealed class OrderProgress
{
    private readonly HashSet<StationType> completedStations = new();
    private readonly HashSet<StationType> requiredStations;

    public OrderProgress(OrderTicket ticket)
    {
        Ticket = ticket;
        requiredStations = RecipeBook.GetRequiredStations(ticket.Item);
    }

    public OrderTicket Ticket { get; }

    public IReadOnlyCollection<StationType> CompletedStations => completedStations;

    public IReadOnlyCollection<StationType> RequiredStations => requiredStations;

    public bool IsReady => requiredStations.All(completedStations.Contains);

    public StationWorkResult ApplyStation(StationType stationType)
    {
        if (!requiredStations.Contains(stationType))
        {
            return StationWorkResult.NotRequired;
        }

        if (!completedStations.Add(stationType))
        {
            return StationWorkResult.AlreadyCompleted;
        }

        return StationWorkResult.Completed;
    }
}
