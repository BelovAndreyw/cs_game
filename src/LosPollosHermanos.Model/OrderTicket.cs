namespace LosPollosHermanos.Model;

public sealed class OrderTicket
{
    public OrderTicket(int id, MenuItemType item)
        : this(id, new[] { item })
    {
    }

    public OrderTicket(int id, IEnumerable<MenuItemType> items)
    {
        var orderItems = items.ToArray();
        if (orderItems.Length == 0)
        {
            throw new ArgumentException("Order must contain at least one item.", nameof(items));
        }

        Id = id;
        Items = orderItems;
    }

    public int Id { get; }

    public IReadOnlyList<MenuItemType> Items { get; }

    public MenuItemType Item => Items[0];
}
