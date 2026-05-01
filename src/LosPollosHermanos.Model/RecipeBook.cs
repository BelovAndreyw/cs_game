namespace LosPollosHermanos.Model;

public static class RecipeBook
{
    private static readonly Dictionary<MenuItemType, StationType[]> Recipes = new()
    {
        [MenuItemType.ClassicBurger] = new[] { StationType.Grill, StationType.Assembly },
        [MenuItemType.SpicyBurger] = new[] { StationType.Grill, StationType.Assembly },
        [MenuItemType.Fries] = new[] { StationType.Fryer },
        [MenuItemType.Drink] = new[] { StationType.Drinks },
        [MenuItemType.ComboMeal] = new[] { StationType.Grill, StationType.Assembly, StationType.Fryer, StationType.Drinks }
    };

    public static HashSet<StationType> GetRequiredStations(MenuItemType item)
    {
        return Recipes[item].ToHashSet();
    }

    public static IReadOnlyList<StationType> GetRequiredStationSequence(IEnumerable<MenuItemType> items)
    {
        return items.SelectMany(item => Recipes[item]).ToArray();
    }

    public static IReadOnlyDictionary<StationType, int> GetRequiredStationCounts(IEnumerable<MenuItemType> items)
    {
        return GetRequiredStationSequence(items)
            .GroupBy(station => station)
            .ToDictionary(group => group.Key, group => group.Count());
    }

    public static string GetMenuItemName(MenuItemType item)
    {
        return item switch
        {
            MenuItemType.ClassicBurger => "Классик бургер",
            MenuItemType.SpicyBurger => "Острый бургер",
            MenuItemType.Fries => "Картошка фри",
            MenuItemType.Drink => "Напиток",
            MenuItemType.ComboMeal => "Комбо-сет",
            _ => item.ToString()
        };
    }

    public static string GetOrderName(IEnumerable<MenuItemType> items)
    {
        return string.Join(" + ", items
            .GroupBy(item => item)
            .Select(group =>
            {
                var name = GetMenuItemName(group.Key);
                return group.Count() == 1 ? name : $"{group.Count()}x {name}";
            }));
    }

    public static string FormatStationCounts(IEnumerable<StationType> stations)
    {
        return string.Join(", ", stations
            .GroupBy(station => station)
            .Select(group =>
            {
                var name = GetStationName(group.Key);
                return group.Count() == 1 ? name : $"{name} x{group.Count()}";
            }));
    }

    public static string GetStationName(StationType type)
    {
        return type switch
        {
            StationType.OrderDesk => "Стойка заказа",
            StationType.Grill => "Гриль",
            StationType.Assembly => "Сборка",
            StationType.Fryer => "Фритюр",
            StationType.Drinks => "Напитки",
            StationType.ServingCounter => "Выдача",
            _ => type.ToString()
        };
    }

    public static string GetStationLabel(StationType type)
    {
        return type switch
        {
            StationType.OrderDesk => "ЗАК",
            StationType.Grill => "ГРИ",
            StationType.Assembly => "СБР",
            StationType.Fryer => "ФРИ",
            StationType.Drinks => "БАР",
            StationType.ServingCounter => "ВЫД",
            _ => "???"
        };
    }
}
