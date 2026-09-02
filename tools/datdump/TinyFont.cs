namespace DatDump;

/// <summary>3x5 bitmap digits, so contact sheets can label each sprite with its index.</summary>
public static class TinyFont
{
    // One string per digit: 5 rows of 3 chars.
    private static readonly string[] Glyphs =
    [
        "###" + "#.#" + "#.#" + "#.#" + "###", // 0
        ".#." + "##." + ".#." + ".#." + "###", // 1
        "###" + "..#" + "###" + "#.." + "###", // 2
        "###" + "..#" + "###" + "..#" + "###", // 3
        "#.#" + "#.#" + "###" + "..#" + "..#", // 4
        "###" + "#.." + "###" + "..#" + "###", // 5
        "###" + "#.." + "###" + "#.#" + "###", // 6
        "###" + "..#" + "..#" + "..#" + "..#", // 7
        "###" + "#.#" + "###" + "#.#" + "###", // 8
        "###" + "#.#" + "###" + "..#" + "###", // 9
    ];

    public const int GlyphW = 3, GlyphH = 5, Advance = 4;

    public static int Width(string s) => s.Length * Advance;

    /// <summary>Draws decimal digits into an RGBA buffer.</summary>
    public static void Draw(byte[] rgba, int bufW, int bufH, int x, int y, string text,
                            byte r, byte g, byte b)
    {
        foreach (char c in text)
        {
            if (c is >= '0' and <= '9')
            {
                string gl = Glyphs[c - '0'];
                for (int row = 0; row < GlyphH; row++)
                    for (int col = 0; col < GlyphW; col++)
                    {
                        if (gl[row * GlyphW + col] != '#') continue;
                        int px = x + col, py = y + row;
                        if (px < 0 || py < 0 || px >= bufW || py >= bufH) continue;
                        int o = (py * bufW + px) * 4;
                        rgba[o] = r; rgba[o + 1] = g; rgba[o + 2] = b; rgba[o + 3] = 255;
                    }
            }
            x += Advance;
        }
    }
}
