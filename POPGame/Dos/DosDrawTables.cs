namespace POPGame.Dos;

/// <summary>
/// One row of the original's per-tile drawing table: which environment images make
/// up a tile and where they sit. Ids are chtab 6 (VDUNGEON.DAT resource 200+id);
/// y offsets are relative to the cell's <c>main_y</c>, x offsets are in 8-pixel units.
/// </summary>
/// <param name="BaseId">The tile's own left part, drawn in its cell.</param>
/// <param name="RightId">The tile's right part, drawn in the NEXT cell to the right.</param>
/// <param name="TopRightId">Drawn one row up and one column right, over the top of the next cell.</param>
/// <param name="BottomId">The 3px floorpiece under the tile.</param>
/// <param name="ForeId">Drawn in front of the characters.</param>
public readonly record struct TilePiece(
    byte BaseId, byte FloorLeft, sbyte BaseY,
    byte RightId, byte FloorRight, sbyte RightY,
    byte StripeId, byte TopRightId, byte BottomId,
    byte ForeId, byte ForeX, sbyte ForeY);

/// <summary>
/// The room-drawing tables of the DOS game, read out of PRINCE.EXE.
///
/// They live in one contiguous block of the data segment, starting with the 31-row
/// tile table and followed by the small animation-frame arrays, so a single anchor
/// locates them all. The block holds a run of ten zeros, which EXEPACK stores as a
/// fill command — hence reading from the unpacked image, not the file.
///
/// The layout and meaning match SDLPoP's reconstruction of the original (seg008.c),
/// which is what the drawing code in <see cref="Rendering.DosRoomDrawer"/> follows.
/// </summary>
public sealed class DosDrawTables
{
    public const int TileCount = 31;

    public TilePiece[] Tiles { get; } = new TilePiece[TileCount];

    public byte[] LooseLeft { get; }
    public byte[] LooseRight { get; }
    public byte[] LooseBottom { get; }
    public byte[] ChomperFrame { get; }
    public byte[] ChomperTop { get; }
    public byte[] ChomperBottom { get; }
    public byte[] ChomperY { get; }
    public byte[] ChomperFore { get; }
    public byte[] SpikesLeft { get; }
    public byte[] SpikesRight { get; }
    public byte[] SpikesFore { get; }
    public byte[] DoorTop { get; }
    /// <summary>The partial top course of a gate. Shares its first byte with <see cref="DoorTop"/>'s last.</summary>
    public byte[] DoorSlice { get; }
    public byte[] BlueLine1 { get; }
    public sbyte[] BlueLineY { get; }
    public byte[] BlueLine3 { get; }
    public byte[] WallBottom { get; }
    public byte[] WallMain { get; }
    public byte[] PotionBubble { get; }
    /// <summary>Y offsets of the left and right wall marks, as words in the EXE.</summary>
    public int[] LeftMarkY { get; }
    public int[] RightMarkY { get; }

    // Offsets from the start of the tile table, in the unpacked image.
    private const int LooseLeftAt = 372, LooseRightAt = 384, LooseBottomAt = 396;
    private const int ChomperFrameAt = 421, ChomperTopAt = 429, ChomperBottomAt = 435;
    private const int ChomperYAt = 441, ChomperForeAt = 446;
    private const int SpikesAt = 452;
    private const int DoorTopAt = 482, DoorSliceAt = 489;
    private const int BlueLine1At = 506, BlueLineYAt = 510, BlueLine3At = 514;
    private const int WallBottomAt = 518, WallMainAt = 522, PotionBubbleAt = 526;
    private const int LeftMarkAt = 592, RightMarkAt = 602;

    /// <summary>An empty row followed by the plain floor's row: the start of the table.</summary>
    private static readonly byte[] Anchor =
        [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 41, 1, 0, 42, 1, 2, 145, 0, 43, 0, 0, 0];

    public DosDrawTables(byte[] image)
    {
        int t = image.AsSpan().IndexOf(Anchor);
        if (t < 0) throw new InvalidDataException("tile drawing table not found in PRINCE.EXE");

        for (int i = 0; i < TileCount; i++)
        {
            var r = image.AsSpan(t + i * 12, 12);
            Tiles[i] = new TilePiece(r[0], r[1], (sbyte)r[2], r[3], r[4], (sbyte)r[5],
                                     r[6], r[7], r[8], r[9], r[10], (sbyte)r[11]);
        }

        byte[] Bytes(int at, int n) => image.AsSpan(t + at, n).ToArray();
        int[] Words(int at, int n) =>
            Enumerable.Range(0, n).Select(i => image[t + at + i * 2] | image[t + at + i * 2 + 1] << 8).ToArray();

        LooseLeft = Bytes(LooseLeftAt, 12);
        LooseRight = Bytes(LooseRightAt, 12);
        LooseBottom = Bytes(LooseBottomAt, 12);
        ChomperFrame = Bytes(ChomperFrameAt, 8);
        ChomperTop = Bytes(ChomperTopAt, 6);
        ChomperBottom = Bytes(ChomperBottomAt, 6);
        ChomperY = Bytes(ChomperYAt, 5);
        ChomperFore = Bytes(ChomperForeAt, 6);
        SpikesLeft = Bytes(SpikesAt, 10);
        SpikesRight = Bytes(SpikesAt + 10, 10);
        SpikesFore = Bytes(SpikesAt + 20, 10);
        DoorTop = Bytes(DoorTopAt, 8);
        DoorSlice = Bytes(DoorSliceAt, 9);
        BlueLine1 = Bytes(BlueLine1At, 4);
        BlueLineY = Bytes(BlueLineYAt, 4).Select(b => (sbyte)b).ToArray();
        BlueLine3 = Bytes(BlueLine3At, 4);
        WallBottom = Bytes(WallBottomAt, 4);
        WallMain = Bytes(WallMainAt, 4);
        PotionBubble = Bytes(PotionBubbleAt, 8);
        LeftMarkY = Words(LeftMarkAt, 5);
        RightMarkY = Words(RightMarkAt, 4);

        if (Validate() is { } err)
            throw new InvalidDataException("PRINCE.EXE drawing tables: " + err);
    }

    public static DosDrawTables Load() =>
        new(ExePack.Unpack(File.ReadAllBytes(DosGame.File("PRINCE.EXE"))));

    /// <summary>
    /// Cross-checks a second anchor in each array, so a different build of the game
    /// fails loudly rather than drawing garbage.
    /// </summary>
    private string? Validate()
    {
        var wall = Tiles[20];
        if (wall.RightId != 1 || wall.TopRightId != 2) return $"tile 20 (wall) is {wall}";
        var gate = Tiles[4];
        if (gate.BaseId != 46 || gate.RightId != 47 || gate.ForeId != 49) return $"tile 4 (gate) is {gate}";
        if (LooseLeft[1] != 69 || LooseRight[1] != 71 || LooseBottom[1] != 73) return "loose floor frames";
        if (SpikesLeft[1] != 128 || SpikesRight[1] != 134 || SpikesFore[1] != 139) return "spike frames";
        if (DoorTop[0] != 60 || DoorSlice[0] != 67 || DoorSlice[8] != 52) return "gate frames";
        if (WallMain[3] != 4 || WallBottom[3] != 3) return "wall frames";
        if (BlueLine3[0] != 44 || BlueLineY[1] != -20) return "back-wall decoration frames";
        if (LeftMarkY[0] != 58 || RightMarkY[0] != 52) return "wall mark positions";
        return null;
    }
}
