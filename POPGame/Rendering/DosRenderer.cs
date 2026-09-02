using POPGame.Data;
using POPGame.Dos;
using POPGame.Sim;

namespace POPGame.Rendering;

/// <summary>
/// Draws a room at the DOS native 320x200 into a <see cref="Framebuffer"/>, using
/// the graphics out of VDUNGEON.DAT, PRINCE.DAT and KID.DAT.
///
/// The world uses the original units (14 x-units per block, 63 pixels per row), so
/// horizontal positions scale by 32/14 on the way to DOS pixels while vertical
/// positions pass through unchanged.
///
/// Background pieces are addressed by raw resource id and placed by
/// <see cref="DosTileArt"/>; see that file for where the offsets came from.
/// </summary>
public sealed class DosRenderer
{
    public const int ScreenW = 320;
    public const int ScreenH = 200;
    public const int TileW = 32;
    public const int TileH = Coord.BlockHeight;   // 63

    private readonly DosImageBank _kid;
    private readonly DosImageBank _env;
    private readonly DosImageBank _flame;

    /// <summary>How far off the left edge the neighbouring room's last column sits.</summary>
    private const int SliverInset = 7;

    /// <summary>
    /// Background art is masked: besides index 0, the dungeon bank uses palette entries
    /// 14 and 15 as stencil markers. They are the only saturated colours in a palette
    /// that is otherwise all blue-greys, and drawing them paints bright green bars over
    /// the gate rail and the exit stairs.
    /// </summary>
    private const int EnvSkipMask = 1 | 1 << 14 | 1 << 15;

    private int _tick;

    /// <summary>Stable per-cell noise, so the same wall is laid the same way every frame.</summary>
    private static int Hash(int room, int row, int col)
    {
        int h = room * 7919 + row * 104729 + col * 1299709;
        h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
        return h & 0x3FFFFFF;
    }

    public Framebuffer Frame { get; } = new(ScreenW, ScreenH);

    public DosRenderer()
    {
        _kid = new DosImageBank(DosGame.File("KID.DAT"), 400);
        _env = new DosImageBank(DosGame.File("VDUNGEON.DAT"), 200, 360);
        _flame = new DosImageBank(DosGame.File("PRINCE.DAT"), 150);
    }

    /// <summary>World x units -> screen pixels.</summary>
    public static int ToPx(int x) => (x - Coord.ScrnLeft) * TileW / Coord.BlockWidth;

    public void Draw(Simulation sim)
    {
        _tick++;
        Frame.Clear(0, 0, 0);
        DrawLayer(sim, sim.Kid.Room, front: false);
        DrawKid(sim);
        DrawLayer(sim, sim.Kid.Room, front: true);
    }

    private void DrawLayer(Simulation sim, int room, bool front)
    {
        if (room < 1 || room > Level.NumScreens) return;

        // The room is capped by a floorpiece above row 0, the underside of the
        // ceiling. Its cell top is one row above the room, so it lands at y = 1.
        if (!front)
        {
            var ceiling = DosTileArt.Ceiling;
            for (int col = 0; col < Coord.Cols; col++)
                BlitPiece(ceiling, col * TileW, Coord.BlockTop(-1));
        }

        // The room to the left shows through in the leftmost 25 pixels: the original
        // draws that neighbour's last column, clipped, so a wall run reads as
        // continuing past the edge of the screen instead of stopping dead at x=0.
        int leftRoom = sim.Level.Left(room);

        for (int row = 0; row < Coord.Rows; row++)
            for (int col = -1; col < Coord.Cols; col++)
            {
                int srcRoom = col < 0 ? leftRoom : room;
                int srcCol = col < 0 ? Coord.Cols - 1 : col;
                if (srcRoom < 1 || srcRoom > Level.NumScreens) continue;

                var tile = sim.Level.GetTileId(srcRoom - 1, row, srcCol);
                var left = srcCol > 0 ? sim.Level.GetTileId(srcRoom - 1, row, srcCol - 1) : TileId.Space;

                // Stable per-cell noise: same room and cell always lay the same bricks.
                int rnd = Hash(srcRoom, row, srcCol);

                // The brick bond runs across the whole row, so its phase must not vary
                // cell to cell.
                int seamPhase = Hash(srcRoom, row, 0) & 3;

                // A gate hangs as far down as its BLUESPEC says; DosTileArt builds the
                // lattice out of that, so nothing here needs clipping.
                int spec = tile == TileId.Gate ? sim.Level.GetSpec(srcRoom - 1, row, srcCol) : 0;

                int mod = sim.Level.GetTileModifier(srcRoom - 1, row, srcCol);

                var pieces = front ? DosTileArt.Front(tile) : DosTileArt.Back(tile, left, rnd, spec, mod, srcCol, seamPhase);
                if (pieces.Count == 0) continue;

                int cellX = col < 0 ? -SliverInset : col * TileW;
                int cellY = Coord.BlockTop(row);

                // Each torch runs the flame loop from its own phase, so a row of them
                // does not flicker in lockstep.
                int phase = srcCol * 3 + row * 5;

                foreach (var piece in pieces)
                    BlitPiece(piece, cellX, cellY, phase);
            }
    }

    private void BlitPiece(DosTileArt.Piece piece, int cellX, int cellY, int phase = 0)
    {
        IndexedImage? img;
        DatPalette pal;

        if (piece.From == DosTileArt.Bank.Flame)
        {
            int index = piece.Image + (piece.Frames > 1 ? (_tick + phase) % piece.Frames : 0);
            img = _flame[index];
            pal = _flame.PaletteForIndex(index);
        }
        else
        {
            img = _env.ById(piece.Image);
            pal = _env.PaletteForId(piece.Image);
        }

        if (img is null) return;
        Frame.Blit(img, pal, cellX + piece.X, cellY + piece.Y, mirror: false,
                   piece.SrcY, piece.SrcRows, EnvSkipMask);
    }

    /// <summary>
    /// Renders one candidate image per column at the floor position, over a brick
    /// wall, so the right piece for a tile can be picked by eye in a single frame.
    /// </summary>
    public void DrawPieceProbe(int[] candidates, int pieceY)
    {
        Frame.Clear(0, 0, 0);

        for (int col = 0; col < Coord.Cols && col < candidates.Length; col++)
        {
            int cellX = col * TileW;

            var body = _env.ById(364);
            if (body is not null)
                Frame.Blit(body, _env.PaletteForId(364), cellX, Coord.BlockTop(2) + 1, false);

            var img = _env.ById(candidates[col]);
            if (img is null) continue;
            Frame.Blit(img, _env.PaletteForId(candidates[col]), cellX, Coord.BlockTop(1) + pieceY, false);
        }
    }

    private void DrawKid(Simulation sim)
    {
        var ch = sim.Kid;
        var f = sim.KidFrame;
        if (f.IsBlank) return;

        var img = _kid[f.Image + 1];        // frame images are 0-based, banks 1-based
        if (img is null) return;

        // dx is in world units and applies in the facing direction; dy is in pixels.
        int px = ToPx(ch.X + (ch.Face < 0 ? -f.Dx : f.Dx));
        int py = ch.Y + f.Dy;

        // Sprites are anchored bottom-centre on the character position.
        Frame.Blit(img, _kid.Palette, px - img.Width / 2, py - img.Height, ch.FacingRight);
    }
}
