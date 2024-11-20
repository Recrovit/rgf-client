namespace Recrovit.RecroGridFramework.Blazor.RgfApexCharts;

public static class RgfColorPalettes
{
    public static Dictionary<string, List<string>> ColorPalettes => new()
    {
        {"RGF Default", DefaultPalette },
        {"RGF Rainbow", Rainbow },
        {"RGF Darker First", DarkerFirstPalette },
        {"RGF Lighter First", LighterFirstPalette },
    };

    public static readonly List<string> DefaultPalette =
    [
        "#1E90FF", // Dodger Blue
        "#FF4500", // Orange Red
        "#32CD32", // Lime Green
        "#FFD700", // Gold
        "#8A2BE2", // Blue Violet
        "#FF1493", // Deep Pink
        "#00CED1", // Dark Turquoise
        "#FFA500", // Orange
        "#7CFC00", // Lawn Green
        "#DC143C", // Crimson
        "#4682B4", // Steel Blue
        "#FF6347", // Tomato
        "#6A5ACD", // Slate Blue
        "#40E0D0", // Turquoise
        "#DAA520", // Goldenrod
        "#EE82EE", // Violet
        "#5F9EA0", // Cadet Blue
        "#FF69B4", // Hot Pink
        "#2E8B57", // Sea Green
        "#BA55D3", // Medium Orchid
        "#9ACD32", // Yellow Green
        "#87CEFA", // Light Sky Blue
        "#CD5C5C", // Indian Red
        "#B8860B"  // Dark Goldenrod
    ];

    public static readonly List<string> Rainbow =
    [
        // Darker Red shade
        "#B22222", // Firebrick (Darker Red)

        // Orange shades
        "#FF6347", // Tomato
        "#FFA500", // Orange
        "#FF8C00", // Dark Orange

        // Yellow shades
        "#FFFF00", // Yellow
        "#FFD700", // Gold

        // Green shades
        "#32CD32", // Lime Green
        "#00FF00", // Green

        // Turquoise shades with a larger difference
        "#40E0D0", // Turquoise
        "#008B8B", // Dark Cyan (darker Turquoise)

        // Blue shades
        "#1E90FF", // Dodger Blue
        "#0000FF", // Blue

        // Purple shades with a larger difference
        "#8A2BE2", // Blue Violet
        "#BA55D3", // Medium Orchid

        // Pink shades
        "#FF69B4", // Hot Pink
        "#FF1493"  // Deep Pink
    ];

    public static readonly List<string> DarkerFirstPalette =
    [
        "#00008B", // Dark Blue
        "#8B0000", // Dark Red
        "#006400", // Dark Green
        "#8B4513", // Saddle Brown
        "#4B0082", // Indigo
        "#800080", // Purple
        "#2F4F4F", // Dark Slate Gray
        "#B8860B",  // Dark Goldenrod

        "#1E90FF", // Dodger Blue
        "#FF4500", // Orange Red
        "#32CD32", // Lime Green
        "#FFD700", // Gold
        "#8A2BE2", // Blue Violet
        "#FF1493", // Deep Pink
        "#00CED1", // Dark Turquoise
        "#FFA500", // Orange

        "#ADD8E6", // Light Blue
        "#FFB6C1", // Light Pink
        "#98FB98", // Pale Green
        "#FFFF00", // Yellow
        "#DDA0DD", // Plum
        "#FFDEAD", // Navajo White
        "#AFEEEE", // Pale Turquoise
        "#FFE4B5", // Moccasin
    ];

    public static readonly List<string> LighterFirstPalette =
    [
        "#ADD8E6", // Light Blue
        "#FFB6C1", // Light Pink
        "#98FB98", // Pale Green
        "#FFFF00", // Yellow
        "#DDA0DD", // Plum
        "#FFDEAD", // Navajo White
        "#AFEEEE", // Pale Turquoise
        "#FFE4B5", // Moccasin

        "#1E90FF", // Dodger Blue
        "#FF4500", // Orange Red
        "#32CD32", // Lime Green
        "#FFD700", // Gold
        "#8A2BE2", // Blue Violet
        "#FF1493", // Deep Pink
        "#00CED1", // Dark Turquoise
        "#FFA500", // Orange

        "#00008B", // Dark Blue
        "#8B0000", // Dark Red
        "#006400", // Dark Green
        "#8B4513", // Saddle Brown
        "#4B0082", // Indigo
        "#800080", // Purple
        "#2F4F4F", // Dark Slate Gray
        "#B8860B",  // Dark Goldenrod
    ];
}
