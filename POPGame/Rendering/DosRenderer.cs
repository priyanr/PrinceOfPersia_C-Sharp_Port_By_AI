using POPGame.Data;
using POPGame.Dos;
using POPGame.Sim;

namespace POPGame.Rendering;

/// <summary>
/// Draws a room at the DOS native 320x200 into a <see cref="Framebuffer"/>, using
/// the graphics out of VDUNGEON.DAT or VPALACE.DAT (by the level's type), PRINCE.DAT
/// and KID.DAT.
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
    private DosImageBank _env;
    private int _envType;
    private readonly DosImageBank?[] _envBanks = new DosImageBank?[2];
    private readonly DosImageBank _flame;
    private readonly DosRoomDrawer _drawer;
    private readonly DosDrawTables _tables;

    /// <summary>
    /// The room drawer works in the original's screen coordinates. Captures of the real
    /// game put every background piece one row lower than those, consistently, so the
    /// whole layer is shifted here rather than any one offset being fudged.
    /// </summary>
    public const int BackgroundYOffset = 0;


    public Framebuffer Frame { get; } = new(ScreenW, ScreenH);

    public DosRenderer()
    {
        _kid = new DosImageBank(DosGame.File("KID.DAT"), 400);
        _env = EnvBank(0);
        _flame = new DosImageBank(DosGame.File("PRINCE.DAT"), 150);
        _tables = DosDrawTables.Load();
        foreach (string note in _tables.Notes) Console.WriteLine("draw tables: " + note);
        _drawer = new DosRoomDrawer(_tables);
    }

    /// <summary>
    /// load_lev_spr (seg000): the environment (chtab 6, resource 200) and wall (chtab 7,
    /// resource 360) images come from V + DUNGEON/PALACE + .DAT, by tbl_level_type.
    /// </summary>
    private DosImageBank EnvBank(int levelType)
    {
        levelType = levelType != 0 ? 1 : 0;
        return _envBanks[levelType] ??= new DosImageBank(
            DosGame.File(levelType == 0 ? "VDUNGEON.DAT" : "VPALACE.DAT"), 200, 360);
    }

    /// <summary>World x units -> screen pixels.</summary>
    public static int ToPx(int x) => (x - Coord.ScrnLeft) * TileW / Coord.BlockWidth;

    /// <param name="showFlash">
    /// Whether this frame shows the tick's flash. do_flash holds it for 2/60 s and the
    /// frame then carries on without it, so the live loop shows it only on the first
    /// two 60 Hz frames after a tick; a dump (one image per tick) always shows it.
    /// </param>
    public void Draw(Simulation sim, bool showFlash = true)
    {
        Frame.Clear(0, 0, 0);

        _envType = DosLevels.LevelType(sim.LevelNumber);
        _env = EnvBank(_envType);

        var kid = sim.Kid;
        if (kid.Room >= 1 && kid.Room <= Level.NumScreens)
            _drawer.Build(sim.Level, kid.Room, kid.Room, kid.Row, kid.Col, sim.TickCount, _envType);
        else
        {
            _drawer.Back.Clear();
            _drawer.Fore.Clear();
            _drawer.WipesBack.Clear();
            _drawer.WipesFore.Clear();
        }

        // draw_tables: wipes 0, back table, characters, wipes 1, front table.
        // draw_moving comes after the room: its redraws append to the back table.
        var mid = MidObjects(sim, out var kidDraw);
        if (kid.Room >= 1 && kid.Room <= Level.NumScreens)
            _drawer.BuildMid(kid.Room, kid.Frame, kidDraw?.Redraw2 ?? [],
                             kidDraw?.FloorOverlay ?? [], mid.Select(o => o.Tilepos).ToList());
        else
            _drawer.Mid.Clear();

        DrawOps(_drawer.WipesBack);
        DrawOps(_drawer.Back);
        DrawMid(mid);
        DrawOps(_drawer.WipesFore);
        DrawOps(_drawer.Fore);

        if (sim.Flash != 0 && showFlash) Flash(sim.Flash);
        if (sim.UpsideDown) FlipGameplay();
    }

    private readonly record struct MidObject(int Tilepos, int Y, bool IsMob, Action Draw);

    /// <summary>
    /// The objtable: falling floor pieces (draw_mobs), then the kid (draw_people),
    /// each filed under a tile (obj_tilepos). Falling pieces also mark tiles for
    /// redraw over them, like the kid; those go into <paramref name="kidDraw"/>'s lists.
    /// </summary>
    private List<MidObject> MidObjects(Simulation sim, out KidDrawInfo? kidDraw)
    {
        var objs = new List<MidObject>();
        kidDraw = null;
        int room = sim.Kid.Room;
        if (room < 1 || room > Level.NumScreens) return objs;

        var redraw2 = new List<int>();
        int above = sim.Level.Above(room), below = sim.Level.Below(room);
        foreach (var mob in sim.Hazards.Mobs)
        {
            if (MobY(mob, room, above, below) is not { } y) continue;
            int xh = mob.Xh;
            // draw_mob: filed under its own tile; the tile right of it (and the one
            // above that, once it straddles two rows) is redrawn over it.
            int col = xh >> 2, row = YToRowMod4(y);
            int tilepos = GetTilepos(col, row);
            objs.Add(new MidObject(tilepos < 0 ? 30 : tilepos, y, true, () => DrawMob(xh, y)));
            redraw2.Add(GetTilepos(col + 1, row));
            int topRow = YToRowMod4(y - 18);
            if (topRow != row) redraw2.Add(GetTilepos(col + 1, topRow));
        }

        var kd = sim.KidDraw(_drawer.LeveldoorYBottom, _drawer.LeveldoorRight);
        if (!sim.KidFrame.IsBlank)
            objs.Add(new MidObject(kd.ObjTilepos, kd.ObjY, false, () => DrawKid(sim, kd.Clip)));
        kidDraw = kd with { Redraw2 = [.. kd.Redraw2, .. redraw2] };
        return objs;
    }

    private static int YToRowMod4(int y) => (y + 60) / 63 % 4 - 1;

    private static int GetTilepos(int col, int row)
    {
        if (row < 0) return -(col + 1);
        if (row >= 3 || col >= 10 || col < 0) return 30;
        return row * 10 + col;
    }

    /// <summary>
    /// The midtable: the room drawer's redraws in tile order, with the objects filed
    /// under each tile drawn at its marker (draw_objtable_items_at_tile). Objects under
    /// one tile are taken last-added first and bubble-sorted as compare_curr_objs does:
    /// lower y first, but falling pieces among themselves the other way round.
    /// </summary>
    private void DrawMid(List<MidObject> objs)
    {
        foreach (var op in _drawer.Mid)
        {
            if (op.Set != DosRoomDrawer.Set.Objects)
            {
                DrawOps([op]);
                continue;
            }

            var here = new List<MidObject>();
            for (int i = objs.Count - 1; i >= 0; i--)
                if (objs[i].Tilepos == op.Id) here.Add(objs[i]);
            for (bool swapped = true; swapped;)
            {
                swapped = false;
                for (int i = 0; i < here.Count - 1; i++)
                {
                    var (a, b) = (here[i], here[i + 1]);
                    bool swap = a.IsMob && b.IsMob ? a.Y < b.Y : a.Y > b.Y;
                    if (!swap) continue;
                    (here[i], here[i + 1]) = (b, a);
                    swapped = true;
                }
            }
            foreach (var o in here) o.Draw();
        }
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

    // VGA colours 0..15 (custom->vga_palette, set at start-up): what a pickup flashes
    // the background with (flash_color), and what the mono blitter paints with.
    private static readonly (byte R, byte G, byte B)[] FlashColors =
    [
        (0, 0, 0), (0, 0, 170), (0, 170, 0), (0, 170, 170), (170, 0, 0), (170, 0, 170),
        (170, 85, 0), (170, 170, 170), (85, 85, 85), (85, 85, 255), (85, 255, 85),
        (85, 255, 255), (255, 85, 85), (255, 85, 255), (255, 255, 85), (255, 255, 255),
    ];

    /// <summary>
    /// method_3_blit_mono (seg009) paints with <c>palette[color]</c>, the global VGA
    /// palette, not the image's own colours: chomper blood (12), potion bubbles
    /// (9, 10, 12), the palace wall seams (6).
    /// </summary>
    private static readonly DatPalette VgaBase = MakeVgaBase();

    private static DatPalette MakeVgaBase()
    {
        var p = new DatPalette();
        for (int i = 0; i < 16; i++) (p.R[i], p.G[i], p.B[i]) = FlashColors[i];
        return p;
    }

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
                // Mono carries the wipe's height, Id its colour: 0 black, or 0x60 + n,
                // colour n of the wall palette (loaded at VGA 0x60 by load_lev_spr).
                byte r = 0, g = 0, b = 0;
                if (op.Id != 0)
                {
                    var wallPal = _env.PaletteForId(361);   // the wall group (images 361+)
                    int n = op.Id & 15;
                    (r, g, b) = (wallPal.R[n], wallPal.G[n], wallPal.B[n]);
                }
                Frame.FillRect(op.X, op.YBottom - op.Mono + 1 + BackgroundYOffset, op.Width, op.Mono, r, g, b);
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
            var pal = op.Mode == BlitMode.Mono ? VgaBase : bank.PaletteForId(id);
            Frame.Blit(img, pal, op.X, y, mirror: false, mode: op.Mode, monoColor: op.Mono & 15);
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

    private void DrawKid(Simulation sim, ClipRect c)
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
        // clip_char: walls, doortops and the floor above cut the sprite off.
        Frame.Blit(img, _kid.Palette, px, py - img.Height + 1, ch.FacingRight,
                   clip: (c.Left, c.Top, c.Right, c.Bottom));
    }
}
