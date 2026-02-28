using Raylib_cs;

namespace POPGame.Data;

/// <summary>
/// Decodes Apple II Hi-Res CHTAB/BGTAB binary image tables into RGBA pixel arrays.
/// Each byte encodes 7 pixels via NTSC artifact coloring (bit 7 selects palette).
/// </summary>
public static class Apple2Image
{
    public struct DecodedSprite
    {
        public int WidthPx;
        public int Height;
        public Color[] Pixels;
    }

    // NTSC artifact colors
    private static readonly Color Black  = new(0, 0, 0, 255);
    private static readonly Color White  = new(255, 255, 255, 255);
    private static readonly Color Green  = new(20, 245, 60, 255);
    private static readonly Color Violet = new(255, 68, 253, 255);
    private static readonly Color Orange = new(255, 106, 60, 255);
    private static readonly Color Blue   = new(20, 207, 253, 255);

    /// <summary>
    /// Parse a CHTAB/BGTAB file. Returns a 1-indexed list (index 0 is empty).
    /// </summary>
    public static List<DecodedSprite> LoadTable(string filePath, ushort loadAddress)
    {
        byte[] data = File.ReadAllBytes(filePath);
        int count = data[0];
        var sprites = new List<DecodedSprite>(count + 1);
        // Index 0 placeholder (images are 1-based)
        sprites.Add(default);

        for (int img = 1; img <= count; img++)
        {
            int ptrIdx = img * 2 - 1;
            if (ptrIdx + 1 >= data.Length)
            {
                sprites.Add(default);
                continue;
            }

            ushort addr = (ushort)(data[ptrIdx] | (data[ptrIdx + 1] << 8));
            int offset = addr - loadAddress;

            if (offset < 0 || offset + 2 > data.Length)
            {
                sprites.Add(default);
                continue;
            }

            int widthBytes = data[offset];
            int height = data[offset + 1];
            int dataStart = offset + 2;

            if (widthBytes == 0 || height == 0 || dataStart + widthBytes * height > data.Length)
            {
                sprites.Add(default);
                continue;
            }

            var pixels = DecodeHiRes(data, dataStart, widthBytes, height);
            sprites.Add(new DecodedSprite
            {
                WidthPx = widthBytes * 7,
                Height = height,
                Pixels = pixels
            });
        }

        return sprites;
    }

    /// <summary>
    /// Decode Apple II Hi-Res bytes to RGBA pixels using NTSC artifact color approximation.
    /// </summary>
    public static Color[] DecodeHiRes(byte[] data, int offset, int widthBytes, int height)
    {
        int widthPx = widthBytes * 7;
        var pixels = new Color[widthPx * height];

        for (int y = 0; y < height; y++)
        {
            for (int bx = 0; bx < widthBytes; bx++)
            {
                int byteIdx = offset + y * widthBytes + bx;
                if (byteIdx >= data.Length) continue;

                byte b = data[byteIdx];
                bool palette = (b & 0x80) != 0; // bit 7 selects color group
                int basePixelX = bx * 7;

                for (int bit = 0; bit < 7; bit++)
                {
                    bool thisOn = (b & (1 << bit)) != 0;
                    int pixelX = basePixelX + bit;

                    // Determine absolute pixel column for even/odd classification
                    int absCol = pixelX;
                    bool isEven = (absCol % 2) == 0;

                    // Look at adjacent bits for white/black determination
                    bool prevOn = false;
                    bool nextOn = false;

                    if (bit > 0)
                        prevOn = (b & (1 << (bit - 1))) != 0;
                    else if (bx > 0)
                    {
                        byte prevByte = data[offset + y * widthBytes + bx - 1];
                        prevOn = (prevByte & (1 << 6)) != 0;
                    }

                    if (bit < 6)
                        nextOn = (b & (1 << (bit + 1))) != 0;
                    else if (bx + 1 < widthBytes)
                    {
                        byte nextByte = data[offset + y * widthBytes + bx + 1];
                        nextOn = (nextByte & 1) != 0;
                    }

                    Color color;
                    if (!thisOn)
                    {
                        color = Black;
                    }
                    else if (prevOn || nextOn)
                    {
                        // Two consecutive ON bits → white
                        color = White;
                    }
                    else
                    {
                        // Single ON bit → artifact color based on column parity and palette
                        if (!palette)
                            color = isEven ? Violet : Green;
                        else
                            color = isEven ? Blue : Orange;
                    }

                    pixels[(height - 1 - y) * widthPx + pixelX] = color;
                }
            }
        }

        return pixels;
    }

    /// <summary>
    /// Create a Raylib Image from a decoded sprite.
    /// </summary>
    public static Image ToRaylibImage(DecodedSprite sprite)
        => ToRaylibImage(sprite, transparentBlack: false);

    /// <summary>
    /// Create a Raylib Image from a decoded sprite, optionally treating black as transparent.
    /// </summary>
    public static Image ToRaylibImage(DecodedSprite sprite, bool transparentBlack)
    {
        if (sprite.Pixels == null || sprite.WidthPx == 0 || sprite.Height == 0)
            return default;

        var img = Raylib.GenImageColor(sprite.WidthPx, sprite.Height,
            transparentBlack ? new Color(0, 0, 0, 0) : Black);
        for (int y = 0; y < sprite.Height; y++)
        {
            for (int x = 0; x < sprite.WidthPx; x++)
            {
                var c = sprite.Pixels[y * sprite.WidthPx + x];
                if (transparentBlack && c.R == 0 && c.G == 0 && c.B == 0)
                    c = new Color(0, 0, 0, 0);
                Raylib.ImageDrawPixel(ref img, x, y, c);
            }
        }
        return img;
    }

    /// <summary>
    /// Create a Raylib Texture2D from a decoded sprite. Must be called after InitWindow.
    /// </summary>
    public static Texture2D ToTexture(DecodedSprite sprite)
        => ToTexture(sprite, transparentBlack: false);

    /// <summary>
    /// Create a Raylib Texture2D from a decoded sprite with optional black transparency.
    /// </summary>
    public static Texture2D ToTexture(DecodedSprite sprite, bool transparentBlack)
    {
        if (sprite.Pixels == null || sprite.WidthPx == 0 || sprite.Height == 0)
            return default;

        var img = ToRaylibImage(sprite, transparentBlack);
        var tex = Raylib.LoadTextureFromImage(img);
        Raylib.UnloadImage(img);
        if (tex.Id != 0)
            Raylib.SetTextureFilter(tex, TextureFilter.Point);
        return tex;
    }
}
