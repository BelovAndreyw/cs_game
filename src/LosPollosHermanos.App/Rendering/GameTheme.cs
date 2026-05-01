using LosPollosHermanos.Model;

namespace LosPollosHermanos.App.Rendering;

public static class GameTheme
{
    public const float PanelRadius = 20f;
    public const int PanelPadding = 18;
    public const int PanelGap = 14;
    public const int SmallGap = 8;

    public static readonly Color WindowBackground = Color.FromArgb(13, 15, 20);
    public static readonly Color ViewportBorder = Color.FromArgb(92, 72, 56);
    public static readonly Color HudBackgroundTop = Color.FromArgb(34, 24, 18);
    public static readonly Color HudBackgroundBottom = Color.FromArgb(20, 15, 12);
    public static readonly Color PanelFill = Color.FromArgb(230, 34, 28, 24);
    public static readonly Color PanelFillMuted = Color.FromArgb(222, 42, 34, 28);
    public static readonly Color PanelBorder = Color.FromArgb(126, 150, 125, 101);
    public static readonly Color TextPrimary = Color.FromArgb(249, 242, 230);
    public static readonly Color TextSecondary = Color.FromArgb(224, 207, 187);
    public static readonly Color TextMuted = Color.FromArgb(170, 155, 138);
    public static readonly Color Accent = Color.FromArgb(246, 196, 88);
    public static readonly Color Warning = Color.FromArgb(242, 143, 85);
    public static readonly Color Danger = Color.FromArgb(224, 96, 88);
    public static readonly Color Success = Color.FromArgb(122, 205, 141);
    public static readonly Color Info = Color.FromArgb(116, 188, 230);
    public static readonly Color OverlayVeil = Color.FromArgb(212, 7, 9, 12);

    public static Font CreateDisplayFont(float size)
    {
        return new Font("Bahnschrift SemiBold", size, FontStyle.Bold, GraphicsUnit.Point);
    }

    public static Font CreateHeadingFont(float size)
    {
        return new Font("Bahnschrift SemiBold", size, FontStyle.Bold, GraphicsUnit.Point);
    }

    public static Font CreateBodyFont(float size, FontStyle style = FontStyle.Regular)
    {
        return new Font("Segoe UI", size, style, GraphicsUnit.Point);
    }

    public static Font CreateMonoFont(float size, FontStyle style = FontStyle.Regular)
    {
        return new Font("Consolas", size, style, GraphicsUnit.Point);
    }

    public static Color GetStationAccent(StationType type)
    {
        return type switch
        {
            StationType.OrderDesk => Color.FromArgb(108, 196, 180),
            StationType.Grill => Color.FromArgb(232, 114, 76),
            StationType.Assembly => Color.FromArgb(222, 118, 148),
            StationType.Fryer => Color.FromArgb(239, 185, 76),
            StationType.Drinks => Color.FromArgb(105, 160, 234),
            StationType.ServingCounter => Color.FromArgb(124, 208, 128),
            _ => TextMuted
        };
    }

    public static Color GetDifficultyColor(ShiftDifficulty difficulty)
    {
        return difficulty switch
        {
            ShiftDifficulty.Easy => Success,
            ShiftDifficulty.Medium => Warning,
            ShiftDifficulty.Hard => Danger,
            _ => TextMuted
        };
    }
}
