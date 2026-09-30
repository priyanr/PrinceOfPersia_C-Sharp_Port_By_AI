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
    private readonly DosDrawTables _tables;

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
        _tables = DosDrawTables.Load();
        _drawer = new DosRoomDrawer(_tables);
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
        DrawMid(sim);
        DrawOps(_drawer.Fore);

        if (sim.Flash != 0) Flash(sim.Flash);
        if (sim.UpsideDown) FlipGameplay();
    }

    /// <summary>
    /// The objects between the back and front layers: the kid and any falling
    /// floor pieces, in the original's order (compare_curr_objs: lower y first, but
    /// pieces among themselves the other way round).
    /// </summary>
    private void DrawMid(Simulation sim)
    {
        var objs = new List<(int Y, bool IsMob, Action Draw)>();
        int room = sim.Kid.Room;
        if (room >= 1 && room <= Level.NumScreens)
        {
            int above = sim.Level.Above(room), below = sim.Level.Below(room);
            foreach (var mob in sim.Hazards.Mobs)
            {
                if (MobY(mob, room, above, below) is not { } y) continue;
                int xh = mob.Xh;
                objs.Add((y, true, () => DrawMob(xh, y)));
            }
        }
        var f = sim.KidFrame;
        objs.Add((sim.Kid.Y + f.Dy, false, () => DrawKid(sim)));

        // A stable bubble sort, as the original does it.
        for (bool swapped = true; swapped;)
        {
            swapped = false;
            for (int i = 0; i < objs.Count - 1; i++)
            {
                var (a, b) = (objs[i], objs[i + 1]);
                bool swap = a.IsMob && b.IsMob ? a.Y < b.Y : a.Y > b.Y;
                if (!swap) continue;
                (objs[i], objs[i + 1]) = (b, a);
                swapped = true;
            }
        }
        foreach (var o in objs) o.Draw();
    }

    /// <summary>draw_mob: where a falling piece shows in the drawn room, if it does.</summary>
    private static int? MobY(Hazards.Mob mob, int drawn, int roomA, int roomB)
    {
        if (mob.Room == drawn) return mob.Y >= 210 ? null : mob.Y;
        if (mob.Room == roomB && roomB != 0)
            return Math.Abs((int)(sbyte)(byte)mob.Y) >= 18 ? null : mob.Y + 192;
        if (mob.Room == roomA && roomA != 0)
            return mob.Y < 174 ? null : mob.Y - 189;
        return null;
    }

    /// <summary>
    /// draw_objtable_item, loose floor: the falling board is the loose floor's frame 10
    /// in three parts, left, bottom and right.
    /// </summary>
    private void DrawMob(int xh, int y)
    {
        const int Frame = 10;
        var ops = new List<DosRoomDrawer.Op>
        {
            new(DosRoomDrawer.Set.Env, _tables.LooseLeft[Frame], xh * 8, y - 3, BlitMode.Trans),
            new(DosRoomDrawer.Set.Env, _tables.LooseBottom[Frame], xh * 8, y, BlitMode.NoTrans),
            new(DosRoomDrawer.Set.Env, _tables.LooseRight[Frame], (xh + 4) * 8, y - 1, BlitMode.Trans),
        };
        DrawOps(ops);
    }

    // The VGA colours a pickup flashes the background with (flash_color).
    private static readonly (byte R, byte G, byte B)[] FlashColors =
    [
        (0, 0, 0), (0, 0, 170), (0, 170, 0), (0, 170, 170), (170, 0, 0), (170, 0, 170),
        (170, 85, 0), (170, 170, 170), (85, 85, 85), (85, 85, 255), (85, 255, 85),
        (85, 255, 255), (255, 85, 85), (255, 85, 255), (255, 255, 85), (255, 255, 255),
    ];

    /// <summary>do_flash: the background colour (black) becomes the flash colour for the tick.</summary>
    private void Flash(int color)
    {
        var (r, g, b) = FlashColors[color & 15];
        var px = Frame.Pixels;
        for (int i = 0; i < px.Length; i += 4)
        {
            if (px[i] != 0 || px[i + 1] != 0 || px[i + 2] != 0) continue;
            px[i] = r; px[i + 1] = g; px[i + 2] = b;
        }
    }

    /// <summary>upside_down: the 192-row play area is shown mirrored top to bottom.</summary>
    private void FlipGameplay()
    {
        const int Rows = 192;
        int stride = Frame.Width * 4;
        var px = Frame.Pixels;
        var tmp = new byte[stride];
        for (int top = 0, bot = Rows - 1; top < bot; top++, bot--)
        {
            Buffer.BlockCopy(px, top * stride, tmp, 0, stride);
            Buffer.BlockCopy(px, bot * stride, px, top * stride, stride);
            Buffer.BlockCopy(tmp, 0, px, bot * stride, stride);
        }
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
                _ => EnvId(op.Id),
            };
            var bank = op.Set == DosRoomDrawer.Set.Flame ? _flame : _env;
            var img = bank.ById(id);
            if (img is null) continue;

            int y = op.YBottom - img.Height + 1 + BackgroundYOffset;
            Frame.Blit(img, bank.PaletteForId(id), op.X, y, mirror: false,
                       mode: op.Mode, monoColor: op.Mono);
        }
    }

    // load_more_opt_graf (SDLPoP seg000): after loading the environment chtab, the
    // original loads these index ranges (1-based, inclusive) again from resource
    // 1200 + index. VDUNGEON keeps the big pillars, spikes, chompers and debris only
    // there, so without this they are simply missing.
    private static readonly byte[] OptGrafMin = [0x01, 0x1E, 0x4B, 0x4E, 0x56, 0x65, 0x7F, 0x0A];
    private static readonly byte[] OptGrafMax = [0x09, 0x1F, 0x4D, 0x53, 0x5B, 0x7B, 0x8F, 0x0D];

    /// <summary>Resource id of environment image <paramref name="index"/> (chtab 6).</summary>
    private int EnvId(int index)
    {
        for (int i = 0; i < OptGrafMin.Length; i++)
            if (index >= OptGrafMin[i] && index <= OptGrafMax[i] && _env.ById(1200 + index) is not null)
                return 1200 + index;
        return 200 + index;
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
