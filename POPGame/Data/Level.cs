namespace POPGame.Data;

/// <summary>
/// Loads and holds Prince of Persia Apple II binary level data.
/// LiveBlueType/LiveBlueSpec are mutable runtime copies for hazard state changes.
/// </summary>
public class Level
{
    public const int NumScreens     = 24;
    public const int CellsPerScreen = 30;  // 3 rows × 10 cols
    public const int Rows           = 3;
    public const int Cols           = 10;

    // --- Original (read-only reference) ---
    public byte[] BlueType = new byte[NumScreens * CellsPerScreen];
    public byte[] BlueSpec = new byte[NumScreens * CellsPerScreen];

    // --- Mutable runtime state (modified by hazard system) ---
    public byte[] LiveBlueType = new byte[NumScreens * CellsPerScreen];
    public byte[] LiveBlueSpec = new byte[NumScreens * CellsPerScreen];

    public byte[] LinkLoc  = new byte[256];
    public byte[] LinkMap  = new byte[256];

    // MAP[screen0, dir]: dir 0=left 1=right 2=above 3=below; 1-indexed; 0=none
    public byte[,] Map     = new byte[NumScreens, 4];
    public byte[]  InfoRaw = new byte[256];

    // Parsed INFO fields
    public int InfoScreenCount;
    public int KidStartScrn;   // 1-indexed
    public int KidStartBlock;  // 0-29
    public int KidStartFace;   // 0xFF = facing left

    public int SwordStartScrn;
    public int SwordStartBlock;

    public byte[] GdStartBlock = new byte[NumScreens];
    public byte[] GdStartFace  = new byte[NumScreens];
    public byte[] GdStartX     = new byte[NumScreens];
    public byte[] GdStartSeqL  = new byte[NumScreens];
    public byte[] GdStartProg  = new byte[NumScreens];
    public byte[] GdStartSeqH  = new byte[NumScreens];

    // --- Cell accessors (0-indexed screen) ---
    public TileId GetTileId(int screen0, int row, int col)
        => (TileId)(LiveBlueType[screen0 * CellsPerScreen + row * Cols + col] & 0x1F);

    public byte GetSpec(int screen0, int row, int col)
        => LiveBlueSpec[screen0 * CellsPerScreen + row * Cols + col];

    public void SetSpec(int screen0, int row, int col, byte value)
        => LiveBlueSpec[screen0 * CellsPerScreen + row * Cols + col] = value;

    public void SetTileType(int screen0, int row, int col, TileId id)
    {
        int idx = screen0 * CellsPerScreen + row * Cols + col;
        LiveBlueType[idx] = (byte)((LiveBlueType[idx] & 0xE0) | ((byte)id & 0x1F));
    }

    // Flat-index variants used by link system
    public TileId GetTileIdFlat(int screen0, int cellIdx)
        => (TileId)(LiveBlueType[screen0 * CellsPerScreen + cellIdx] & 0x1F);

    public byte GetSpecFlat(int screen0, int cellIdx)
        => LiveBlueSpec[screen0 * CellsPerScreen + cellIdx];

    public void SetSpecFlat(int screen0, int cellIdx, byte value)
        => LiveBlueSpec[screen0 * CellsPerScreen + cellIdx] = value;

    // Neighbours (1-indexed input/output; 0 = none)
    public int Left (int scrn1) => Valid(scrn1) ? Map[scrn1 - 1, 0] : 0;
    public int Right(int scrn1) => Valid(scrn1) ? Map[scrn1 - 1, 1] : 0;
    public int Above(int scrn1) => Valid(scrn1) ? Map[scrn1 - 1, 2] : 0;
    public int Below(int scrn1) => Valid(scrn1) ? Map[scrn1 - 1, 3] : 0;
    private static bool Valid(int s) => s >= 1 && s <= NumScreens;

    // -------------------------------------------------------------------------
    // Loader
    // -------------------------------------------------------------------------
    public static Level Load(string path) => Load(File.ReadAllBytes(path));

    /// <summary>
    /// Parses a 2304-byte level blob. Both the Apple II LEVELn files and the
    /// resources inside the DOS LEVELS.DAT use this exact layout.
    /// </summary>
    public static Level Load(byte[] data)
    {
        if (data.Length < 2304)
            throw new IOException($"Level data too short: {data.Length} bytes (need 2304)");

        var lv = new Level();
        Buffer.BlockCopy(data,    0, lv.BlueType, 0, 720);
        Buffer.BlockCopy(data,  720, lv.BlueSpec, 0, 720);
        Buffer.BlockCopy(data, 1440, lv.LinkLoc,  0, 256);
        Buffer.BlockCopy(data, 1696, lv.LinkMap,  0, 256);

        // MAP at offset 0x7A0 = 1952
        for (int s = 0; s < NumScreens; s++)
        {
            int off = 1952 + s * 4;
            lv.Map[s, 0] = data[off];
            lv.Map[s, 1] = data[off + 1];
            lv.Map[s, 2] = data[off + 2];
            lv.Map[s, 3] = data[off + 3];
        }

        // INFO at offset 0x800 = 2048
        Buffer.BlockCopy(data, 2048, lv.InfoRaw, 0, 256);
        lv.InfoScreenCount = lv.InfoRaw[0];
        lv.KidStartScrn    = lv.InfoRaw[64];
        lv.KidStartBlock   = lv.InfoRaw[65];
        lv.KidStartFace    = lv.InfoRaw[66];
        lv.SwordStartScrn  = lv.InfoRaw[68];
        lv.SwordStartBlock = lv.InfoRaw[69];

        const int gdBase = 71;
        for (int i = 0; i < NumScreens; i++)
        {
            lv.GdStartBlock[i] = lv.InfoRaw[gdBase +   0 + i];
            lv.GdStartFace [i] = lv.InfoRaw[gdBase +  24 + i];
            lv.GdStartX    [i] = lv.InfoRaw[gdBase +  48 + i];
            lv.GdStartSeqL [i] = lv.InfoRaw[gdBase +  72 + i];
            lv.GdStartProg [i] = lv.InfoRaw[gdBase +  96 + i];
            lv.GdStartSeqH [i] = lv.InfoRaw[gdBase + 120 + i];
        }

        // Initialise live copies from original data
        Buffer.BlockCopy(lv.BlueType, 0, lv.LiveBlueType, 0, lv.BlueType.Length);
        Buffer.BlockCopy(lv.BlueSpec, 0, lv.LiveBlueSpec, 0, lv.BlueSpec.Length);

        return lv;
    }

    /// <summary>Reset live state back to original loaded values (on level restart).</summary>
    public void Reset()
    {
        Buffer.BlockCopy(BlueType, 0, LiveBlueType, 0, BlueType.Length);
        Buffer.BlockCopy(BlueSpec, 0, LiveBlueSpec, 0, BlueSpec.Length);
    }
}
