using POPGame.Data;

namespace POPGame.Rendering;

/// <summary>
/// Which DOS dungeon images make up each tile, and where they sit inside its cell.
///
/// Pieces are addressed by their raw VDUNGEON.DAT resource id and placed relative to
/// the cell's top-left corner (cellX = 32*col, cellY = <see cref="Sim.Coord.BlockTop"/>).
/// Every offset here was read back off the real game: the DOSBox frame was captured at
/// native 320x200 and each decoded resource was template-matched against it, so a hit
/// is an exact pixel-for-pixel placement rather than a guess.
///
/// The layout that came out of that:
///
///   * a cell is 32x63; a solid wall is one 32x60 body at +1 capped by a 3px
///     floorpiece at +61, which lands exactly on the next row's cell top
///   * a *floor* has no body at all — only the perspective top face at +48 and the
///     same 3px cap at +61, so the empty part of the cell shows the black backdrop
///   * that top face is flat for a continuing run and a wedge at the run's left end,
///     which is what gives POP its slab-seen-from-slightly-above look
///   * wall-mounted decoration (the torch) is drawn one cell to the RIGHT of the tile
///     that owns it — an original quirk, confirmed on both torches of level 1 room 1
/// </summary>
public static class DosTileArt
{
    /// <summary>Which decoded bank a piece comes from.</summary>
    public enum Bank { Env, Flame }

    /// <param name="Image">Raw resource id in the piece's bank.</param>
    /// <param name="SrcY">First source row, for pieces cut out of a larger image.</param>
    /// <param name="SrcRows">Source row count, or -1 for the rest of the image.</param>
    /// <param name="Frames">Length of the animation loop starting at Image; 1 is static.</param>
    public readonly record struct Piece(int Image, int X, int Y, Bank From = Bank.Env,
                                        int SrcY = 0, int SrcRows = -1, int Frames = 1);

    // ---- the environment resources this file places -------------------------------

    private const int Cap = 243;        // 32x3 floorpiece
    private const int BlockCap = 369;   // 32x3 floorpiece on a wall (palette group 360)
    private const int Wedge = 348;      // 32x14 top face, sloping up to the right
    private const int Rail = 237;       // 9px rail over a 32x13 slab; only the slab is used
    private const int GateRow = 252;    // 24x8 lattice course, the repeating unit
    private const int GateEnd = 251;    // 16x10 weighted bottom of the lattice
    private const int GateStub = 260;   // 260-h is the h-pixel partial course (h = 1..7)
    private const int GateSill = 362;   // 25x12 threshold the portcullis comes down onto
    private const int GateX = 7;        // the lattice hangs inset from the cell's left edge
    private const int PostsBody = 292;  // 32x60
    private const int PostsFace = 295;  // 19x60
    private const int PostsSide = 293;  // 26x62, the shaded flank on the next cell
    private const int Rubble = 300;     // 32x9
    private const int Sconce = 346;     // 32x15 torch bracket
    private const int Bones = 1230;     // 32x19
    private const int Stairs = 344;     // 41x45, the way out of the level
    private const int PillarTop = 1088;
    private const int PillarBot = 1086;
    private const int Decor = 244;      // the faint cross scratched into the back wall

    /// <summary>
    /// How far the spikes are out, shortest first: the blades grow out of the floor as
    /// the tile's BLUESPEC counts up. Drawn sitting on the walking surface, so each is
    /// placed by its own height.
    /// </summary>
    private static readonly int[] SpikeFrames = [1305, 1304, 1303, 1301];
    private static readonly int[] SpikeHeights = [17, 21, 30, 60];

    /// <summary>Potion, from the PRINCE.DAT fire bank — the dungeon palette has no glass.</summary>
    private const int Flask = 12;       // 150-bank index; 12..15 is the bubbling loop
    private const int SwordPickup = 11; // 150-bank index

    /// <summary>The plain masonry body, used wherever the tile's modifier is 0.</summary>
    private const int PlainBody = 364;

    /// <summary>
    /// The bodies a modifier-1 wall picks from. Solving every wall cell of rooms 1 and 2
    /// against the captured frames showed the rule: a modifier of 0 is *always* 364,
    /// while a non-zero one draws 364, 366 or 370 (368 never turned up). Which of the
    /// three is the original's own randomness and is not reproduced here.
    /// </summary>
    private static readonly int[] BodyVariants = [364, 370, 366, 370];

    /// <summary>The two vertical brick seams, 9px and 8px wide.</summary>
    private static readonly int[] SeamVariants = [371, 372];

    /// <summary>
    /// A cell is three 21px courses, but only the lower two carry a vertical break, and
    /// each falls in a narrow band: the middle course 8..12 px into the cell, the bottom
    /// one 0..4 px in. Every wall cell solved off the captures obeys this.
    ///
    /// The break is not per-cell noise. Solving a whole wall row of room 3 gives, left to
    /// right, 4 4 3 3 2 1 0 0 0 — the seams sit on one continuous grid a shade under 32px
    /// apart, so their offset inside the cell walks down about half a pixel per column and
    /// wraps. <see cref="SeamX"/> reproduces that drift, which is what makes a long wall
    /// read as one running bond instead of a row of identical stamps.
    /// </summary>
    private static readonly (int Y, int MinX, int Span)[] Seams =
        [(BodyY + 21, 8, 5), (BodyY + 42, 0, 5)];

    private static int SeamX(int course, int col, int phase)
    {
        var (_, minX, span) = Seams[course];
        return minX + span - 1 - ((col / 2 + phase) % span);
    }

    /// <summary>Rows 47..59 of the rail image: the flat 32x13 slab a floor run is made of.</summary>
    private static readonly Piece Slab = new(Rail, 0, FaceY, SrcY: 47, SrcRows: 13);
    private static readonly Piece LeftEnd = new(Wedge, 0, FaceY);

    // Offsets within the cell, all measured off the captured frame.
    private const int FaceY = 48;       // top of the perspective floor face
    private const int CapY = 61;        // top of the 3px floorpiece
    private const int BodyY = 1;        // top of a 32x60 masonry body
    private const int SconceX = 32, SconceY = 21;
    private const int FlameX = 40, FlameY = 3;
    private const int DecorY = 14;      // the back-wall cross, in the open part of a cell

    /// <summary>Tiles with no walkable surface, which therefore draw no floor at all.</summary>
    public static bool IsOpen(TileId t) => t is TileId.Space or TileId.PillarTop
        or TileId.PanelWOF or TileId.ArchTop1 or TileId.ArchTop2
        or TileId.ArchTop3 or TileId.ArchTop4;

    /// <summary>A tile whose mass reaches the left edge of its cell, so the run continues.</summary>
    private static bool ContinuesFloor(TileId t) => !IsOpen(t);

    /// <summary>
    /// Layers drawn behind the characters. <paramref name="left"/> is the tile in the
    /// cell to the left, which decides whether the floor face is a flat slab or the
    /// wedge that ends a run. <paramref name="rnd"/> is per-cell noise, used only where
    /// the original itself is random; <paramref name="seamPhase"/> is shared by a whole
    /// wall row so its brick courses line up into one bond.
    /// </summary>
    public static List<Piece> Back(TileId tile, TileId left, int rnd, int spec = 0, int mod = 0,
                                   int col = 0, int seamPhase = 0)
    {
        var pieces = new List<Piece>(8);

        switch (tile)
        {
            case TileId.Space:
            case TileId.PillarTop:
            case TileId.PanelWOF:
            case TileId.ArchTop1:
            case TileId.ArchTop2:
            case TileId.ArchTop3:
            case TileId.ArchTop4:
                return pieces;

            case TileId.Block:
                pieces.Add(new(mod == 0 ? PlainBody : BodyVariants[rnd & 3], 0, BodyY));
                // The body already carries the top course whole; the two below it get a
                // vertical break so the courses do not line up into a grid.
                for (int c = 0; c < Seams.Length; c++)
                    pieces.Add(new(SeamVariants[(col + c) & 1],
                                   SeamX(c, col, seamPhase + c * 2), Seams[c].Y));
                pieces.Add(new(BlockCap, 0, CapY));
                return pieces;

            case TileId.Posts:
                pieces.Add(new(PostsBody, 0, BodyY));
                pieces.Add(new(PostsFace, 8, BodyY));
                pieces.Add(new(PostsSide, 32, BodyY));
                pieces.Add(new(Cap, 0, CapY));
                return pieces;

            case TileId.Gate:
                AddGate(pieces, spec);
                return pieces;
        }

        // Everything else stands on a floor: the perspective top face plus its cap.
        // The rest of the cell is open, so the back wall shows through — and now and
        // then it carries the faint cross that is scratched into it.
        // (never on a torch tile — the bracket already hangs there)
        if (rnd % 10 == 0 && tile != TileId.Torch) pieces.Add(new(Decor, 0, DecorY));
        pieces.Add(ContinuesFloor(left) ? Slab : LeftEnd);
        pieces.Add(new(Cap, 0, CapY));

        // Anything standing on that floor. These placements put the piece's foot on the
        // walking surface; unlike the masonry above they have not been checked against a
        // capture of the original yet, because none of them occur in a room reachable
        // from the start of level 1.
        switch (tile)
        {
            case TileId.Rubble: pieces.Add(new(Rubble, 0, 52)); break;
            case TileId.Torch: pieces.Add(new(Sconce, SconceX, SconceY)); break;
            case TileId.Bones: pieces.Add(new(Bones, 0, CapY - 19)); break;
            case TileId.PillarBot: pieces.Add(new(PillarBot, 0, BodyY)); break;

            case TileId.Spikes:
                // Retracted spikes show nothing but the floor.
                int frame = Math.Clamp(spec, 0, SpikeFrames.Length);
                if (frame > 0)
                    pieces.Add(new(SpikeFrames[frame - 1], 0, CapY - SpikeHeights[frame - 1]));
                break;

            case TileId.Flask:
                pieces.Add(new(Flask, 11, CapY - 15, Bank.Flame, Frames: 4));
                break;

            case TileId.Sword:
                pieces.Add(new(SwordPickup, 4, CapY - 9, Bank.Flame));
                break;

            // The stairs out are one 41x45 image spanning the exit pair, drawn from the
            // left half; the right half (Exit2) adds nothing of its own.
            case TileId.Exit: pieces.Add(new(Stairs, 0, CapY - 45)); break;
        }

        return pieces;
    }

    /// <summary>
    /// A portcullis: the fixed rail-and-threshold image, then the lattice hung from the
    /// top of the doorway down to wherever the gate currently is.
    ///
    /// The lattice is built the way the original builds it — a stack of 8px courses
    /// (<see cref="GateRow"/>) with a short course of the leftover height at the top and
    /// the weighted end piece below the last one. <paramref name="spec"/> is the tile's
    /// BLUESPEC, 0 for shut and <see cref="Sim.RoomView.GateOpen"/> for fully raised, so
    /// the drop is <c>GateOpen + 1 - spec</c>. Checked against the shut gate on the edge
    /// of room 1: 47px of drop == a 7px course at +1, five full courses, end piece at +48.
    /// </summary>
    private static void AddGate(List<Piece> pieces, int spec)
    {
        // Its own narrow threshold, not the usual full-width floor face — the doorway
        // is recessed, so the slab starts where the lattice does.
        pieces.Add(new(GateSill, GateX, 52));

        int drop = Math.Clamp(Sim.RoomView.GateOpen + 1 - spec, 0, Sim.RoomView.GateOpen + 1);
        int y = BodyY;

        if (drop % 8 is int stub and > 0)
        {
            pieces.Add(new(GateStub - stub, GateX, y));
            y += stub;
        }
        for (int i = 0; i < drop / 8; i++, y += 8)
            pieces.Add(new(GateRow, GateX, y));

        if (drop > 0) pieces.Add(new(GateEnd, GateX, y));
    }

    /// <summary>Layers drawn in front of the characters (pillar tops, torch flames).</summary>
    public static List<Piece> Front(TileId tile) => tile switch
    {
        TileId.PillarTop => [new(PillarTop, 4, BodyY)],
        // Flame frame 1 of nine; DosRenderer cycles it and gives each tile its own phase.
        TileId.Torch => [new(1, FlameX, FlameY, Bank.Flame, Frames: 9)],
        _ => [],
    };

    /// <summary>The floorpiece the room's ceiling is capped with, drawn across every column.</summary>
    public static Piece Ceiling => new(Cap, 0, CapY);
}
