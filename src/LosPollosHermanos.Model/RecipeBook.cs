namespace LosPollosHermanos.Model;

public static class RecipeBook
{
    private static readonly Dictionary<MenuItemType, HashSet<StationType>> Recipes = new()
    {
        {
            MenuItemType.ClassicBurger,
            new HashSet<StationType>
            {
                StationType.Grill,
                StationType.Assembly
            }
        },
        {
            MenuItemType.SpicyBurger,
            new HashSet<StationType>
            {
                StationType.Grill,
                StationType.Assembly,
                StationType.Drinks
            }
        },
        {
            MenuItemType.ComboMeal,
            new HashSet<StationType>
            {
                StationType.Grill,
                StationType.Assembly,
                StationType.Fryer,
                StationType.Drinks
            }
        }
    };

    public static HashSet<StationType> GetRequiredStations(MenuItemType item)
    {
        return new HashSet<StationType>(Recipes[item]);
    }

    public static string GetMenuItemName(MenuItemType item)
    {
        return item switch
        {
            MenuItemType.ClassicBurger => "Классик бургер",
            MenuItemType.SpicyBurger => "Острый бургер",
            MenuItemType.ComboMeal => "Комбо-сет",
            _ => item.ToString()
        };
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
            StationType.OrderDesk => "ORD",
            StationType.Grill => "GRL",
            StationType.Assembly => "ASM",
            StationType.Fryer => "FRY",
            StationType.Drinks => "DRK",
            StationType.ServingCounter => "OUT",
            _ => "???"
        };
    }
}
