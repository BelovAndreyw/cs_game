using LosPollosHermanos.Model;

namespace LosPollosHermanos.App.Rendering;

public enum FloorTileKind
{
    Dining,
    Threshold,
    Kitchen
}

public sealed class GameSpriteLibrary : IDisposable
{
    private const int BaseSpriteSize = 16;

    private readonly string assetsRoot;
    private readonly Dictionary<string, Bitmap> cache = new(StringComparer.OrdinalIgnoreCase);

    public GameSpriteLibrary(string assetsRoot)
    {
        this.assetsRoot = assetsRoot;
    }

    public Image GetFloorTile(FloorTileKind kind, int variation)
    {
        var key = $"floor:{kind}:{variation}";
        return GetOrCreate(
            key,
            () => LoadOrFallback(
                Path.Combine("tiles", $"floor-{kind.ToString().ToLowerInvariant()}-{variation}.png"),
                () => CreateFloorTile(kind, variation)));
    }

    public Image GetScenePropSprite(ScenePropType type, int variation)
    {
        var key = $"prop:{type}:{variation}";
        return GetOrCreate(
            key,
            () => LoadOrFallback(
                Path.Combine("props", $"{type.ToString().ToLowerInvariant()}-{variation}.png"),
                () => CreateScenePropSprite(type, variation)));
    }

    public Image GetStationSprite(StationType type)
    {
        var key = $"station:{type}";
        return GetOrCreate(
            key,
            () => LoadOrFallback(
                Path.Combine("stations", $"{type.ToString().ToLowerInvariant()}.png"),
                () => CreateStationSprite(type)));
    }

    public Image GetNpcSprite(NpcRole role, int variation)
    {
        var key = $"npc:{role}:{variation}";
        var fileName = role == NpcRole.Chef ? "chef.png" : $"customer-{variation}.png";
        return GetOrCreate(
            key,
            () => LoadOrFallback(
                Path.Combine("npcs", fileName),
                () => CreateNpcSprite(role, variation)));
    }

    public Image GetPlayerSprite(PlayerAnimationFrame frame)
    {
        var key = $"player:{frame.Mode}:{frame.Facing}:{frame.Frame}";
        var fileName = $"{frame.Mode.ToString().ToLowerInvariant()}-{frame.Facing.ToString().ToLowerInvariant()}-{frame.Frame}.png";
        return GetOrCreate(
            key,
            () => LoadOrFallback(
                Path.Combine("player", fileName),
                () => CreatePlayerSprite(frame)));
    }

    public void Dispose()
    {
        foreach (var sprite in cache.Values)
        {
            sprite.Dispose();
        }

        cache.Clear();
    }

    private Image GetOrCreate(string key, Func<Bitmap> factory)
    {
        if (cache.TryGetValue(key, out var existing))
        {
            return existing;
        }

        var created = factory();
        cache[key] = created;
        return created;
    }

    private Bitmap LoadOrFallback(string relativePath, Func<Bitmap> fallbackFactory)
    {
        var fullPath = Path.Combine(assetsRoot, relativePath);
        if (!File.Exists(fullPath))
        {
            return fallbackFactory();
        }

        try
        {
            using var source = (Bitmap)Image.FromFile(fullPath);
            return new Bitmap(source);
        }
        catch
        {
            return fallbackFactory();
        }
    }

    private static Bitmap CreateFloorTile(FloorTileKind kind, int variation)
    {
        return CreateSprite(canvas =>
        {
            switch (kind)
            {
                case FloorTileKind.Dining:
                    DrawDiningFloor(canvas, variation);
                    break;
                case FloorTileKind.Threshold:
                    DrawThresholdFloor(canvas, variation);
                    break;
                default:
                    DrawKitchenFloor(canvas, variation);
                    break;
            }
        });
    }

    private static Bitmap CreateScenePropSprite(ScenePropType type, int variation)
    {
        return CreateSprite(canvas =>
        {
            switch (type)
            {
                case ScenePropType.Wall:
                    DrawWall(canvas, variation);
                    break;
                case ScenePropType.Window:
                    DrawWindow(canvas, variation);
                    break;
                case ScenePropType.Door:
                    DrawDoor(canvas);
                    break;
                case ScenePropType.NeonSign:
                    DrawNeonSign(canvas);
                    break;
                case ScenePropType.Counter:
                    DrawFrontCounter(canvas, variation);
                    break;
                case ScenePropType.MenuBoard:
                    DrawMenuBoard(canvas, variation);
                    break;
                case ScenePropType.Booth:
                    DrawBooth(canvas, variation);
                    break;
                case ScenePropType.Table:
                    DrawTable(canvas, variation);
                    break;
                case ScenePropType.Chair:
                    DrawChair(canvas, variation);
                    break;
                case ScenePropType.Plant:
                    DrawPlant(canvas, variation);
                    break;
                case ScenePropType.QueuePost:
                    DrawQueuePost(canvas);
                    break;
                case ScenePropType.KitchenBench:
                    DrawKitchenBench(canvas, variation);
                    break;
                case ScenePropType.PrepTable:
                    DrawPrepTable(canvas, variation);
                    break;
                case ScenePropType.Shelf:
                    DrawShelf(canvas, variation);
                    break;
                case ScenePropType.Fridge:
                    DrawFridge(canvas, variation);
                    break;
                case ScenePropType.TrashCan:
                    DrawTrashCan(canvas, variation);
                    break;
                case ScenePropType.FloorMat:
                    DrawFloorMat(canvas, variation);
                    break;
                case ScenePropType.CoffeeMachine:
                    DrawCoffeeMachine(canvas);
                    break;
                case ScenePropType.ExhaustHood:
                    DrawExhaustHood(canvas, variation);
                    break;
            }
        });
    }

    private static Bitmap CreateStationSprite(StationType type)
    {
        return CreateSprite(canvas =>
        {
            var accent = GameTheme.GetStationAccent(type);
            var outline = Color.FromArgb(39, 29, 24);
            var steel = Color.FromArgb(92, 99, 108);
            var top = Color.FromArgb(214, 213, 206);

            canvas.Rect(1, 3, 14, 10, steel);
            canvas.Rect(2, 4, 12, 8, Color.FromArgb(121, 129, 142));
            canvas.Rect(1, 2, 14, 2, top);
            canvas.Outline(1, 3, 14, 10, outline);

            switch (type)
            {
                case StationType.OrderDesk:
                    canvas.Rect(3, 5, 10, 5, Color.FromArgb(233, 238, 241));
                    canvas.Rect(5, 6, 6, 2, Color.FromArgb(87, 182, 179));
                    canvas.Rect(6, 10, 4, 1, Color.FromArgb(89, 104, 122));
                    break;
                case StationType.Grill:
                    canvas.Rect(3, 5, 10, 5, Color.FromArgb(54, 51, 55));
                    canvas.VLine(5, 5, 9, Color.FromArgb(190, 194, 198));
                    canvas.VLine(7, 5, 9, Color.FromArgb(190, 194, 198));
                    canvas.VLine(9, 5, 9, Color.FromArgb(190, 194, 198));
                    canvas.Rect(5, 10, 6, 2, Color.FromArgb(236, 125, 74));
                    break;
                case StationType.Assembly:
                    canvas.Rect(3, 9, 10, 2, Color.FromArgb(123, 83, 52));
                    canvas.Rect(4, 7, 8, 2, Color.FromArgb(241, 201, 92));
                    canvas.Rect(4, 5, 8, 1, Color.FromArgb(95, 180, 104));
                    canvas.Rect(5, 10, 6, 1, Color.FromArgb(202, 118, 142));
                    break;
                case StationType.Fryer:
                    canvas.Rect(4, 5, 8, 5, Color.FromArgb(79, 84, 95));
                    canvas.Outline(4, 5, 8, 5, Color.FromArgb(230, 232, 236));
                    canvas.Rect(6, 3, 4, 3, Color.FromArgb(240, 196, 88));
                    canvas.Rect(6, 10, 4, 2, Color.FromArgb(207, 168, 54));
                    break;
                case StationType.Drinks:
                    canvas.Rect(5, 3, 6, 8, Color.FromArgb(234, 242, 250));
                    canvas.Rect(10, 3, 1, 8, Color.FromArgb(154, 193, 226));
                    canvas.Rect(6, 2, 1, 3, Color.FromArgb(255, 118, 109));
                    canvas.Rect(8, 2, 1, 3, Color.FromArgb(255, 214, 97));
                    break;
                case StationType.ServingCounter:
                    canvas.Rect(3, 8, 10, 2, Color.FromArgb(238, 230, 220));
                    canvas.Rect(4, 5, 8, 3, accent);
                    canvas.Rect(6, 4, 4, 1, Color.FromArgb(247, 247, 247));
                    break;
            }

            canvas.Rect(2, 13, 12, 2, Color.FromArgb(49, 41, 36));
        });
    }

    private static Bitmap CreateNpcSprite(NpcRole role, int variation)
    {
        return CreateSprite(canvas =>
        {
            if (role == NpcRole.Chef)
            {
                canvas.Rect(5, 1, 6, 3, Color.FromArgb(248, 248, 248));
                canvas.Rect(4, 4, 8, 2, Color.FromArgb(239, 239, 239));
                canvas.Rect(5, 6, 6, 4, Color.FromArgb(240, 205, 168));
                canvas.Rect(4, 10, 8, 4, Color.FromArgb(248, 248, 248));
                canvas.Rect(6, 10, 4, 4, Color.FromArgb(255, 199, 79));
                canvas.Rect(4, 14, 3, 2, Color.FromArgb(73, 90, 117));
                canvas.Rect(9, 14, 3, 2, Color.FromArgb(73, 90, 117));
                return;
            }

            var palettes = new[]
            {
                (shirt: Color.FromArgb(84, 151, 218), pants: Color.FromArgb(60, 79, 110), hair: Color.FromArgb(72, 51, 37)),
                (shirt: Color.FromArgb(201, 109, 118), pants: Color.FromArgb(72, 80, 104), hair: Color.FromArgb(47, 33, 22)),
                (shirt: Color.FromArgb(106, 191, 135), pants: Color.FromArgb(63, 74, 94), hair: Color.FromArgb(128, 85, 48)),
                (shirt: Color.FromArgb(198, 164, 82), pants: Color.FromArgb(64, 76, 101), hair: Color.FromArgb(59, 46, 33))
            };

            var selected = palettes[Math.Abs(variation % palettes.Length)];
            canvas.Rect(5, 3, 6, 4, Color.FromArgb(241, 206, 175));
            canvas.Rect(4, 2, 8, 2, selected.hair);
            canvas.Rect(4, 7, 8, 5, selected.shirt);
            canvas.Rect(5, 12, 3, 4, selected.pants);
            canvas.Rect(8, 12, 3, 4, selected.pants);
        });
    }

    private static Bitmap CreatePlayerSprite(PlayerAnimationFrame frame)
    {
        return CreateSprite(canvas =>
        {
            var skin = Color.FromArgb(241, 205, 174);
            var hair = Color.FromArgb(61, 44, 31);
            var shirt = Color.FromArgb(245, 202, 92);
            var apron = Color.FromArgb(163, 52, 57);
            var pants = Color.FromArgb(68, 97, 143);
            var shoes = Color.FromArgb(34, 40, 49);
            var legSwing = frame.Mode == PlayerAnimationMode.Walk ? (frame.Frame % 2 == 0 ? -1 : 1) : 0;
            var armRaise = frame.Mode == PlayerAnimationMode.Work ? 2 : frame.Mode == PlayerAnimationMode.Walk ? -legSwing : 0;

            switch (frame.Facing)
            {
                case Direction.Up:
                    canvas.Rect(5, 1, 6, 2, hair);
                    canvas.Rect(5, 3, 6, 4, skin);
                    canvas.Rect(4, 7, 8, 4, shirt);
                    canvas.Rect(5, 9, 6, 3, apron);
                    canvas.Rect(3, 7 + armRaise, 2, 5, shirt);
                    canvas.Rect(11, 7 - armRaise, 2, 5, shirt);
                    canvas.Rect(5 + legSwing, 12, 3, 3, pants);
                    canvas.Rect(8 - legSwing, 12, 3, 3, pants);
                    break;
                case Direction.Left:
                    canvas.Rect(4, 2, 4, 2, hair);
                    canvas.Rect(4, 4, 4, 4, skin);
                    canvas.Rect(4, 8, 5, 4, shirt);
                    canvas.Rect(7, 9, 2, 3, apron);
                    canvas.Rect(2, 8 - armRaise, 2, 5, shirt);
                    canvas.Rect(9, 8 + armRaise, 2, 5, shirt);
                    canvas.Rect(4, 12 + Math.Min(legSwing, 0), 2, 3, pants);
                    canvas.Rect(7, 12 + Math.Max(legSwing, 0), 2, 3, pants);
                    break;
                case Direction.Right:
                    canvas.Rect(8, 2, 4, 2, hair);
                    canvas.Rect(8, 4, 4, 4, skin);
                    canvas.Rect(7, 8, 5, 4, shirt);
                    canvas.Rect(7, 9, 2, 3, apron);
                    canvas.Rect(12, 8 - armRaise, 2, 5, shirt);
                    canvas.Rect(5, 8 + armRaise, 2, 5, shirt);
                    canvas.Rect(7, 12 + Math.Min(legSwing, 0), 2, 3, pants);
                    canvas.Rect(10, 12 + Math.Max(legSwing, 0), 2, 3, pants);
                    break;
                default:
                    canvas.Rect(5, 1, 6, 2, hair);
                    canvas.Rect(5, 3, 6, 4, skin);
                    canvas.Pixel(6, 5, Color.FromArgb(29, 35, 42));
                    canvas.Pixel(9, 5, Color.FromArgb(29, 35, 42));
                    canvas.Rect(4, 7, 8, 5, shirt);
                    canvas.Rect(5, 9, 6, 4, apron);
                    canvas.Rect(3, 8 - armRaise, 2, 5, shirt);
                    canvas.Rect(11, 8 + armRaise, 2, 5, shirt);
                    canvas.Rect(5 + legSwing, 13, 3, 2, pants);
                    canvas.Rect(8 - legSwing, 13, 3, 2, pants);
                    break;
            }

            canvas.Rect(5 + legSwing, 15, 3, 1, shoes);
            canvas.Rect(8 - legSwing, 15, 3, 1, shoes);
        });
    }

    private static void DrawDiningFloor(PixelCanvas canvas, int variation)
    {
        var baseColor = Color.FromArgb(154, 108, 72);
        var dark = Color.FromArgb(112, 78, 52);
        var grout = Color.FromArgb(135, 95, 66);
        var sparkle = Color.FromArgb(235, 228, 208);

        canvas.Rect(0, 0, BaseSpriteSize, BaseSpriteSize, baseColor);
        for (var y = 0; y < BaseSpriteSize; y += 4)
        {
            canvas.HLine(0, BaseSpriteSize - 1, y, grout);
        }

        for (var x = 0; x < BaseSpriteSize; x += 4)
        {
            canvas.VLine(x, 0, BaseSpriteSize - 1, x % 8 == 0 ? dark : grout);
        }

        if (variation % 2 == 0)
        {
            canvas.Rect(2, 2, 2, 2, dark);
            canvas.Rect(10, 6, 2, 2, dark);
        }
        else
        {
            canvas.Rect(6, 10, 2, 2, dark);
            canvas.Rect(12, 2, 2, 2, dark);
        }

        if (variation % 3 == 2)
        {
            canvas.Rect(7, 7, 1, 1, sparkle);
            canvas.Rect(3, 12, 1, 1, sparkle);
        }
    }

    private static void DrawThresholdFloor(PixelCanvas canvas, int variation)
    {
        var light = Color.FromArgb(188, 171, 145);
        var mid = Color.FromArgb(150, 134, 112);
        var dark = Color.FromArgb(92, 76, 60);
        canvas.Rect(0, 0, BaseSpriteSize, BaseSpriteSize, light);

        for (var y = 0; y < BaseSpriteSize; y += 4)
        {
            for (var x = 0; x < BaseSpriteSize; x += 4)
            {
                var tile = ((x + y) / 4 + variation) % 2 == 0 ? mid : dark;
                canvas.Rect(x, y, 4, 4, tile);
            }
        }

        canvas.HLine(0, 15, 0, Color.FromArgb(227, 217, 193));
        canvas.HLine(0, 15, 15, Color.FromArgb(71, 57, 47));
    }

    private static void DrawKitchenFloor(PixelCanvas canvas, int variation)
    {
        var baseColor = Color.FromArgb(92, 107, 126);
        var tile = Color.FromArgb(73, 88, 104);
        var grout = Color.FromArgb(132, 148, 168);

        canvas.Rect(0, 0, BaseSpriteSize, BaseSpriteSize, baseColor);
        for (var y = 0; y < BaseSpriteSize; y += 4)
        {
            canvas.HLine(0, BaseSpriteSize - 1, y, grout);
        }

        for (var x = 0; x < BaseSpriteSize; x += 4)
        {
            canvas.VLine(x, 0, BaseSpriteSize - 1, grout);
        }

        var drainX = variation % 2 == 0 ? 5 : 9;
        canvas.Rect(drainX, 5, 2, 2, tile);
        canvas.Rect(drainX, 9, 2, 2, tile);
    }

    private static void DrawWall(PixelCanvas canvas, int variation)
    {
        if (variation >= 2)
        {
            var panel = Color.FromArgb(168, 176, 184);
            var seam = Color.FromArgb(108, 118, 127);
            var metalTrim = Color.FromArgb(82, 90, 98);
            canvas.Rect(0, 0, 16, 16, panel);
            canvas.Rect(0, 12, 16, 4, metalTrim);
            canvas.HLine(0, 15, 3, Color.FromArgb(219, 226, 232));
            canvas.VLine(4, 0, 11, seam);
            canvas.VLine(11, 0, 11, seam);
            canvas.Rect(5, 5, 6, 3, Color.FromArgb(198, 208, 217));
            canvas.Rect(6, 8, 4, 1, Color.FromArgb(92, 102, 111));
            return;
        }

        var plaster = Color.FromArgb(224, 209, 188);
        var trim = Color.FromArgb(142, 91, 61);
        var accent = Color.FromArgb(191, 142, 92);
        canvas.Rect(0, 0, 16, 16, plaster);
        canvas.Rect(0, 12, 16, 4, trim);
        canvas.HLine(0, 15, 3, accent);

        if (variation % 2 == 0)
        {
            canvas.Rect(2, 5, 5, 4, Color.FromArgb(232, 219, 201));
            canvas.Rect(9, 5, 5, 4, Color.FromArgb(232, 219, 201));
        }
        else
        {
            canvas.Rect(2, 4, 2, 8, trim);
            canvas.Rect(12, 4, 2, 8, trim);
            canvas.Rect(5, 6, 6, 3, Color.FromArgb(232, 219, 201));
        }
    }

    private static void DrawWindow(PixelCanvas canvas, int variation)
    {
        var frame = Color.FromArgb(113, 74, 50);
        var glass = Color.FromArgb(122, 188, 230);
        var glow = Color.FromArgb(233, 245, 252);
        canvas.Rect(0, 0, 16, 16, Color.Transparent);
        canvas.Rect(1, 1, 14, 14, frame);
        canvas.Rect(3, 3, 10, 10, glass);
        canvas.HLine(3, 12, 8, glow);
        canvas.VLine(8, 3, 12, glow);
        if (variation % 2 == 1)
        {
            canvas.Rect(4, 4, 3, 3, glow);
        }
    }

    private static void DrawDoor(PixelCanvas canvas)
    {
        var frame = Color.FromArgb(93, 61, 43);
        var glass = Color.FromArgb(92, 138, 171);
        var fill = Color.FromArgb(61, 42, 31);
        canvas.Rect(1, 1, 14, 14, frame);
        canvas.Rect(3, 3, 10, 6, glass);
        canvas.Rect(3, 9, 10, 5, fill);
        canvas.VLine(8, 3, 13, Color.FromArgb(187, 169, 151));
        canvas.Pixel(11, 10, Color.FromArgb(231, 210, 115));
    }

    private static void DrawNeonSign(PixelCanvas canvas)
    {
        var board = Color.FromArgb(44, 27, 31);
        canvas.Rect(1, 3, 14, 10, board);
        canvas.Outline(1, 3, 14, 10, Color.FromArgb(246, 196, 88));
        canvas.Rect(4, 5, 8, 1, Color.FromArgb(224, 96, 88));
        canvas.Rect(3, 7, 10, 1, Color.FromArgb(246, 196, 88));
        canvas.Rect(5, 9, 6, 1, Color.FromArgb(124, 208, 128));
        canvas.Rect(7, 4, 2, 7, Color.FromArgb(224, 96, 88));
    }

    private static void DrawFrontCounter(PixelCanvas canvas, int variation)
    {
        var top = Color.FromArgb(221, 204, 177);
        var face = variation % 2 == 0 ? Color.FromArgb(150, 58, 51) : Color.FromArgb(132, 48, 43);
        var trim = Color.FromArgb(91, 43, 32);
        canvas.Rect(0, 0, 16, 16, Color.Transparent);
        canvas.Rect(0, 2, 16, 2, top);
        canvas.Rect(0, 4, 16, 9, face);
        canvas.Rect(0, 13, 16, 3, trim);
        canvas.VLine(5, 4, 12, Color.FromArgb(179, 86, 75));
        canvas.VLine(10, 4, 12, Color.FromArgb(179, 86, 75));
    }

    private static void DrawMenuBoard(PixelCanvas canvas, int variation)
    {
        canvas.Rect(1, 2, 14, 10, Color.FromArgb(31, 34, 39));
        canvas.Outline(1, 2, 14, 10, Color.FromArgb(95, 107, 121));
        var line = variation % 2 == 0 ? GameTheme.Accent : GameTheme.Warning;
        canvas.Rect(3, 4, 10, 1, line);
        canvas.Rect(3, 6, 8, 1, Color.FromArgb(239, 239, 239));
        canvas.Rect(3, 8, 9, 1, Color.FromArgb(239, 239, 239));
        canvas.Rect(3, 10, 6, 1, Color.FromArgb(239, 239, 239));
    }

    private static void DrawBooth(PixelCanvas canvas, int variation)
    {
        var seat = Color.FromArgb(170, 55, 54);
        var back = Color.FromArgb(126, 39, 38);
        var outline = Color.FromArgb(74, 35, 31);
        canvas.Rect(0, 0, 16, 16, Color.Transparent);
        canvas.Rect(2, 2, 12, 4, back);
        canvas.Rect(2, 8, 12, 4, seat);
        canvas.Rect(2, 12, 12, 2, outline);
        if (variation % 2 == 0)
        {
            canvas.Rect(1, 2, 2, 10, outline);
        }
        else
        {
            canvas.Rect(13, 2, 2, 10, outline);
        }
    }

    private static void DrawTable(PixelCanvas canvas, int variation)
    {
        var wood = variation switch
        {
            1 => Color.FromArgb(150, 94, 58),
            2 => Color.FromArgb(202, 170, 114),
            _ => Color.FromArgb(175, 123, 81)
        };

        canvas.Rect(0, 0, 16, 16, Color.Transparent);
        if (variation == 2)
        {
            canvas.Rect(3, 4, 10, 7, wood);
            canvas.Outline(3, 4, 10, 7, Color.FromArgb(102, 73, 48));
        }
        else
        {
            canvas.Rect(4, 4, 8, 8, wood);
            canvas.Outline(4, 4, 8, 8, Color.FromArgb(102, 73, 48));
        }

        canvas.Rect(7, 11, 2, 4, Color.FromArgb(88, 69, 61));
    }

    private static void DrawChair(PixelCanvas canvas, int variation)
    {
        var seat = Color.FromArgb(86, 106, 136);
        var frame = Color.FromArgb(54, 43, 39);
        canvas.Rect(0, 0, 16, 16, Color.Transparent);
        switch (variation % 3)
        {
            case 0:
                canvas.Rect(5, 4, 6, 4, seat);
                canvas.Rect(4, 2, 8, 2, frame);
                canvas.Rect(5, 8, 1, 4, frame);
                canvas.Rect(10, 8, 1, 4, frame);
                break;
            case 1:
                canvas.Rect(4, 5, 4, 6, seat);
                canvas.Rect(2, 4, 2, 8, frame);
                canvas.Rect(8, 5, 4, 1, frame);
                canvas.Rect(8, 10, 4, 1, frame);
                break;
            default:
                canvas.Rect(8, 5, 4, 6, seat);
                canvas.Rect(12, 4, 2, 8, frame);
                canvas.Rect(4, 5, 4, 1, frame);
                canvas.Rect(4, 10, 4, 1, frame);
                break;
        }
    }

    private static void DrawPlant(PixelCanvas canvas, int variation)
    {
        var pot = Color.FromArgb(161, 97, 58);
        var leaves = variation switch
        {
            1 => Color.FromArgb(86, 163, 110),
            2 => Color.FromArgb(98, 188, 124),
            3 => Color.FromArgb(74, 154, 95),
            _ => Color.FromArgb(108, 184, 101)
        };

        canvas.Rect(0, 0, 16, 16, Color.Transparent);
        canvas.Rect(5, 10, 6, 3, pot);
        canvas.Rect(4, 7, 8, 3, leaves);
        canvas.Rect(6, 4, 4, 4, leaves);
        canvas.Rect(3, 6, 3, 3, leaves);
        canvas.Rect(10, 6, 3, 3, leaves);
    }

    private static void DrawQueuePost(PixelCanvas canvas)
    {
        canvas.Rect(7, 3, 2, 9, Color.FromArgb(214, 214, 219));
        canvas.Rect(5, 2, 6, 2, Color.FromArgb(237, 196, 79));
        canvas.Rect(4, 11, 8, 2, Color.FromArgb(102, 83, 71));
        canvas.Rect(1, 6, 6, 1, Color.FromArgb(184, 36, 44));
        canvas.Rect(9, 6, 6, 1, Color.FromArgb(184, 36, 44));
    }

    private static void DrawKitchenBench(PixelCanvas canvas, int variation)
    {
        var steel = variation % 2 == 0 ? Color.FromArgb(169, 176, 186) : Color.FromArgb(152, 160, 171);
        canvas.Rect(0, 0, 16, 16, Color.Transparent);
        canvas.Rect(1, 4, 14, 8, steel);
        canvas.Rect(1, 3, 14, 2, Color.FromArgb(216, 218, 223));
        canvas.Rect(2, 6, 12, 5, Color.FromArgb(120, 129, 140));
        canvas.VLine(7, 6, 10, Color.FromArgb(195, 200, 207));
    }

    private static void DrawPrepTable(PixelCanvas canvas, int variation)
    {
        var top = variation % 2 == 0 ? Color.FromArgb(223, 214, 202) : Color.FromArgb(207, 211, 216);
        var body = variation % 2 == 0 ? Color.FromArgb(145, 108, 73) : Color.FromArgb(121, 129, 140);
        canvas.Rect(0, 0, 16, 16, Color.Transparent);
        canvas.Rect(1, 3, 14, 3, top);
        canvas.Rect(1, 6, 14, 7, body);
        canvas.Rect(3, 8, 10, 3, Color.FromArgb(173, 130, 84));
        canvas.Rect(2, 13, 12, 1, Color.FromArgb(57, 49, 44));
        canvas.Rect(3, 5, 2, 1, Color.FromArgb(229, 93, 81));
        canvas.Rect(6, 5, 2, 1, Color.FromArgb(94, 180, 105));
        canvas.Rect(9, 5, 2, 1, Color.FromArgb(241, 199, 93));
    }

    private static void DrawShelf(PixelCanvas canvas, int variation)
    {
        var rack = Color.FromArgb(113, 94, 77);
        var box = variation % 2 == 0 ? Color.FromArgb(198, 167, 105) : Color.FromArgb(141, 181, 135);
        canvas.Rect(0, 0, 16, 16, Color.Transparent);
        canvas.Rect(2, 2, 12, 2, rack);
        canvas.Rect(2, 7, 12, 2, rack);
        canvas.Rect(2, 12, 12, 2, rack);
        canvas.Rect(3, 4, 4, 3, box);
        canvas.Rect(9, 4, 4, 3, Color.FromArgb(191, 112, 95));
        canvas.Rect(4, 9, 3, 3, Color.FromArgb(239, 216, 150));
        canvas.Rect(9, 9, 2, 3, box);
        canvas.VLine(2, 2, 13, rack);
        canvas.VLine(13, 2, 13, rack);
    }

    private static void DrawFridge(PixelCanvas canvas, int variation)
    {
        var body = variation % 2 == 0 ? Color.FromArgb(214, 220, 228) : Color.FromArgb(192, 210, 231);
        canvas.Rect(2, 1, 12, 14, body);
        canvas.Outline(2, 1, 12, 14, Color.FromArgb(111, 124, 143));
        canvas.VLine(8, 2, 14, Color.FromArgb(145, 156, 171));
        canvas.Rect(6, 5, 1, 3, Color.FromArgb(90, 107, 126));
        canvas.Rect(9, 9, 1, 3, Color.FromArgb(90, 107, 126));
    }

    private static void DrawTrashCan(PixelCanvas canvas, int variation)
    {
        var body = variation % 2 == 0 ? Color.FromArgb(94, 107, 121) : Color.FromArgb(76, 118, 98);
        canvas.Rect(4, 5, 8, 8, body);
        canvas.Rect(3, 4, 10, 2, Color.FromArgb(136, 146, 158));
        canvas.Rect(5, 13, 6, 1, Color.FromArgb(47, 54, 62));
    }

    private static void DrawFloorMat(PixelCanvas canvas, int variation)
    {
        var fill = variation switch
        {
            1 => Color.FromArgb(94, 112, 160),
            2 => Color.FromArgb(96, 154, 118),
            _ => Color.FromArgb(132, 92, 72)
        };
        var edge = Color.FromArgb(44, 35, 30);
        canvas.Rect(1, 9, 14, 5, fill);
        canvas.Outline(1, 9, 14, 5, edge);
        canvas.Rect(3, 10, 10, 1, Color.FromArgb(224, 223, 217));
        canvas.Rect(3, 12, 10, 1, Color.FromArgb(224, 223, 217));
    }

    private static void DrawCoffeeMachine(PixelCanvas canvas)
    {
        canvas.Rect(2, 4, 12, 8, Color.FromArgb(177, 55, 49));
        canvas.Rect(4, 5, 8, 4, Color.FromArgb(225, 227, 232));
        canvas.Rect(5, 10, 2, 2, Color.FromArgb(231, 208, 171));
        canvas.Rect(9, 10, 2, 2, Color.FromArgb(231, 208, 171));
        canvas.Rect(3, 12, 10, 2, Color.FromArgb(54, 49, 48));
    }

    private static void DrawExhaustHood(PixelCanvas canvas, int variation)
    {
        var steel = variation % 2 == 0 ? Color.FromArgb(196, 203, 210) : Color.FromArgb(176, 185, 193);
        canvas.Rect(0, 0, 16, 16, Color.Transparent);
        canvas.Rect(3, 2, 10, 3, steel);
        canvas.Rect(2, 5, 12, 4, Color.FromArgb(151, 160, 171));
        canvas.Rect(4, 9, 8, 2, Color.FromArgb(117, 126, 138));
        canvas.Rect(6, 11, 4, 3, Color.FromArgb(89, 98, 108));
        canvas.VLine(7, 0, 2, Color.FromArgb(226, 230, 235));
        canvas.VLine(8, 0, 2, Color.FromArgb(226, 230, 235));
    }

    private static Bitmap CreateSprite(Action<PixelCanvas> draw)
    {
        var bitmap = new Bitmap(BaseSpriteSize, BaseSpriteSize);
        using var canvas = new PixelCanvas(bitmap);
        draw(canvas);
        return bitmap;
    }

    private sealed class PixelCanvas : IDisposable
    {
        private readonly Bitmap bitmap;

        public PixelCanvas(Bitmap bitmap)
        {
            this.bitmap = bitmap;
            Clear(Color.Transparent);
        }

        public void Dispose()
        {
        }

        public void Clear(Color color)
        {
            for (var y = 0; y < bitmap.Height; y++)
            {
                for (var x = 0; x < bitmap.Width; x++)
                {
                    bitmap.SetPixel(x, y, color);
                }
            }
        }

        public void Pixel(int x, int y, Color color)
        {
            if (x < 0 || y < 0 || x >= bitmap.Width || y >= bitmap.Height)
            {
                return;
            }

            bitmap.SetPixel(x, y, color);
        }

        public void Rect(int x, int y, int width, int height, Color color)
        {
            for (var iy = y; iy < y + height; iy++)
            {
                for (var ix = x; ix < x + width; ix++)
                {
                    Pixel(ix, iy, color);
                }
            }
        }

        public void Outline(int x, int y, int width, int height, Color color)
        {
            HLine(x, x + width - 1, y, color);
            HLine(x, x + width - 1, y + height - 1, color);
            VLine(x, y, y + height - 1, color);
            VLine(x + width - 1, y, y + height - 1, color);
        }

        public void HLine(int x1, int x2, int y, Color color)
        {
            for (var x = x1; x <= x2; x++)
            {
                Pixel(x, y, color);
            }
        }

        public void VLine(int x, int y1, int y2, Color color)
        {
            for (var y = y1; y <= y2; y++)
            {
                Pixel(x, y, color);
            }
        }
    }
}
