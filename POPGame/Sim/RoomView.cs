using POPGame.Data;

namespace POPGame.Sim;

/// <summary>
/// The level as the simulation sees it, plus the tile classifications shared by the
/// kid engine and the renderer. Tile lookups that cross room edges live in
/// <see cref="KidEngine"/> (get_tile), because the original's collision code depends on
/// the side effects of each lookup.
/// </summary>
public sealed class RoomView
{
    public RoomView(Level level) => Level = level;

    public Level Level { get; }

    /// <summary>
    /// Whether a tile presents a walkable surface. Almost everything in POP does; this
    /// is CMPSPACE (CTRLSUBS.S:1495) inverted, and its exclusion list is exactly space,
    /// pillartop, panelwof, block, and archtop1 upwards (SDLPoP tile_is_floor).
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

    /// <summary>
    /// A gate's BLUESPEC is its height, 0 (shut) to 47 (fully raised): the original's
    /// modifier (0..188) divided by 4.
    /// </summary>
    public const int GateOpen = 47;

    /// <summary>BLUESPEC value at which a slicer's blade is out (MOVEDATA.S).</summary>
    public const int SlicerExtended = 2;
}
