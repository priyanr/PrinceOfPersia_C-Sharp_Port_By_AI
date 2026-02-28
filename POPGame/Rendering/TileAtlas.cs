using Raylib_cs;
using POPGame.Data;

namespace POPGame.Rendering;

/// <summary>
/// Maps tile IDs (0-29) to composite background textures built from BGTAB image layers.
/// Data from BGDATA.S: piecea/pieceb/piecec/pieced define which BGTAB images compose each tile.
/// Bit 7 of image index selects bgtable2 (set) vs bgtable1 (clear). 0 = no image.
/// </summary>
public static class TileAtlas
{
    // Section A images (upper-left area of tile, at BlockBot-3)
    private static readonly byte[] PieceA =
    [
        0x00, 0x01, 0x05, 0x07, 0x0a, 0x01, 0x01, 0x0a, 0x10, 0x00, 0x01, 0x00, 0x00, 0x14, 0x20, 0x4b,
        0x01, 0x00, 0x00, 0x01, 0x00, 0x97, 0x00, 0x01, 0x00, 0xa7, 0xa9, 0xaa, 0xac, 0xad
    ];

    // Section A Y offsets
    private static readonly int[] PieceAY =
    [
        0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, -4, -4, -4
    ];

    // Section B images (behind/overlay, at BlockBot-3)
    private static readonly byte[] PieceB =
    [
        0x00, 0x02, 0x06, 0x08, 0x0b, 0x1b, 0x02, 0x9e, 0x1a, 0x1c, 0x02, 0x00, 0x9e, 0x4a, 0x21, 0x1b,
        0x4d, 0x4e, 0x02, 0x51, 0x84, 0x98, 0x02, 0x91, 0x92, 0x02, 0x00, 0x00, 0x00, 0x00
    ];

    // Section B Y offsets
    private static readonly int[] PieceBY =
    [
        0, 0, 0, 0, 0, 1, 0, 3, 0, 3, 0, 0, 3, 0, 0, -1,
        0, 0, 0, -1, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0
    ];

    // Section C images (lower area, at BlockBot)
    private static readonly byte[] PieceC =
    [
        0x00, 0x00, 0x00, 0x09, 0x0c, 0x00, 0x00, 0x9f, 0x00, 0x1d, 0x00, 0x00, 0x9f, 0x00, 0x00, 0x00,
        0x4f, 0x50, 0x00, 0x00, 0x85, 0x00, 0x00, 0x93, 0x94, 0x00, 0x00, 0x00, 0x00, 0x00
    ];

    // Section D images (floor/base layer, at BlockBot)
    private static readonly byte[] PieceD =
    [
        0x00, 0x15, 0x15, 0x15, 0x15, 0x18, 0x19, 0x16, 0x15, 0x00, 0x15, 0x00, 0x17, 0x15, 0x2e, 0x4c,
        0x15, 0x15, 0x15, 0x15, 0x86, 0x15, 0x15, 0x15, 0x15, 0x15, 0xab, 0x00, 0x00, 0x00
    ];

    // Foreground images (drawn on top, at position offset by frontx/fronty)
    private static readonly byte[] FrontI =
    [
        0x00, 0x00, 0x00, 0x45, 0x46, 0x00, 0x00, 0x46, 0x48, 0x49, 0x87, 0x00, 0x46, 0x0f, 0x13, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x83, 0x00, 0x00, 0x00, 0x00, 0xa8, 0x00, 0xae, 0xae, 0xae
    ];

    // B-stripe image
    private static readonly byte[] BStripe =
    [
        0x00, 0x47, 0x47, 0x00, 0x00, 0x47, 0x47, 0x00, 0x00, 0x00, 0x47, 0x47, 0x00, 0x00, 0x47, 0x47,
        0x00, 0x00, 0x47, 0x00, 0x00, 0x00, 0x47, 0x00, 0x00, 0x47, 0x00, 0x00, 0x00, 0x00
    ];

    // Mask images for section A
    private static readonly byte[] MaskA =
    [
        0x00, 0x03, 0x03, 0x03, 0x03, 0x03, 0x03, 0x03, 0x03, 0x00, 0x03, 0x03, 0x00, 0x03, 0x03, 0x03,
        0x03, 0x00, 0x00, 0x03, 0x00, 0x03, 0x00, 0x03, 0x00, 0x03, 0x00, 0x00, 0x00, 0x00
    ];

    // Mask images for section B
    private static readonly byte[] MaskB =
    [
        0x00, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x00, 0x04, 0x00, 0x04, 0x00, 0x00, 0x04, 0x04, 0x04,
        0x00, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04, 0x00, 0x04, 0x04, 0x00, 0x00, 0x00, 0x00
    ];

    // Foreground Y offsets
    private static readonly int[] FrontY =
    [
        0, 0, 0, -1, 0, 0, 0, 0, -1, 3, -3, 0, 0, -1, 0, 0,
        0, 0, 0, 0, 0, 0, 0, 0, 0, -1, 0, -36, -36, -36
    ];

    // Foreground X offsets (in bytes, multiply by 7 for pixels)
    private static readonly byte[] FrontX =
    [
        0x00, 0x00, 0x00, 0x01, 0x03, 0x00, 0x00, 0x03, 0x01, 0x01, 0x02, 0x00, 0x03, 0x01, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00
    ];

    // Apple II tile cell = 4 bytes wide × 63 scanlines (sections A+B at top, C+D at bottom)
    // We render into a 32×72 Raylib tile cell by scaling/mapping.
    public const int AppleTileWidthBytes = 4;
    public const int AppleTileWidthPx = 28; // 4 × 7
    public const int AppleTileHeight = 63;  // 3 scanlines per "row" section

    // Cached textures per tile ID (0-29)
    private static Texture2D[] _tileTextures = new Texture2D[30];
    private static Texture2D[] _frontTextures = new Texture2D[30]; // foreground layers
    private static bool _loaded;

    // Raw decoded sprites from both tables
    private static List<Apple2Image.DecodedSprite> _bgTab1 = null!;
    private static List<Apple2Image.DecodedSprite> _bgTab2 = null!;

    public static bool IsLoaded => _loaded;

    public static void Load(string imagesDir)
    {
        string tab1Path = Path.Combine(imagesDir, "IMG.BGTAB1.DUN");
        string tab2Path = Path.Combine(imagesDir, "IMG.BGTAB2.DUN");

        if (!File.Exists(tab1Path) || !File.Exists(tab2Path))
            return;

        _bgTab1 = Apple2Image.LoadTable(tab1Path, 0x6000);
        _bgTab2 = Apple2Image.LoadTable(tab2Path, 0x8400);

        for (int tileId = 0; tileId < 30; tileId++)
        {
            BuildTileTexture(tileId);
        }

        _loaded = true;
    }

    private static Apple2Image.DecodedSprite GetBgSprite(byte imgIndex)
    {
        if (imgIndex == 0) return default;

        bool useTab2 = (imgIndex & 0x80) != 0;
        int idx = imgIndex & 0x7F;

        var table = useTab2 ? _bgTab2 : _bgTab1;
        if (table == null || idx < 0 || idx >= table.Count)
            return default;

        return table[idx];
    }

    // Foreground offset data stored per tile for TileSprites to use
    private static int[] _frontCanvasX = new int[30];
    private static int[] _frontCanvasY = new int[30];

    private static void BuildTileTexture(int tileId)
    {
        const int cw = AppleTileWidthPx; // 28
        const int ch = AppleTileHeight;  // 63

        var canvas = new Color[cw * ch];
        for (int i = 0; i < canvas.Length; i++)
            canvas[i] = new Color(0, 0, 0, 255);

        // Apple II anchor positions (bottom edge Y coordinates on canvas)
        const int Dy = 62; // BlockBot — bottom of D/C sections
        const int Ay = 59; // BlockBot - 3 — bottom of A/B sections

        // Drawing order from RedBlockSure (FRAMEADV.S):
        // 1. C (ORA = transparent)
        BlitAnchored(canvas, cw, ch, GetBgSprite(PieceC[tileId]), 0, Dy, true);

        // 2. B (ORA = transparent) with Y offset
        int byOff = tileId < PieceBY.Length ? PieceBY[tileId] : 0;
        BlitAnchored(canvas, cw, ch, GetBgSprite(PieceB[tileId]), 0, Ay + byOff, true);

        // 3. D (STA = overwrite)
        BlitAnchored(canvas, cw, ch, GetBgSprite(PieceD[tileId]), 0, Dy, false);

        // 4. BStripe (ORA = transparent)
        BlitAnchored(canvas, cw, ch, GetBgSprite(BStripe[tileId]), 0, Ay, true);

        // 5. A (ORA = transparent) with Y offset
        int ayOff = tileId < PieceAY.Length ? PieceAY[tileId] : 0;
        BlitAnchored(canvas, cw, ch, GetBgSprite(PieceA[tileId]), 0, Ay + ayOff, true);

        // Build texture from canvas
        var img = Raylib.GenImageColor(cw, ch, new Color(0, 0, 0, 255));
        for (int y = 0; y < ch; y++)
            for (int x = 0; x < cw; x++)
            {
                var c = canvas[y * cw + x];
                Raylib.ImageDrawPixel(ref img, x, y, c);
            }
        _tileTextures[tileId] = Raylib.LoadTextureFromImage(img);
        if (_tileTextures[tileId].Id != 0)
            Raylib.SetTextureFilter(_tileTextures[tileId], TextureFilter.Point);
        Raylib.UnloadImage(img);

        // Build foreground texture if present
        byte fi = FrontI[tileId];
        if (fi != 0)
        {
            var frontSprite = GetBgSprite(fi);
            if (frontSprite.Pixels != null)
            {
                // Compute foreground canvas position anchored at Ay
                int fy = FrontY[tileId];
                _frontCanvasX[tileId] = FrontX[tileId] * 7;
                _frontCanvasY[tileId] = Ay + fy - frontSprite.Height + 1;
                _frontTextures[tileId] = Apple2Image.ToTexture(frontSprite, transparentBlack: true);
            }
        }
    }

    /// <summary>
    /// Blit a sprite onto the canvas anchored by its bottom edge at bottomY.
    /// </summary>
    private static void BlitAnchored(Color[] canvas, int canvasW, int canvasH,
        Apple2Image.DecodedSprite sprite, int dx, int bottomY, bool transparent)
    {
        if (sprite.Pixels == null || sprite.WidthPx == 0 || sprite.Height == 0)
            return;

        int topY = bottomY - sprite.Height + 1;

        for (int y = 0; y < sprite.Height; y++)
        {
            int dstY = topY + y;
            if (dstY < 0 || dstY >= canvasH) continue;

            for (int x = 0; x < sprite.WidthPx; x++)
            {
                int dstX = dx + x;
                if (dstX < 0 || dstX >= canvasW) continue;

                var c = sprite.Pixels[y * sprite.WidthPx + x];
                if (transparent && c.R == 0 && c.G == 0 && c.B == 0)
                    continue;
                canvas[dstY * canvasW + dstX] = c;
            }
        }
    }

    /// <summary>
    /// Get the pre-built texture for a tile ID. Returns default if not loaded.
    /// </summary>
    public static Texture2D GetTileTexture(int tileId)
    {
        if (!_loaded || tileId < 0 || tileId >= 30)
            return default;
        return _tileTextures[tileId];
    }

    /// <summary>
    /// Get the foreground overlay texture for a tile ID (e.g., posts, gate bars).
    /// </summary>
    public static Texture2D GetFrontTexture(int tileId)
    {
        if (!_loaded || tileId < 0 || tileId >= 30)
            return default;
        return _frontTextures[tileId];
    }

    /// <summary>
    /// Get foreground draw offsets for a tile.
    /// </summary>
    public static (int xPx, int y) GetFrontOffset(int tileId)
    {
        if (tileId < 0 || tileId >= 30) return (0, 0);
        return (_frontCanvasX[tileId], _frontCanvasY[tileId]);
    }

    public static void Unload()
    {
        if (!_loaded) return;
        for (int i = 0; i < 30; i++)
        {
            if (_tileTextures[i].Id != 0) Raylib.UnloadTexture(_tileTextures[i]);
            if (_frontTextures[i].Id != 0) Raylib.UnloadTexture(_frontTextures[i]);
        }
        _tileTextures = new Texture2D[30];
        _frontTextures = new Texture2D[30];
        _loaded = false;
    }
}
