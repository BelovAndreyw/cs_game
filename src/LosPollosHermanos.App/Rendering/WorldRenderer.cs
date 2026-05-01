using LosPollosHermanos.Model;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Numerics;

namespace LosPollosHermanos.App.Rendering;

public sealed class WorldRenderer
{
    private readonly int cellSize;
    private readonly GameSpriteLibrary spriteLibrary;

    public WorldRenderer(int cellSize, GameSpriteLibrary spriteLibrary)
    {
        this.cellSize = cellSize;
        this.spriteLibrary = spriteLibrary;
    }

    public void Draw(
        Graphics g,
        GameSnapshot snapshot,
        Rectangle viewport,
        Vector2 cameraPosition,
        IReadOnlyList<InteractionPulse> pulses,
        PlayerAnimationFrame playerFrame,
        StationType? objectiveStation)
    {
        var state = g.Save();
        g.SetClip(viewport);
        g.TranslateTransform(viewport.Left - cameraPosition.X, viewport.Top - cameraPosition.Y);
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.SmoothingMode = SmoothingMode.None;

        DrawFloor(g, snapshot);
        DrawAtmosphere(g, snapshot);
        DrawSceneProps(g, snapshot.SceneProps.Where(x => x.Type == ScenePropType.FloorMat));
        DrawSceneProps(g, snapshot.SceneProps.Where(IsBackWallProp));
        DrawSceneProps(g, snapshot.SceneProps.Where(x => !IsBackWallProp(x) && x.Type != ScenePropType.FloorMat));
        DrawStations(g, snapshot, objectiveStation);
        DrawPulses(g, pulses);
        DrawNpcs(g, snapshot);
        DrawPlayer(g, snapshot, playerFrame);
        g.Restore(state);
    }

    private void DrawFloor(Graphics g, GameSnapshot snapshot)
    {
        for (var y = 0; y < snapshot.MapHeight; y++)
        {
            for (var x = 0; x < snapshot.MapWidth; x++)
            {
                var tileKind = y < snapshot.KitchenStartRow - 2
                    ? FloorTileKind.Dining
                    : y < snapshot.KitchenStartRow
                        ? FloorTileKind.Threshold
                        : FloorTileKind.Kitchen;

                var variation = Math.Abs(((x * 37) + (y * 17)) % 4);
                var tile = spriteLibrary.GetFloorTile(tileKind, variation);
                g.DrawImage(tile, x * cellSize, y * cellSize, cellSize, cellSize);
            }
        }
    }

    private void DrawAtmosphere(Graphics g, GameSnapshot snapshot)
    {
        var worldWidth = snapshot.MapWidth * cellSize;
        var counterY = (snapshot.KitchenStartRow - 2) * cellSize;
        var hallHeight = counterY;
        var warmTop = new RectangleF(cellSize * 2f, cellSize * 1.4f, worldWidth - cellSize * 4f, hallHeight * 0.65f);
        var serviceLane = new RectangleF(cellSize * 8f, cellSize * 5.4f, cellSize * 10.5f, cellSize * 1.8f);
        var kitchenGlow = new RectangleF(cellSize * 8f, counterY + cellSize * 2f, cellSize * 9f, cellSize * 5f);

        using var warmBrush = new SolidBrush(Color.FromArgb(24, 255, 207, 142));
        using var amberBrush = new SolidBrush(Color.FromArgb(26, 226, 114, 76));
        using var coolBrush = new SolidBrush(Color.FromArgb(16, 116, 188, 230));
        using var dividerTop = new SolidBrush(Color.FromArgb(88, 209, 186, 153));
        using var dividerFront = new SolidBrush(Color.FromArgb(124, 86, 59, 44));
        using var queueRunner = new SolidBrush(Color.FromArgb(34, 246, 196, 88));
        using var hallShadow = new SolidBrush(Color.FromArgb(50, 0, 0, 0));

        g.FillEllipse(warmBrush, warmTop);
        g.FillEllipse(amberBrush, serviceLane);
        g.FillEllipse(coolBrush, kitchenGlow);
        g.FillRectangle(queueRunner, serviceLane);
        g.FillRectangle(dividerTop, 0, counterY - 6, worldWidth, 8);
        g.FillRectangle(dividerFront, 0, counterY + 2, worldWidth, cellSize / 2f);
        g.FillRectangle(hallShadow, 0, hallHeight - cellSize * 0.45f, worldWidth, cellSize * 0.45f);
    }

    private void DrawSceneProps(Graphics g, IEnumerable<ScenePropSnapshot> props)
    {
        foreach (var prop in props.OrderBy(x => x.Position.Y).ThenBy(x => x.Position.X))
        {
            var rect = new Rectangle(prop.Position.X * cellSize, prop.Position.Y * cellSize, cellSize, cellSize);
            if (NeedsShadow(prop.Type))
            {
                DrawPropShadow(g, rect, prop.Type);
            }

            g.DrawImage(spriteLibrary.GetScenePropSprite(prop.Type, prop.Variant), rect);
        }
    }

    private void DrawStations(Graphics g, GameSnapshot snapshot, StationType? objectiveStation)
    {
        var state = g.Save();
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        using var labelFont = GameTheme.CreateBodyFont(8.6f, FontStyle.Bold);
        using var labelBrush = new SolidBrush(GameTheme.TextPrimary);

        foreach (var station in snapshot.Stations)
        {
            var cellRect = new Rectangle(station.Position.X * cellSize, station.Position.Y * cellSize, cellSize, cellSize);
            var spriteRect = Rectangle.Inflate(cellRect, -2, -2);
            var accent = GameTheme.GetStationAccent(station.Type);
            var isObjective = objectiveStation is not null && station.Type == objectiveStation;
            var isCurrent = snapshot.PlayerPosition == station.Position || snapshot.InteractionStation == station.Type;

            using var shadow = new SolidBrush(Color.FromArgb(90, 0, 0, 0));
            g.FillEllipse(shadow, cellRect.X + 8, cellRect.Bottom - 13, cellRect.Width - 16, 10);

            if (isObjective)
            {
                using var glow = new SolidBrush(Color.FromArgb(70, accent));
                using var outline = new Pen(Color.FromArgb(220, accent), 2f);
                g.FillEllipse(glow, cellRect.X - 7, cellRect.Y - 7, cellRect.Width + 14, cellRect.Height + 14);
                g.DrawEllipse(outline, cellRect.X - 5, cellRect.Y - 5, cellRect.Width + 10, cellRect.Height + 10);
            }
            else if (isCurrent)
            {
                using var outline = new Pen(Color.FromArgb(150, accent), 1.6f);
                g.DrawEllipse(outline, cellRect.X - 2, cellRect.Y - 2, cellRect.Width + 4, cellRect.Height + 4);
            }

            g.DrawImage(spriteLibrary.GetStationSprite(station.Type), spriteRect);

            if (isObjective || isCurrent)
            {
                DrawStationCallout(g, labelFont, labelBrush, cellRect, accent, station.Name);
            }
        }

        g.Restore(state);
    }

    private void DrawStationCallout(Graphics g, Font font, Brush textBrush, Rectangle cellRect, Color accent, string label)
    {
        var rect = new RectangleF(cellRect.X - 14f, cellRect.Y - 18f, cellRect.Width + 28f, 16f);
        RenderPrimitives.FillRoundedPanel(g, rect, Color.FromArgb(216, 20, 18, 18), Color.FromArgb(180, accent), radius: 8f, borderWidth: 1f);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        g.DrawString(label, font, textBrush, rect, format);
    }

    private void DrawNpcs(Graphics g, GameSnapshot snapshot)
    {
        var state = g.Save();
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        using var nameFont = GameTheme.CreateMonoFont(8.3f, FontStyle.Bold);
        using var nameBrush = new SolidBrush(GameTheme.TextPrimary);
        using var speechFont = GameTheme.CreateBodyFont(8.5f);
        using var speechBrush = new SolidBrush(Color.FromArgb(57, 44, 35));

        foreach (var npc in snapshot.Npcs.OrderBy(x => x.Position.Y).ThenBy(x => x.Position.X))
        {
            var cellRect = new Rectangle(npc.Position.X * cellSize, npc.Position.Y * cellSize, cellSize, cellSize);
            var spriteRect = new Rectangle(cellRect.X + 4, cellRect.Y + 2, cellRect.Width - 8, cellRect.Height - 4);
            var variation = StableHash(npc.Name) % 4;

            using var shadow = new SolidBrush(Color.FromArgb(80, 0, 0, 0));
            g.FillEllipse(shadow, cellRect.X + 10, cellRect.Bottom - 12, cellRect.Width - 20, 8);
            g.DrawImage(spriteLibrary.GetNpcSprite(npc.Role, variation), spriteRect);

            var nameSize = g.MeasureString(npc.Name, nameFont);
            var nameRect = new RectangleF(
                cellRect.X + (cellRect.Width - nameSize.Width) / 2f - 6f,
                cellRect.Y - 16f,
                nameSize.Width + 12f,
                nameSize.Height + 2f);

            RenderPrimitives.FillRoundedPanel(g, nameRect, Color.FromArgb(218, 18, 22, 31), Color.FromArgb(148, 232, 194, 109), radius: 8f, borderWidth: 1f);
            g.DrawString(npc.Name, nameFont, nameBrush, nameRect.X + 6f, nameRect.Y + 1f);

            if (!string.IsNullOrWhiteSpace(npc.Speech))
            {
                var bubbleWidth = Math.Min(cellSize * 4.1f, 190f);
                var bubbleRect = new RectangleF(cellRect.X - (bubbleWidth - cellRect.Width) / 2f, cellRect.Y - 56f, bubbleWidth, 34f);
                RenderPrimitives.FillRoundedPanel(g, bubbleRect, Color.FromArgb(244, 244, 236, 220), Color.FromArgb(145, 121, 101, 86), radius: 12f, borderWidth: 1f);
                using var tailBrush = new SolidBrush(Color.FromArgb(244, 244, 236, 220));
                g.FillPolygon(
                    tailBrush,
                    new[]
                    {
                        new PointF(cellRect.X + cellRect.Width / 2f - 6f, bubbleRect.Bottom - 2f),
                        new PointF(cellRect.X + cellRect.Width / 2f + 6f, bubbleRect.Bottom - 2f),
                        new PointF(cellRect.X + cellRect.Width / 2f, bubbleRect.Bottom + 8f)
                    });
                RenderPrimitives.DrawWrappedText(g, npc.Speech, speechFont, speechBrush, new RectangleF(bubbleRect.X + 8f, bubbleRect.Y + 6f, bubbleRect.Width - 16f, bubbleRect.Height - 12f));
            }
        }

        g.Restore(state);
    }

    private void DrawPulses(Graphics g, IReadOnlyList<InteractionPulse> pulses)
    {
        var state = g.Save();
        g.SmoothingMode = SmoothingMode.AntiAlias;

        foreach (var pulse in pulses)
        {
            var radius = cellSize * 0.35f + pulse.Progress * pulse.MaxRadius;
            var alpha = (int)(185 * (1f - pulse.Progress));
            using var ring = new Pen(Color.FromArgb(Math.Clamp(alpha, 0, 255), pulse.Color), 2.2f);
            g.DrawEllipse(ring, pulse.WorldPosition.X - radius, pulse.WorldPosition.Y - radius, radius * 2f, radius * 2f);
        }

        g.Restore(state);
    }

    private void DrawPlayer(Graphics g, GameSnapshot snapshot, PlayerAnimationFrame playerFrame)
    {
        var cellRect = new Rectangle(snapshot.PlayerPosition.X * cellSize, snapshot.PlayerPosition.Y * cellSize, cellSize, cellSize);
        var spriteRect = new Rectangle(cellRect.X + 3, cellRect.Y + 1, cellRect.Width - 6, cellRect.Height - 2);
        using var shadow = new SolidBrush(Color.FromArgb(88, 0, 0, 0));
        g.FillEllipse(shadow, cellRect.X + 10, cellRect.Bottom - 12, cellRect.Width - 20, 8);
        g.DrawImage(spriteLibrary.GetPlayerSprite(playerFrame), spriteRect);
    }

    private void DrawPropShadow(Graphics g, Rectangle rect, ScenePropType type)
    {
        if (type == ScenePropType.Wall || type == ScenePropType.Window || type == ScenePropType.MenuBoard || type == ScenePropType.NeonSign)
        {
            return;
        }

        using var shadow = new SolidBrush(Color.FromArgb(72, 0, 0, 0));
        var shadowRect = type switch
        {
            ScenePropType.Counter or ScenePropType.KitchenBench or ScenePropType.Shelf or ScenePropType.Fridge =>
                new RectangleF(rect.X + 4, rect.Bottom - 12, rect.Width - 8, 8),
            ScenePropType.Booth =>
                new RectangleF(rect.X + 3, rect.Bottom - 10, rect.Width - 6, 7),
            ScenePropType.Table =>
                new RectangleF(rect.X + 8, rect.Y + rect.Height * 0.55f, rect.Width - 16, 11),
            _ => new RectangleF(rect.X + 9, rect.Bottom - 11, rect.Width - 18, 8)
        };

        g.FillEllipse(shadow, shadowRect);
    }

    private static bool IsBackWallProp(ScenePropSnapshot prop)
    {
        return prop.Type is ScenePropType.Wall
            or ScenePropType.Window
            or ScenePropType.Door
            or ScenePropType.Counter
            or ScenePropType.MenuBoard
            or ScenePropType.NeonSign
            or ScenePropType.ExhaustHood;
    }

    private static bool NeedsShadow(ScenePropType type)
    {
        return type is not ScenePropType.FloorMat
            and not ScenePropType.Wall
            and not ScenePropType.Window
            and not ScenePropType.MenuBoard
            and not ScenePropType.NeonSign
            and not ScenePropType.ExhaustHood;
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            var hash = 17;
            foreach (var ch in value)
            {
                hash = (hash * 31) + ch;
            }

            return Math.Abs(hash);
        }
    }
}
