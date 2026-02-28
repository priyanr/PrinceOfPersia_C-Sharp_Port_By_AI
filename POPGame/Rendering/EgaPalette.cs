using Raylib_cs;

namespace POPGame.Rendering;

/// <summary>
/// The 16 canonical EGA hardware colors, mapped to Raylib Color values.
/// </summary>
public static class EgaPalette
{
    private static Color C(int r, int g, int b)
        => new Color { R = (byte)r, G = (byte)g, B = (byte)b, A = 255 };

    public static readonly Color Black       = C(0, 0, 0);
    public static readonly Color DarkBlue    = C(0, 0, 170);
    public static readonly Color DarkGreen   = C(0, 170, 0);
    public static readonly Color DarkCyan    = C(0, 170, 170);
    public static readonly Color DarkRed     = C(170, 0, 0);
    public static readonly Color DarkMagenta = C(170, 0, 170);
    public static readonly Color Brown       = C(170, 85, 0);
    public static readonly Color Gray        = C(170, 170, 170);
    public static readonly Color DarkGray    = C(85, 85, 85);
    public static readonly Color Blue        = C(85, 85, 255);
    public static readonly Color Green       = C(85, 255, 85);
    public static readonly Color Cyan        = C(85, 255, 255);
    public static readonly Color Red         = C(255, 85, 85);
    public static readonly Color Magenta     = C(255, 85, 255);
    public static readonly Color Yellow      = C(255, 255, 85);
    public static readonly Color White       = C(255, 255, 255);
}
