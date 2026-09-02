using POPGame.Data;

namespace POPGame.Sim;

/// <summary>
/// Tile queries in room-relative terms, with columns outside 0..9 resolved through
/// the level's MAP neighbour links so collision code can just ask about "the block
/// to my left" without caring about room boundaries.
/// </summary>
public sealed class RoomView
{
    private readonly Level _level;

    public RoomView(Level level) => _level = level;

    public Level Level => _level;

    /// <summary>
    /// Resolves (room, row, col) that may sit outside the room into the neighbouring
    /// room. Returns false when there is no neighbour, i.e. the edge of the world.
    /// </summary>
    public bool Resolve(int room, int row, int col, out int outRoom, out int outRow, out int outCol)
    {
        outRoom = room; outRow = row; outCol = col;

        while (outCol < 0)
        {
            int n = _level.Left(outRoom);
            if (n == 0) return false;
            outRoom = n; outCol += Coord.Cols;
        }
        while (outCol >= Coord.Cols)
        {
            int n = _level.Right(outRoom);
            if (n == 0) return false;
            outRoom = n; outCol -= Coord.Cols;
        }
        while (outRow < 0)
        {
            int n = _level.Above(outRoom);
            if (n == 0) return false;
            outRoom = n; outRow += Coord.Rows;
        }
        while (outRow >= Coord.Rows)
        {
            int n = _level.Below(outRoom);
            if (n == 0) return false;
            outRoom = n; outRow -= Coord.Rows;
        }
        return outRoom >= 1 && outRoom <= Level.NumScreens;
    }

    /// <summary>Tile at a possibly out-of-room cell; Space when there is no neighbour.</summary>
    public TileId Tile(int room, int row, int col)
        => Resolve(room, row, col, out int r, out int rr, out int cc)
            ? _level.GetTileId(r - 1, rr, cc)
            : TileId.Space;

    public byte Spec(int room, int row, int col)
        => Resolve(room, row, col, out int r, out int rr, out int cc)
            ? _level.GetSpec(r - 1, rr, cc)
            : (byte)0;

    /// <summary>
    /// Whether a tile presents a walkable surface. Almost everything in POP does; this
    /// is CMPSPACE (CTRLSUBS.S:1495) inverted, and its exclusion list is exactly space,
    /// pillartop, panelwof, block, and archtop1 upwards.
    ///
    /// <b>A solid block has no floor</b> — CMPSPACE says so in as many words. It is a
    /// wall, not a ledge: it blocks movement through its cell *and* you cannot stand on
    /// it, so a character that ends up in one has nothing under its feet. Leaving Block
    /// out of this list lets characters stand on top of walls, which then lets them
    /// climb to places the original never allows.
    /// </summary>
    public static bool HasFloor(TileId t) => t switch
    {
        TileId.Space or TileId.PillarTop or TileId.PanelWOF or TileId.Block
            or TileId.ArchTop1 or TileId.ArchTop2 or TileId.ArchTop3 or TileId.ArchTop4 => false,
        _ => true,
    };

    /// <summary>A gate's BLUESPEC is its height, 0 (shut) to 47 (fully raised).</summary>
    public const int GateOpen = 47;
    /// <summary>Raised at least this far and the kid can duck through.</summary>
    public const int GatePassable = 32;

    /// <summary>
    /// Whether a tile blocks horizontal movement through its cell.
    ///
    /// The set is CMPBARR (CTRLSUBS.S:1532): panels and gates, mirror and slicer, and
    /// solid block. Everything else is clear — a flask in particular is explicitly
    /// "not really a barrier" (COLL.S:529). A gate only bars you while it is low, and
    /// a slicer only while its blade is out.
    /// </summary>
    public bool IsBarrier(int room, int row, int col) => Tile(room, row, col) switch
    {
        TileId.Block or TileId.PanelWF or TileId.PanelWOF or TileId.Mirror => true,
        TileId.Gate => Spec(room, row, col) < GatePassable,
        TileId.Slicer => Spec(room, row, col) == SlicerExtended,
        _ => false,
    };

    /// <summary>BLUESPEC value at which a slicer's blade is out (MOVEDATA.S).</summary>
    public const int SlicerExtended = 2;

    public bool FloorAt(int room, int row, int col) => HasFloor(Tile(room, row, col));

    /// <summary>True when the cell is empty enough for a character to occupy it.</summary>
    public bool IsOpen(int room, int row, int col) => !IsBarrier(room, row, col);
}
