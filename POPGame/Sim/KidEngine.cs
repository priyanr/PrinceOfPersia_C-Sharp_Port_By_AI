using POPGame.Data;
using POPGame.Dos;
using POPGame.Input;

namespace POPGame.Sim;

/// <summary>
/// The kid's per-frame logic, ported from SDLPoP (the DOS game's own routines, which
/// match the Apple II CTRL.S / COLL.S / CTRLSUBS.S line for line where they overlap).
///
/// SDLPoP shares a set of globals between these routines (<c>cur_frame</c>,
/// <c>obj_x</c>, the collision edges, <c>curr_tile2</c> / <c>tile_col</c> from the last
/// <c>get_tile</c>, the latched controls). They are fields here, and the routines keep
/// their SDLPoP names in the comments so each one can be checked against the source:
/// <list type="bullet">
/// <item><c>KidEngine.cs</c> — frame order (play_kid_frame), tiles, frame/column
/// helpers (seg006), room exits (seg002).</item>
/// <item><c>KidEngine.Physics.cs</c> — floor checks, falls, landing, grabbing
/// (seg005 do_fall/land, seg006) and wall collisions (seg004).</item>
/// <item><c>KidEngine.Control.cs</c> — the player's controls (seg005 control_*).</item>
/// </list>
/// </summary>
public sealed partial class KidEngine
{
    private readonly SeqRunner _seq;
    private readonly DosTables _tables;
    private readonly RoomView _view;
    private readonly Level _level;
    private readonly Func<FrameDef, (int W, int H)> _imageSize;
    private readonly Hazards _hazards;
    private readonly int _levelNumber;

    private CharState _ch = null!;
    private SeqEffects _fx = null!;

    public KidEngine(SeqRunner seq, DosTables tables, RoomView view,
                     Func<FrameDef, (int W, int H)> imageSize, Hazards hazards, int levelNumber)
    {
        _seq = seq;
        _tables = tables;
        _view = view;
        _level = view.Level;
        _imageSize = imageSize;
        _hazards = hazards;
        _levelNumber = levelNumber;
        ClearCollRooms();
    }

    /// <summary>The room the level started in; its exit door can't be used (up_pressed).</summary>
    public int StartRoom { get; set; }

    /// <summary>
    /// jumped_through_mirror: set by is_obstacle when a running jump goes through the
    /// level 4 mirror. The original then releases the kid's shadow (jump_through_mirror,
    /// seg003); there are no other characters yet, so nothing reads it.
    /// </summary>
    public bool JumpedThroughMirror { get; set; }

    // ── per-frame order ───────────────────────────────────────────────────────

    /// <summary>
    /// play_kid_frame (seg000): control picks a sequence, the sequence poses the kid,
    /// gravity moves him, then collisions, bumps and the floor are resolved, and last
    /// the plates and loose floors he is on (check_press, check_knock).
    /// </summary>
    public void PlayKidFrame(CharState ch, InputState input, SeqEffects fx)
    {
        _ch = ch;
        _fx = fx;

        Timers();
        if (UpsideDown && !ch.Alive) UpsideDown = false;

        LoadFramDetCol();
        FellOut();
        ControlKid(input);
        if (ch.Room == 0) return;

        PlaySeq();
        FallAccel();
        FallSpeed();
        LoadFrameToObj();
        LoadFramDetCol();
        SetCharCollision();
        CheckCollisions();
        CheckBumped();
        CheckGatePush();
        CheckAction();
        _hazards.CheckPress(ch, _frame);
        CheckSpikeBelow();
        CheckSpiked();
        CheckChompedKid();
        _hazards.CheckKnock(ch, fx.JarFloor);
    }

    /// <summary>
    /// check_spike_below (seg006): spikes under the kid's feet, or anywhere below him
    /// down an open drop in this room, spring out.
    /// </summary>
    private void CheckSpikeBelow()
    {
        int rightCol = Coord.ColM7(_charXRight);
        if (rightCol < 0) return;
        int room = _ch.Room;
        for (int col = Coord.ColM7(_charXLeft); col <= rightCol; ++col)
        {
            int row = _ch.Row;
            bool notFinished;
            do
            {
                notFinished = false;
                if (GetTile(room, col, row) == TileId.Spikes)
                {
                    _hazards.StartAnimSpike(_posRoom, _posRow * Coord.Cols + _posCol);
                }
                else if (!TileIsFloor(_currTile2) && _currRoom != 0 && room == _currRoom)
                {
                    ++row;
                    notFinished = true;
                }
            } while (notFinished);
        }
    }

    /// <summary>
    /// check_spiked (seg006): running (frames 7..14) or starting a running jump (34..39)
    /// onto spikes that are springing out, or landing a jump (26, 43) on any spikes
    /// that are out, impales the kid.
    /// </summary>
    private void CheckSpiked()
    {
        int frame = _ch.Frame;
        if (GetTile(_ch.Room, _ch.Col, _ch.Row) != TileId.Spikes) return;
        int harmful = IsSpikeHarmful();
        if ((harmful >= 2 && (frame is >= 7 and < 15 || frame is >= 34 and < 40))
            || (frame is 43 or 26 && harmful != 0))
        {
            Spiked();
        }
    }

    /// <summary>
    /// is_spike_harmful (seg007), for the spikes of the last get_tile: 0 retracted or
    /// disabled, 2 springing out (1..4), 1 out and holding (bit 7).
    /// </summary>
    private int IsSpikeHarmful()
    {
        sbyte modifier = (sbyte)Modif();
        if (modifier is 0 or -1) return 0;
        if (modifier < 0) return 1;
        if (modifier < 5) return 2;
        return 0;
    }

    /// <summary>
    /// spiked (seg005): impaled on the spikes of the last get_tile, which stay out for
    /// good (0xFF) and harmless to anyone else.
    /// </summary>
    private void Spiked()
    {
        SetModif(0xFF);
        _ch.Y = Coord.FloorY(_ch.Row);
        _ch.X = Coord.BlockEdge(_tileCol) + 10;
        _ch.X = _ch.DxForward(8);
        _ch.FallY = 0;
        TakeHp(100);
        _seq.Start(_ch, Seq.Impale);
        PlaySeq();
    }

    /// <summary>
    /// check_chomped_kid (seg004): a shut chomper (frame 2) in a column the kid's body
    /// overlaps on his row cuts him in half.
    /// </summary>
    private void CheckChompedKid()
    {
        int tileRow = _ch.Row;
        for (int tileCol = 0; tileCol < 10; ++tileCol)
        {
            if (_currRowCollFlags[tileCol] == 0xFF
                && GetTile(_currRowCollRoom[tileCol], tileCol, tileRow) == TileId.Slicer
                && (Modif() & 0x7F) == 2)
            {
                Chomped();
            }
        }
    }

    /// <summary>chomped (seg004): blood on the jaws, and the kid is halved.</summary>
    private void Chomped()
    {
        SetModif(Modif() | 0x80);
        if (_ch.Frame == 178 || _ch.Room != _currRoom) return;
        _ch.X = Coord.BlockEdge(_tileCol) + 7;
        _ch.X = _ch.DxForward(_ch.FacingRight ? 6 : 7);   // 7 - !Char.direction
        _ch.Y = Coord.FloorY(_ch.Row);
        TakeHp(100);
        _seq.Start(_ch, Seq.Halve);
        PlaySeq();
    }

    /// <summary>exit_room (seg002): move the kid to the next room once he leaves this one.</summary>
    public void ExitRoom(CharState ch)
    {
        _ch = ch;
        if (ch.Room == 0) return;
        LoadFrameToObj();
        SetCharCollision();
        LeaveRoom();
    }

    /// <summary>
    /// do_startpos / set_start_pos (seg003): the kid starts facing the other way and
    /// plays the turn, so he ends up facing the level's start direction; level 1
    /// instead starts with the fall. Returns after the first frame has been played.
    /// </summary>
    public void StartPos(CharState ch, SeqEffects fx, int room, int block, int startFace, bool fallingEntry)
    {
        _ch = ch;
        _fx = fx;
        ch.Room = room;
        ch.Col = block % Coord.Cols;
        ch.Row = block / Coord.Cols;
        ch.X = Coord.BlockEdge(ch.Col) + Coord.BlockWidth;
        ch.Face = (sbyte)~startFace;
        _seq.Start(ch, fallingEntry ? Seq.StepFall : Seq.Turn);

        ch.Y = Coord.FloorY(ch.Row);
        ch.Hp = ch.MaxHp;
        ch.Alive = true;
        ch.FallX = ch.FallY = 0;
        ch.Repeat = 0;
        PlaySeq();

        ClearCollRooms();
        ResetControls();
        _grabTimer = 0;
        UpsideDown = false;
        _isFeatherFall = 0;
    }

    /// <summary>play_seq, then proc_get_object when the sequence reached its get-item opcode.</summary>
    private void PlaySeq()
    {
        _seq.Animate(_ch, _fx, _isFeatherFall != 0);
        if (_fx.DrankPotion)
        {
            _fx.DrankPotion = false;
            ProcGetObject();
        }
    }

    // ── frame, column and object (seg006, seg008) ─────────────────────────────

    private FrameDef _frame;

    /// <summary>load_frame.</summary>
    private void LoadFrame() => _frame = _tables.Frames[Math.Clamp(_ch.Frame, 0, DosTables.FrameCount)];

    /// <summary>load_fram_det_col.</summary>
    private void LoadFramDetCol()
    {
        LoadFrame();
        DetermineCol();
    }

    /// <summary>determine_col: the column under the kid's weight.</summary>
    private void DetermineCol() => _ch.Col = Coord.ColM7(DxWeight());

    /// <summary>
    /// dx_weight: x of the kid's weight — the frame's dx less its foot offset
    /// (<c>flags &amp; 0x1F</c>), forward from Char.x. GETBASEX in CTRLSUBS.S.
    /// </summary>
    private int DxWeight() => _ch.DxForward((sbyte)(_frame.Dx - (_frame.Flags & 0x1F)));

    /// <summary>distance_to_edge_weight.</summary>
    private int DistanceToEdgeWeight() => DistanceToEdge(DxWeight());

    /// <summary>distance_to_edge: units from xpos to the front edge of its block.</summary>
    private int DistanceToEdge(int xpos)
    {
        int d = Coord.OffsetM7(xpos);
        return _ch.FacingRight ? 13 - d : d;
    }

    /// <summary>back_delta_x.</summary>
    private int BackDeltaX(int dx) => _ch.Face < 0 ? dx : -dx;

    private int _objX, _objY;

    /// <summary>
    /// load_frame_to_obj (seg008): the sprite's anchor in the 280-wide screen space.
    /// The renderer uses the same formula (<see cref="SpriteX"/>).
    /// </summary>
    private void LoadFrameToObj()
    {
        LoadFrame();
        _objX = SpriteX(_ch, _frame);
        _objY = _frame.Dy + _ch.Y;
    }

    /// <summary>
    /// obj_x for a character's current frame: <c>2 * (x + dx) - 116</c>, plus one when
    /// the frame's odd-pixel flag and the facing agree. 280-wide screen coordinates.
    /// </summary>
    public static int SpriteX(CharState ch, FrameDef f)
    {
        int x = (ch.DxForward(f.Dx) << 1) - 116;
        if ((sbyte)(f.Flags ^ (byte)ch.Face) >= 0) x++;
        return x;
    }

    private int _charWidthHalf, _charHeight;
    private int _charXLeft, _charXRight, _charXLeftColl, _charXRightColl;

    /// <summary>
    /// set_char_collision (seg006): the kid's horizontal extent in x units, from the
    /// sprite's width. Facing left the sprite starts at obj_x; facing right it ends
    /// there. "Thin" frames shrink the collision box by 4 on each side.
    /// </summary>
    private void SetCharCollision()
    {
        var (w, h) = _frame.IsBlank ? (0, 0) : _imageSize(_frame);
        _charWidthHalf = (w + 1) / 2;
        _charHeight = h;

        _charXLeft = _objX / 2 + 58;
        if (_ch.FacingRight) _charXLeft -= _charWidthHalf;
        _charXLeftColl = _charXLeft;
        _charXRightColl = _charXRight = _charXLeft + _charWidthHalf;
        if (_frame.Thin)
        {
            _charXLeftColl += 4;
            _charXRightColl -= 4;
        }
    }

    // ── tiles (seg006 get_tile) ───────────────────────────────────────────────

    // Results of the last GetTile, as SDLPoP's curr_room / tile_col / tile_row / curr_tile2.
    private int _currRoom, _tileCol, _tileRow;
    private TileId _currTile2;
    private int _inFrontX;

    /// <summary>
    /// get_tile: the tile at (room, col, row), following the MAP links when the cell is
    /// outside the room. Beyond the edge of the level (room 0) there is wall.
    /// </summary>
    private TileId GetTile(int room, int col, int row)
    {
        _currRoom = room;
        _tileCol = col;
        _tileRow = row;
        FindRoomOfTile();

        _posRoom = _currRoom;
        _posRow = _tileRow;
        _posCol = _tileCol;

        _currTile2 = _currRoom > 0
            ? _level.GetTileId(_currRoom - 1, _tileRow, _tileCol)
            : TileId.Block;
        return _currTile2;
    }

    /// <summary>find_room_of_tile: columns are resolved before rows, as in the original.</summary>
    private void FindRoomOfTile()
    {
        while (true)
        {
            if (_tileCol < 0) { _tileCol += 10; if (_currRoom != 0) _currRoom = _level.Left(_currRoom); }
            else if (_tileCol >= 10) { _tileCol -= 10; if (_currRoom != 0) _currRoom = _level.Right(_currRoom); }
            else if (_tileRow < 0) { _tileRow += 3; if (_currRoom != 0) _currRoom = _level.Above(_currRoom); }
            else if (_tileRow >= 3) { _tileRow -= 3; if (_currRoom != 0) _currRoom = _level.Below(_currRoom); }
            else return;
        }
    }

    // curr_tilepos: where the last GetTile actually read. Callers overwrite _tileCol with
    // unresolved columns (for x positions), but the modifier still comes from here.
    private int _posRoom, _posRow, _posCol;

    /// <summary>curr_room_modif[curr_tilepos]: BLUESPEC of the last tile read.</summary>
    private int Modif() => _posRoom > 0 ? _level.GetSpec(_posRoom - 1, _posRow, _posCol) : 0;

    /// <summary>curr_room_modif[curr_tilepos] = value, for the last tile read.</summary>
    private void SetModif(int value)
    {
        if (_posRoom <= 0) return;
        _level.LiveBlueSpec[(_posRoom - 1) * Level.CellsPerScreen + _posRow * Coord.Cols + _posCol] = (byte)value;
    }

    private TileId GetTileAtChar() => GetTile(_ch.Room, _ch.Col, _ch.Row);
    private TileId GetTileAboveChar() => GetTile(_ch.Room, _ch.Col, _ch.Row - 1);

    private TileId GetTileInfrontofChar()
    {
        _inFrontX = _ch.Col + _ch.FaceSign;
        return GetTile(_ch.Room, _inFrontX, _ch.Row);
    }

    private TileId GetTileBehindChar() => GetTile(_ch.Room, _ch.Col - _ch.FaceSign, _ch.Row);
    private TileId GetTileBehindAboveChar() => GetTile(_ch.Room, _ch.Col - _ch.FaceSign, _ch.Row - 1);

    private TileId GetTileFrontAboveChar()
    {
        _inFrontX = _ch.Col + _ch.FaceSign;
        return GetTile(_ch.Room, _inFrontX, _ch.Row - 1);
    }

    /// <summary>tile_is_floor: CMPSPACE inverted (<see cref="RoomView.HasFloor"/>).</summary>
    private static bool TileIsFloor(TileId t) => RoomView.HasFloor(t);

    /// <summary>
    /// wall_type: 1 = wall at the right of the block (gate, doortops), 2 = wall at the
    /// left (mirror), 3 = chomper, 4 = solid on both sides, 0 = none.
    /// </summary>
    private static int WallType(TileId t) => t switch
    {
        TileId.Gate or TileId.PanelWF or TileId.PanelWOF => 1,
        TileId.Mirror => 2,
        TileId.Slicer => 3,
        TileId.Block => 4,
        _ => 0,
    };

    /// <summary>
    /// can_bump_into_gate: a gate stops the kid while its bottom is below his head.
    /// </summary>
    private bool CanBumpIntoGate() => (Modif() >> 2) + 6 < _charHeight;

    // ── leaving the room (seg002) ─────────────────────────────────────────────

    /// <summary>fell_out: dropping out of the bottom of the level kills.</summary>
    private void FellOut()
    {
        if (_ch.Alive && _ch.Room == 0)
        {
            _ch.Hp = 0;
            _ch.Alive = false;
            _fx.Died = true;
            _ch.Frame = 185;
        }
    }

    /// <summary>leave_room (seg002).</summary>
    private void LeaveRoom()
    {
        int chary = _ch.Y;
        int frame = _ch.Frame;
        var action = _ch.Action;
        int dir;

        if (action is not (CharAction.Bumped or CharAction.InFreefall or CharAction.InMidair)
            && (sbyte)(byte)chary < 10 && (sbyte)(byte)chary > -16)
        {
            dir = 2;    // up
        }
        else if ((byte)chary >= 211)    // Char.y is a byte in the original
        {
            dir = 3;    // down
        }
        else if (frame is >= 135 and < 150 or >= 110 and < 120 or >= 150 and < 163 or >= 166 and < 169
                 || action == CharAction.Turn)
        {
            return;
        }
        else if (!_ch.FacingRight)
        {
            if (_charXLeft <= 54) dir = 0;
            else if (_charXLeft >= 198) dir = 1;
            else return;
        }
        else
        {
            GetTile(_ch.Room, 9, _ch.Row);
            if (_currTile2 is not (TileId.PanelWF or TileId.PanelWOF) && _charXRight >= 201) dir = 1;
            else if (_charXRight <= 57) dir = 0;
            else return;
        }

        GotoOtherRoom(dir);
    }

    /// <summary>goto_other_room (seg002).</summary>
    private void GotoOtherRoom(int dir)
    {
        int room = _ch.Room;
        _ch.Room = dir switch
        {
            0 => _level.Left(room),
            1 => _level.Right(room),
            2 => _level.Above(room),
            _ => _level.Below(room),
        };
        switch (dir)
        {
            case 0: _ch.X += 140; break;
            case 1: _ch.X -= 140; break;
            case 2: _ch.Y += 189; _ch.Row = YToRowMod4(_ch.Y); break;
            default: _ch.Y -= 189; _ch.Row = YToRowMod4(_ch.Y); break;
        }
    }

    /// <summary>y_to_row_mod4.</summary>
    private static int YToRowMod4(int y) => (y + 60) / 63 % 4 - 1;
}
