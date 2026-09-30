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
/// Background layout comes from <see cref="DosRoomDrawer"/>, a port of the original's
/// own room-drawing routine; this class only turns its placements into pixels, in
/// the original order: back layer, characters, front layer.
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
    private readonly DosRoomDrawer _drawer;

    /// <summary>
    /// The room drawer works in the original's screen coordinates. Captures of the real
    /// game put every background piece one row lower than those, consistently, so the
    /// whole layer is shifted here rather than any one offset being fudged.
    /// </summary>
    public const int BackgroundYOffset = 0;

    private int _tick;

    public Framebuffer Frame { get; } = new(ScreenW, ScreenH);

    public DosRenderer()
    {
        _kid = new DosImageBank(DosGame.File("KID.DAT"), 400);
        _env = new DosImageBank(DosGame.File("VDUNGEON.DAT"), 200, 360);
        _flame = new DosImageBank(DosGame.File("PRINCE.DAT"), 150);
        _drawer = new DosRoomDrawer(DosDrawTables.Load());
    }

    /// <summary>World x units -> screen pixels.</summary>
    public static int ToPx(int x) => (x - Coord.ScrnLeft) * TileW / Coord.BlockWidth;

    public void Draw(Simulation sim)
    {
        _tick++;
        Frame.Clear(0, 0, 0);

        var kid = sim.Kid;
        if (kid.Room >= 1 && kid.Room <= Level.NumScreens)
            _drawer.Build(sim.Level, kid.Room, kid.Room, kid.Row, kid.Col, _tick);
        else
        {
            _drawer.Back.Clear();
            _drawer.Fore.Clear();
        }

        DrawOps(_drawer.Back);
        DrawKid(sim);
        DrawOps(_drawer.Fore);
    }

    private void DrawOps(List<DosRoomDrawer.Op> ops)
    {
        foreach (var op in ops)
        {
            if (op.Set == DosRoomDrawer.Set.Wipe)
            {
                // Mono carries the wipe's height.
                Frame.FillRect(op.X, op.YBottom - op.Mono + 1 + BackgroundYOffset, op.Width, op.Mono, 0, 0, 0);
                continue;
            }

            int id = op.Set switch
            {
                DosRoomDrawer.Set.Flame => 150 + op.Id,
                DosRoomDrawer.Set.Wall => 360 + op.Id,
                _ => 200 + op.Id,
            };
            var bank = op.Set == DosRoomDrawer.Set.Flame ? _flame : _env;
            var img = bank.ById(id);
            if (img is null) continue;

            int y = op.YBottom - img.Height + 1 + BackgroundYOffset;
            Frame.Blit(img, bank.PaletteForId(id), op.X, y, mirror: false,
                       mode: op.Mode, monoColor: op.Mono);
        }
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

        // load_frame_to_obj + draw_mid (SDLPoP seg008): obj_x is in the original's
        // 280-wide space and scales to 320. Facing left the sprite's left edge is at
        // obj_x; facing right the sprite is mirrored and its right edge is there.
        // Vertically y names the image's bottom row.
        int px = KidEngine.SpriteX(ch, f) * 320 / 280;
        if (ch.FacingRight) px -= img.Width;
        int py = ch.Y + f.Dy;
        Frame.Blit(img, _kid.Palette, px, py - img.Height + 1, ch.FacingRight);
    }
}
