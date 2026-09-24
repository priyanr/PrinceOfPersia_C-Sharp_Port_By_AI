using POPGame.Data;
using POPGame.Dos;
using POPGame.Sim;

namespace POPGame.Rendering;

/// <summary>
/// Lays out a room the way the DOS game does, as a list of image placements.
///
/// This is a reimplementation of the original's room drawing (DRAW_ROOM and the
/// draw_tile_* family, as reconstructed in SDLPoP's seg008.c), driven by the tables
/// in <see cref="DosDrawTables"/>. The key facts that make the original's output come
/// out of it, rather than having to be tuned in by hand:
///
///   * a tile is drawn in two halves. Its <c>base</c> goes in its own cell, but its
///     <c>right</c> part — floor slab, gate lattice, torch bracket, posts' flank — is
///     drawn by the cell to its RIGHT, which asks "what is my left neighbour?". That
///     is why wall-mounted decoration sits one cell right of the tile that owns it.
///   * rows are drawn bottom to top, columns left to right, and the room ends with the
///     bottom row of the room above, which is where the ceiling comes from.
///   * walls pick their body by whether their left and right neighbours are walls, and
///     lay their brick seams and marks from a PRNG seeded by (room, row, col), so the
///     masonry is random-looking but the same every time.
///   * wall bodies, pillar fronts and a gate the kid is standing under are drawn in
///     front of the characters.
///
/// Positions follow the original: x is <c>xh*8 + xl</c> with <c>xh = 4*col</c>, and
/// y is the image's BOTTOM row, with <c>draw_bottom_y = 63*row + 65</c> and
/// <c>draw_main_y = draw_bottom_y - 3</c>.
/// </summary>
public sealed class DosRoomDrawer
{
    /// <summary>The image set a placement comes from (the original's chtab ids).</summary>
    public enum Set
    {
        /// <summary>Palette index 1 of PRINCE.DAT: flames, potions, sword.</summary>
        Flame = 1,
        /// <summary>VDUNGEON.DAT resource 200 + id.</summary>
        Env = 6,
        /// <summary>VDUNGEON.DAT resource 360 + id: the wall bodies and brick detail.</summary>
        Wall = 7,
        /// <summary>A black rectangle, not an image.</summary>
        Wipe = 99,
    }

    /// <param name="YBottom">Bottom row of the image; for a wipe, its bottom edge.</param>
    /// <param name="Mono">Palette index for <see cref="BlitMode.Mono"/>; for a wipe, its height.</param>
    /// <param name="Width">Only used by a wipe.</param>
    public readonly record struct Op(Set Set, int Id, int X, int YBottom, BlitMode Mode,
                                     int Mono = 0, int Width = 0);

    public List<Op> Back { get; } = [];
    public List<Op> Fore { get; } = [];

    private readonly DosDrawTables _t;

    // Tile ids by their original names, for readability of the ported logic.
    private const int Empty = 0, Floor = 1, Spike = 2, Pillar = 3, Gate = 4, Stuck = 5,
        Closer = 6, DoorTopWithFloor = 7, BigPillarTop = 9, Potion = 10, Loose = 11,
        DoorTop = 12, Opener = 15, LevelDoorLeft = 16, Chomper = 18, Torch = 19,
        Wall = 20, Sword = 22, LatticeDown = 26, LatticeSmall = 27, LatticeLeft = 28,
        LatticeRight = 29, TorchWithDebris = 30;

    // ---- per-draw state, named as in the original --------------------------------

    private Level _level = null!;
    private int _tick;
    private int _kidRoom, _kidRow, _kidCol;

    private int _drawnRoom, _drawnRow, _drawnCol;
    private int _drawXh, _drawMainY, _drawBottomY;
    private int _currTile, _currMod, _tileLeft, _modLeft;
    private int _roomL, _roomR, _roomA, _roomB, _roomBL;
    private readonly (int Tile, int Mod)[] _leftRoom = new (int, int)[3];
    private readonly (int Tile, int Mod)[] _rowBelowLeft = new (int, int)[10];
    private uint _seed;

    private List<Op> _addTo = null!;

    public DosRoomDrawer(DosDrawTables tables) => _t = tables;

    /// <summary>
    /// Lays out <paramref name="room"/>. The kid's cell decides whether a gate is drawn
    /// in front of him, and <paramref name="tick"/> drives torches and potions.
    /// </summary>
    public void Build(Level level, int room, int kidRoom, int kidRow, int kidCol, int tick)
    {
        Back.Clear();
        Fore.Clear();
        _level = level;
        _tick = tick;
        _kidRoom = kidRoom; _kidRow = kidRow; _kidCol = kidCol;

        // DRAW_ROOM: rows bottom to top, then the bottom row of the room above.
        _drawnRoom = room;
        LoadRoomLinks();
        LoadLeftRoom();
        for (_drawnRow = 2; _drawnRow >= 0; _drawnRow--)
        {
            LoadRowBelow();
            _drawBottomY = 63 * _drawnRow + 65;
            _drawMainY = _drawBottomY - 3;
            for (_drawnCol = 0; _drawnCol < 10; _drawnCol++)
            {
                LoadCurrAndLeftTile();
                DrawTile();
            }
        }

        _drawnRoom = _roomA;
        LoadRoomLinks();
        LoadLeftRoom();
        _drawnRow = 2;
        LoadRowBelow();
        for (_drawnCol = 0; _drawnCol < 10; _drawnCol++)
        {
            LoadCurrAndLeftTile();
            _drawMainY = -1;
            _drawBottomY = 2;
            DrawTileAboveRoom();
        }
    }

    // ---- level access -------------------------------------------------------------

    private bool ValidRoom(int r) => r >= 1 && r <= Level.NumScreens;

    private int RawTile(int room, int row, int col) => (int)_level.GetTileId(room - 1, row, col);

    /// <summary>
    /// The modifier a tile is drawn with — BLUESPEC as the original rewrites it when a
    /// room is loaded (LOAD_ALTER_MOD). Our simulation keeps a gate's height in 0..47
    /// where the original keeps 0..188, and walls get their neighbour bits here.
    /// </summary>
    private int AlteredMod(int room, int row, int col)
    {
        int spec = _level.GetSpec(room - 1, row, col);
        switch (RawTile(room, row, col))
        {
            case Gate: return Math.Min(spec, RoomView.GateOpen) * 4;
            case Loose: return 0;
            case Torch: case TorchWithDebris: return 0;
            case Wall:
            {
                // Bit 7: "no blue" (a modifier of 1 in the level); bits 0-1: which
                // neighbours are walls too. A missing neighbour room counts as a wall.
                int mod = (spec & 1) << 7;
                bool left = col == 0
                    ? _level.Left(room) is var l && (l == 0 || RawTile(l, row, 9) == Wall)
                    : RawTile(room, row, col - 1) == Wall;
                bool right = col == 9
                    ? _level.Right(room) is var r && (r == 0 || RawTile(r, row, 0) == Wall)
                    : RawTile(room, row, col + 1) == Wall;
                return mod | (left ? 2 : 0) | (right ? 1 : 0);
            }
            default: return spec;
        }
    }

    /// <summary>GET_TILE_TO_DRAW: a tile and its modifier, with pressed buttons drawn down.</summary>
    private (int Tile, int Mod) TileToDraw(int room, int col, int row, int missing)
    {
        if (col == -1) return _leftRoom[row];

        int tile, mod;
        if (ValidRoom(room))
        {
            tile = RawTile(room, row, col);
            mod = AlteredMod(room, row, col);
        }
        else
        {
            tile = missing;
            mod = 0;
        }

        // A button that is held down is drawn as the depressed version.
        if (tile == Closer && LinkTimer(mod) > 1) tile = Stuck;
        else if (tile == Opener && LinkTimer(mod) > 1) { tile = Floor; mod = 0; }

        return (tile, mod);
    }

    private int LinkTimer(int index) => index is >= 0 and < 256 ? _level.LinkMap[index] & 0x1F : 0;

    private void LoadRoomLinks()
    {
        _roomL = _roomR = _roomA = _roomB = _roomBL = 0;
        if (!ValidRoom(_drawnRoom)) return;

        _roomL = _level.Left(_drawnRoom);
        _roomR = _level.Right(_drawnRoom);
        _roomA = _level.Above(_drawnRoom);
        _roomB = _level.Below(_drawnRoom);
        _roomBL = _roomB != 0 ? _level.Left(_roomB) : _roomL != 0 ? _level.Below(_roomL) : 0;
    }

    private void LoadLeftRoom()
    {
        for (int row = 0; row < 3; row++)
            _leftRoom[row] = TileToDraw(_roomL, 9, row, Wall);
    }

    /// <summary>The row under the one being drawn, shifted one column right.</summary>
    private void LoadRowBelow()
    {
        int room, roomLeft, rowBelow;
        if (_drawnRow == 2) { room = _roomB; roomLeft = _roomBL; rowBelow = 0; }
        else { room = _drawnRoom; roomLeft = _roomL; rowBelow = _drawnRow + 1; }

        for (int col = 1; col < 10; col++)
            _rowBelowLeft[col] = TileToDraw(room, col - 1, rowBelow, Empty);
        _rowBelowLeft[0] = TileToDraw(roomLeft, 9, rowBelow, Wall);
    }

    private void LoadCurrAndLeftTile()
    {
        int missing = _drawnRow == 2 ? Floor : Wall;
        (_currTile, _currMod) = TileToDraw(_drawnRoom, _drawnCol, _drawnRow, missing);
        (_tileLeft, _modLeft) = TileToDraw(_drawnRoom, _drawnCol - 1, _drawnRow, missing);
        _drawXh = _drawnCol * 4;
    }

    // ---- emitting -----------------------------------------------------------------

    private void Add(List<Op> to, Set set, int id, int xh, int xl, int yBottom, BlitMode mode, int mono = 0)
    {
        if (id == 0) return;
        to.Add(new Op(set, id, xh * 8 + xl, yBottom, mode, mono));
    }

    private void Back_(Set set, int id, int xh, int xl, int y, BlitMode mode, int mono = 0)
        => Add(Back, set, id, xh, xl, y, mode, mono);

    private void Fore_(Set set, int id, int xh, int xl, int y, BlitMode mode, int mono = 0)
        => Add(Fore, set, id, xh, xl, y, mode, mono);

    /// <summary>The original's ptr_add_table: back or fore, depending on who is drawing.</summary>
    private void Table(Set set, int id, int xh, int xl, int y, BlitMode mode)
        => Add(_addTo, set, id, xh, xl, y, mode);

    private void Wipe(int x, int yBottom, int height, int width)
        => Back.Add(new Op(Set.Wipe, 0, x, yBottom, BlitMode.NoTrans, height, width));

    // ---- the tile drawing, one function per original routine ---------------------

    private TilePiece T(int tile) => _t.Tiles[Math.Clamp(tile, 0, DosDrawTables.TileCount - 1)];

    private void DrawTile()
    {
        _addTo = Back;
        DrawTileFloorRight();
        DrawTileAnimTopRight();
        DrawTileRight();
        DrawTileAnimRight();
        DrawTileBottom();
        DrawLoose();
        DrawTileBase();
        DrawTileAnim();
        DrawTileFore();
    }

    private void DrawTileAboveRoom()
    {
        _addTo = Back;
        DrawTileFloorRight();
        DrawTileAnimTopRight();
        DrawTileRight();
        DrawTileBottom();
        DrawLoose();
        DrawTileFore();
    }

    private bool CanSeeBottomLeft() =>
        _currTile is Empty or BigPillarTop or DoorTop or LatticeDown;

    private static bool TileIsFloor(int tile) => tile is not (Empty or BigPillarTop or DoorTop
        or Wall or LatticeDown or LatticeSmall or LatticeLeft or LatticeRight);

    private void DrawTileFloorRight()
    {
        if (!CanSeeBottomLeft()) return;
        DrawTileTopRight();
        if (T(_tileLeft).FloorRight == 0) return;
        Back_(Set.Env, 42, _drawXh, 0, T(Floor).RightY + _drawMainY, BlitMode.Black);
    }

    private void DrawTileTopRight()
    {
        int below = _rowBelowLeft[_drawnCol].Tile;
        if (below is DoorTopWithFloor or DoorTop) return;   // palace only
        if (below == Wall)
            Back_(Set.Wall, 2, _drawXh, 0, _drawBottomY, BlitMode.Or);
        else
            Back_(Set.Env, T(below).TopRightId, _drawXh, 0, _drawBottomY, BlitMode.Or);
    }

    private void DrawTileAnimTopRight()
    {
        if (_currTile is not (Empty or BigPillarTop or DoorTop)) return;
        var below = _rowBelowLeft[_drawnCol];
        if (below.Tile != Gate) return;

        // The top of a raised gate showing above the doorway of the row below.
        Back_(Set.Env, 68, _drawXh, 0, _drawBottomY, BlitMode.Black);
        int mod = Math.Min(below.Mod, 188);
        Back_(Set.Env, _t.DoorTop[(mod >> 2) % 8], _drawXh, 0, _drawBottomY, BlitMode.Or);
    }

    private void DrawTileRight()
    {
        if (_currTile == Wall) return;

        switch (_tileLeft)
        {
            default:
            {
                int id = T(_tileLeft).RightId;
                if (id != 0)
                {
                    var mode = BlitMode.Or;
                    if (_tileLeft == Stuck)
                    {
                        mode = BlitMode.Trans;
                        if (_currTile is Empty or Stuck || !TileIsFloor(_currTile)) id = 42;
                    }
                    Back_(Set.Env, id, _drawXh, 0, T(_tileLeft).RightY + _drawMainY, mode);
                }
                if (_tileLeft is Torch or TorchWithDebris)
                    Back_(Set.Env, 146, _drawXh, 0, _drawBottomY - 28, BlitMode.NoTrans);
                break;
            }
            case Empty:
                if (_modLeft > 3) return;
                Back_(Set.Env, _t.BlueLine1[_modLeft], _drawXh, 0, _t.BlueLineY[_modLeft] + _drawMainY, BlitMode.Or);
                break;
            case Floor:
            {
                Table(Set.Env, 42, _drawXh, 0, T(_tileLeft).RightY + _drawMainY, BlitMode.Trans);
                int num = _modLeft > 3 ? 0 : _modLeft;
                if (num == 0) return;   // dungeon: modifier 0 is a bare wall
                Back_(Set.Env, _t.BlueLine3[num], _drawXh, 0, _drawMainY - 20, BlitMode.NoTrans);
                break;
            }
            case DoorTopWithFloor:
            case DoorTop:
                return;   // palace only
            case Wall:
                Back_(Set.Wall, 1, _drawXh, 0, T(_tileLeft).RightY + _drawMainY, BlitMode.Or);
                break;
        }
    }

    private void DrawTileAnimRight()
    {
        switch (_tileLeft)
        {
            case Spike:
                Back_(Set.Env, _t.SpikesRight[SpikeFrame(_modLeft)], _drawXh, 0, _drawMainY - 7, BlitMode.Trans);
                break;
            case Gate:
                DrawGateBack();
                break;
            case Loose:
                Back_(Set.Env, _t.LooseRight[LooseFrame(_modLeft)], _drawXh, 0, _drawBottomY - 1, BlitMode.Or);
                break;
            case LevelDoorLeft:
                DrawLevelDoor();
                break;
            case Torch:
            case TorchWithDebris:
                Back_(Set.Flame, TorchFrame() + 1, _drawXh + 1, 0, _drawMainY - 40, BlitMode.NoTrans);
                break;
        }
    }

    private void DrawTileBottom()
    {
        int id;
        var set = Set.Env;
        var mode = BlitMode.NoTrans;

        switch (_currTile)
        {
            case Wall:
                id = _t.WallBottom[_currMod & 3];
                set = Set.Wall;
                break;
            case DoorTop:
                mode = BlitMode.Or;
                id = T(_currTile).BottomId;
                break;
            default:
                id = T(_currTile).BottomId;
                break;
        }

        Table(set, id, _drawXh, 0, _drawBottomY, mode);
        if (set == Set.Wall) WallPattern(false, false);
    }

    private void DrawLoose()
    {
        if (_currTile != Loose) return;
        int id = _t.LooseBottom[LooseFrame(_currMod)];
        Back_(Set.Env, id, _drawXh, 0, _drawBottomY, BlitMode.NoTrans);
        Fore_(Set.Env, id, _drawXh, 0, _drawBottomY, BlitMode.NoTrans);
    }

    private void DrawTileBase()
    {
        int id;
        int yBottom = _drawMainY;

        if (_tileLeft == LatticeDown && _currTile == DoorTop) { id = 6; yBottom += 3; }
        else if (_currTile == Loose) id = _t.LooseLeft[LooseFrame(_currMod)];
        else if (_currTile == Opener && _tileLeft == Empty) id = 148;   // no floor to the left
        else id = T(_currTile).BaseId;

        Table(Set.Env, id, _drawXh, 0, T(_currTile).BaseY + yBottom, BlitMode.Trans);
    }

    private void DrawTileAnim()
    {
        switch (_currTile)
        {
            case Spike:
                Table(Set.Env, _t.SpikesLeft[SpikeFrame(_currMod)], _drawXh, 0, _drawMainY - 2, BlitMode.Trans);
                break;

            case Potion:
            {
                int color = 12, size = 0;
                switch ((_currMod & 0xF8) >> 3)
                {
                    case 0: return;
                    case 5: case 6: color = 9; break;
                    case 3: case 4: color = 10; size = 1; break;
                    case 2: size = 1; break;
                }
                int y = _drawMainY - (size << 2) - 14;
                Back_(Set.Flame, 23, _drawXh + 3, 1, y, BlitMode.Black);
                Fore_(Set.Flame, _t.PotionBubble[PotionFrame()], _drawXh + 3, 1, y, BlitMode.Mono, color);
                break;
            }

            case Sword:
                Back_(Set.Flame, (_currMod == 1 ? 1 : 0) + 10, _drawXh, 0, _drawMainY - 3, BlitMode.Trans);
                break;

            case Chomper:
            {
                int n = _t.ChomperFrame[Math.Min(_currMod & 0x7F, 6)];
                Back_(Set.Env, _t.ChomperBottom[n], _drawXh, 0, _drawMainY, BlitMode.Trans);
                if ((_currMod & 0x80) != 0)
                    Back_(Set.Env, n + 114, _drawXh + 1, 4, _drawMainY - 6, BlitMode.Mono, 12);
                Back_(Set.Env, _t.ChomperTop[n], _drawXh, 0, _drawMainY - _t.ChomperY[Math.Min(n, 4)], BlitMode.Trans);
                break;
            }
        }
    }

    private void DrawTileFore()
    {
        if (_tileLeft == Gate && _kidRoom == _drawnRoom && _kidRow == _drawnRow && _kidCol == _drawnCol - 1)
            DrawGateFore();

        switch (_currTile)
        {
            case Spike:
                Fore_(Set.Env, _t.SpikesFore[SpikeFrame(_currMod)], _drawXh, 0, _drawMainY - 2, BlitMode.Trans);
                break;

            case Chomper:
            {
                int n = _t.ChomperFrame[Math.Min(_currMod & 0x7F, 6)];
                Fore_(Set.Env, _t.ChomperFore[n], _drawXh, 0, _drawMainY, BlitMode.Trans);
                if ((_currMod & 0x80) != 0)
                    Fore_(Set.Env, n + 119, _drawXh + 1, 4, _drawMainY - 6, BlitMode.Mono, 12);
                break;
            }

            case Wall:
                Fore_(Set.Wall, _t.WallMain[_currMod & 3], _drawXh, 0, _drawMainY, BlitMode.NoTrans);
                WallPattern(true, true);
                break;

            default:
            {
                var p = T(_currTile);
                int id = p.ForeId;
                if (id == 0) return;

                int xh = p.ForeX + _drawXh;
                int y = p.ForeY + _drawMainY;

                if (_currTile == Potion)
                {
                    int type = (_currMod & 0xF8) >> 3;
                    if (type is >= 2 and < 5) id = 13;
                    Fore_(Set.Flame, id, xh, 6, y, BlitMode.Trans);
                }
                else
                {
                    var mode = _currTile == Pillar || _currTile is >= LatticeSmall and < TorchWithDebris
                        ? BlitMode.NoTrans : BlitMode.Trans;
                    Fore_(Set.Env, id, xh, 0, y, mode);
                }
                break;
            }
        }
    }

    // ---- gates and the level door -------------------------------------------------

    private int _gateTopY, _gateBottomY;

    private void CalcGatePos()
    {
        _gateTopY = _drawBottomY - 62;
        int openness = (Math.Min(_modLeft, 188) >> 2) + 1;
        _gateBottomY = _drawMainY - openness;
    }

    private void DrawGateBack()
    {
        CalcGatePos();

        if (_gateBottomY + 12 < _drawMainY)
        {
            Back_(Set.Env, 50, _drawXh, 0, _gateBottomY, BlitMode.NoTrans);
        }
        else
        {
            // Nearly shut: the lattice's end reaches the floor, so redraw what it covers.
            Back_(Set.Env, T(Gate).RightId, _drawXh, 0, T(Gate).RightY + _drawMainY, BlitMode.NoTrans);
            if (CanSeeBottomLeft()) DrawTileTopRight();
            DrawTileBottom();
            DrawLoose();
            DrawTileBase();
            Back_(Set.Env, 51, _drawXh, 0, _gateBottomY - 2, BlitMode.Trans);
        }

        int y = _gateBottomY - 12;
        if (y < 192)
            for (; y >= 0 && y > 7 && y - 7 > _gateTopY; y -= 8)
                Back_(Set.Env, 52, _drawXh, 0, y, BlitMode.NoTrans);

        int frame = y - _gateTopY + 1;
        if (frame is > 0 and < 9)
            Back_(Set.Env, _t.DoorSlice[frame], _drawXh, 0, y, BlitMode.NoTrans);
    }

    private void DrawGateFore()
    {
        CalcGatePos();
        Fore_(Set.Env, 51, _drawXh, 0, _gateBottomY - 2, BlitMode.Trans);

        int y = _gateBottomY - 12;
        if (y < 192)
            for (; y >= 0 && y > 7 && y - 7 > _gateTopY; y -= 8)
                Fore_(Set.Env, 52, _drawXh, 0, y, BlitMode.Trans);
    }

    /// <summary>The exit: stairs behind a door that slides up 4px a step as it opens.</summary>
    private void DrawLevelDoor()
    {
        int yBottom = _drawMainY - 13;
        Back_(Set.Env, 99, _drawXh + 1, 0, yBottom, BlitMode.NoTrans);

        if (_modLeft != 0)
        {
            if (_level.KidStartScrn != _drawnRoom)
                Back_(Set.Env, 144, _drawXh + 1, 0, yBottom - 4, BlitMode.NoTrans);
            else
                Wipe(8 * (_drawXh + 1) + 2, yBottom - 4, 45, 39);
        }

        int doorY = yBottom - (_modLeft & 3) - 48;
        int y = yBottom - _modLeft;
        while (true)
        {
            Back_(Set.Env, 33, _drawXh + 1, 0, doorY, BlitMode.NoTrans);
            if (y > doorY) doorY += 4;
            else break;
        }
        Back_(Set.Env, 34, _drawXh + 1, 0, _drawMainY - 64, BlitMode.NoTrans);
    }

    // ---- masonry ------------------------------------------------------------------

    /// <summary>The original's random: Microsoft C's LCG, top 16 bits, modulo max+1.</summary>
    private int PRandom(int max)
    {
        _seed = _seed * 214013 + 2531011;
        return (int)((_seed >> 16) % (uint)(max + 1));
    }

    /// <summary>
    /// WALL_PATTERN (dungeon branch): the brick seams and chipped marks on a wall. The
    /// PRNG is reseeded from the cell's position every time, so the "random" masonry is
    /// identical on every visit and between the back and front halves of one wall.
    /// </summary>
    private void WallPattern(bool front, bool foreTable)
    {
        var savedTable = _addTo;
        _addTo = foreTable ? Fore : Back;
        uint savedSeed = _seed;

        _seed = (uint)(_drawnRoom + _drawnRow * 10 + _drawnCol);
        PRandom(1);

        int middleDivider = PRandom(1);
        int middleOffset = PRandom(4);
        int bottomDivider = PRandom(1);
        int bottomOffset = PRandom(4);

        const int Divider1 = 11, RndBlock = 13;

        switch (_currMod & 0x7F)
        {
            case 3:   // walls on both sides
                if (front)
                {
                    if (PRandom(4) == 0) Table(Set.Wall, RndBlock, _drawXh, 0, _drawBottomY - 42, BlitMode.NoTrans);
                    Table(Set.Wall, Divider1 + middleDivider, _drawXh + 1, middleOffset, _drawBottomY - 21, BlitMode.Trans);
                }
                Table(Set.Wall, Divider1 + bottomDivider, _drawXh, bottomOffset, _drawBottomY, BlitMode.Trans);
                if (front)
                {
                    if (PRandom(4) == 0) RightMark(PRandom(3), middleOffset);
                    if (PRandom(4) == 0) LeftMark(PRandom(4), middleOffset - middleDivider, bottomOffset - bottomDivider);
                }
                break;

            case 0:   // a lone wall
                if (front && PRandom(6) == 0)
                    LeftMark(PRandom(1), middleOffset - middleDivider, bottomOffset - bottomDivider);
                break;

            case 1:   // wall on the right only
                if (front)
                {
                    if (PRandom(4) == 0) Table(Set.Wall, RndBlock, _drawXh, 0, _drawBottomY - 42, BlitMode.NoTrans);
                    Table(Set.Wall, Divider1 + middleDivider, _drawXh + 1, middleOffset, _drawBottomY - 21, BlitMode.Trans);
                    if (PRandom(4) == 0) RightMark(PRandom(3), middleOffset);
                    if (PRandom(4) == 0) LeftMark(PRandom(3), middleOffset - middleDivider, bottomOffset - bottomDivider);
                }
                break;

            case 2:   // wall on the left only
                if (front)
                    Table(Set.Wall, Divider1 + middleDivider, _drawXh + 1, middleOffset, _drawBottomY - 21, BlitMode.Trans);
                Table(Set.Wall, Divider1 + bottomDivider, _drawXh, bottomOffset, _drawBottomY, BlitMode.Trans);
                if (front)
                {
                    if (PRandom(4) == 0) RightMark(PRandom(1) + 2, middleOffset);
                    if (PRandom(4) == 0) LeftMark(PRandom(4), middleOffset - middleDivider, bottomOffset - bottomDivider);
                }
                break;
        }

        _seed = savedSeed;
        _addTo = savedTable;
    }

    private void LeftMark(int variant, int middle, int bottom)
    {
        const int MarkTL = 14, MarkBL = 15;
        int id = variant % 2 != 0 ? MarkBL : MarkTL;
        int xl = variant > 3 ? bottom + 6 : variant > 1 ? middle + 6 : 0;
        int xh = _drawXh + (variant is 2 or 3 ? 1 : 0);
        Table(Set.Wall, id, xh, xl, _drawBottomY - _t.LeftMarkY[variant], BlitMode.Trans);
    }

    private void RightMark(int variant, int middle)
    {
        const int MarkTR = 16, MarkBR = 17;
        int id = variant % 2 != 0 ? MarkBR : MarkTR;
        int xl = variant < 2 ? 24 : middle - 3;
        Table(Set.Wall, id, _drawXh + (variant > 1 ? 1 : 0), xl, _drawBottomY - _t.RightMarkY[variant], BlitMode.Trans);
    }

    // ---- animation frames ---------------------------------------------------------

    private static int SpikeFrame(int mod) => (mod & 0x80) != 0 ? 5 : Math.Clamp(mod, 0, 9);

    private static int LooseFrame(int mod)
    {
        if ((mod & 0x80) != 0)
        {
            mod &= 0x7F;
            if (mod > 10) return 1;
        }
        return Math.Clamp(mod, 0, 11);
    }

    /// <summary>Each torch runs the nine-frame flame loop from its own phase.</summary>
    private int TorchFrame() => (_tick + _drawnCol * 3 + _drawnRow * 5) % 9;

    /// <summary>Bubbles cycle through frames 1..7; frame 0 is "no bubble".</summary>
    private int PotionFrame() => 1 + (_tick + _drawnCol) % 7;
}
