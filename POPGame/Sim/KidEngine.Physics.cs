using POPGame.Data;

namespace POPGame.Sim;

public sealed partial class KidEngine
{
    public const int FallingSpeedAccel = 3;
    public const int FallingSpeedMax = 33;
    public const int FallingSpeedAccelFeather = 1;
    public const int FallingSpeedMaxFeather = 4;

    private int _grabTimer;
    private int _fallFrame;

    // ── gravity (seg006) ──────────────────────────────────────────────────────

    /// <summary>fall_accel.</summary>
    private void FallAccel()
    {
        if (_ch.Action != CharAction.InFreefall) return;
        _ch.FallY = _isFeatherFall != 0
            ? Math.Min(_ch.FallY + FallingSpeedAccelFeather, FallingSpeedMaxFeather)
            : Math.Min(_ch.FallY + FallingSpeedAccel, FallingSpeedMax);
    }

    /// <summary>fall_speed.</summary>
    private void FallSpeed()
    {
        _ch.Y += _ch.FallY;
        if (_ch.Action == CharAction.InFreefall)
        {
            _ch.X = _ch.DxForward(_ch.FallX);
            LoadFramDetCol();
        }
    }

    /// <summary>check_action: which floor test applies depends on the action.</summary>
    private void CheckAction()
    {
        var action = _ch.Action;
        int frame = _ch.Frame;

        if (action is CharAction.HangStraight or CharAction.Bumped)
        {
            if (frame == 109) CheckOnFloor();       // crouching
        }
        else if (action == CharAction.InFreefall)
        {
            DoFall();
        }
        else if (action == CharAction.InMidair)
        {
            if (frame is >= 102 and < 106) CheckGrab();
        }
        else if (action != CharAction.HangClimb)
        {
            CheckOnFloor();
        }
    }

    /// <summary>
    /// check_on_floor: frames flagged "needs floor" (0x40, fcheckmark) start a fall
    /// when there is nothing underfoot. A kid standing inside a wall is pushed out
    /// first.
    /// </summary>
    private void CheckOnFloor()
    {
        if (!_frame.Check) return;
        if (GetTileAtChar() == TileId.Block) InWall();
        if (TileIsFloor(_currTile2)) return;

        // Special event: level 12's hidden bridge. Once the kid has united with his
        // shadow, floor appears under him on the top row of room 2, and of room 13 from
        // column 6 on: this tile and the next one along.
        if (_levelNumber == 12 && UnitedWithShadow < 0 && _ch.Row == 0
            && (_ch.Room == 2 || (_ch.Room == 13 && _tileCol >= 6)))
        {
            SetTileAtPos(0, TileId.Floor);
            SetTileAtPos(1, TileId.Floor);
        }
        else
        {
            StartFall();
        }
    }

    /// <summary>curr_room_tiles[curr_tilepos + offset] = tile, for the last tile read.</summary>
    private void SetTileAtPos(int offset, TileId tile)
    {
        int tilepos = _posRow * Coord.Cols + _posCol + offset;
        if (_posRoom <= 0 || tilepos >= Level.CellsPerScreen) return;
        _level.LiveBlueType[(_posRoom - 1) * Level.CellsPerScreen + tilepos] = (byte)tile;
    }

    /// <summary>start_fall: the fall sequence depends on the frame the kid dropped out of.</summary>
    private void StartFall()
    {
        int frame = _ch.Frame;
        _ch.Row++;
        _hazards.StartChompers(_ch);
        _fallFrame = frame;

        int seq;
        if (frame == 9) seq = Seq.StepFall;                         // run
        else if (frame == 13) seq = Seq.StepFall2;                  // run
        else if (frame == 26) seq = Seq.JumpFall;                   // standing jump landing
        else if (frame == 44) seq = Seq.RJumpFall;                  // running jump landing
        else if (frame is >= 81 and < 86)                           // hangdrop
        {
            seq = Seq.StepFall2;
            _ch.X = _ch.DxForward(5);
            LoadFramDetCol();
        }
        else if (frame is >= 150 and < 180)                         // with sword
        {
            if (!_ch.FacingRight && DistanceToEdgeWeight() <= 7)
                _ch.X = _ch.DxForward(-5);
            seq = Seq.FightFall;
        }
        else seq = Seq.StepFall;

        _seq.Start(_ch, seq);
        PlaySeq();
        LoadFramDetCol();

        if (GetTileAtChar() == TileId.Block)
        {
            InWall();
            return;
        }
        if (GetTileInfrontofChar() == TileId.Block)
        {
            if (_fallFrame != 44 || DistanceToEdgeWeight() >= 6)
                _ch.X = _ch.DxForward(-1);
            else
            {
                _seq.Start(_ch, SeqStartFallInFrontOfWall);
                PlaySeq();
            }
            LoadFramDetCol();
        }
    }

    /// <summary>seq_104_start_fall_in_front_of_wall.</summary>
    private const int SeqStartFallInFrontOfWall = 104;

    /// <summary>in_wall: push a kid who ended up inside a wall back out of it.</summary>
    private void InWall()
    {
        int dx = DistanceToEdgeWeight();
        if (dx >= 8 || GetTileInfrontofChar() == TileId.Block)
            dx = 6 - dx;
        else
            dx += 4;
        _ch.X = _ch.DxForward(dx);
        LoadFramDetCol();
        GetTileAtChar();
    }

    /// <summary>
    /// check_grab: falling with the action button held, the kid catches the ledge he
    /// is falling past (FALLON in CTRL.S).
    /// </summary>
    private void CheckGrab()
    {
        if (!(_controlShift && _ch.FallY < 32 && _ch.Alive
              && (ushort)Coord.FloorY(_ch.Row) <= (ushort)((byte)_ch.Y + 25)))
            return;

        int oldX = _ch.X;
        _ch.X = _ch.DxForward(-8);
        LoadFramDetCol();
        if (!CanGrabFrontAbove())
        {
            _ch.X = oldX;
            return;
        }

        _ch.X = _ch.DxForward(DistanceToEdgeWeight());
        _ch.Y = Coord.FloorY(_ch.Row);
        _ch.FallY = 0;
        _seq.Start(_ch, Seq.FallHang);
        PlaySeq();
        _grabTimer = 12;
    }

    /// <summary>can_grab_front_above.</summary>
    private bool CanGrabFrontAbove()
    {
        _throughTile = GetTileAboveChar();
        GetTileFrontAboveChar();
        return CanGrab();
    }

    private TileId _throughTile;

    /// <summary>can_grab: can the kid grab curr_tile2 through _throughTile?</summary>
    private bool CanGrab()
    {
        int modifier = Modif();
        if (_throughTile == TileId.Block) return false;
        if (_throughTile == TileId.PanelWOF && _ch.FacingRight) return false;
        if (TileIsFloor(_throughTile)) return false;
        if (_currTile2 == TileId.Loose && modifier != 0) return false;
        if (_currTile2 == TileId.PanelWF && !_ch.FacingRight) return false;
        return TileIsFloor(_currTile2);
    }

    /// <summary>do_fall (seg005): in freefall, look for the floor the kid has reached.</summary>
    private void DoFall()
    {
        // Unsigned, as in the original: Char.y is a byte and y_land a word.
        if ((ushort)Coord.FloorY(_ch.Row) > (ushort)(byte)_ch.Y)
        {
            CheckGrab();
            return;
        }

        if (GetTileAtChar() == TileId.Block) InWall();
        if (TileIsFloor(_currTile2)) Land();
        else _ch.Row++;
    }

    /// <summary>
    /// land (seg005): landing on spikes that are out impales; otherwise it depends on
    /// how fast he was falling: under 22 is a soft crouch, under 33 costs a hit point
    /// (medium land), faster kills.
    /// </summary>
    private void Land()
    {
        _ch.Y = Coord.FloorY(_ch.Row);

        bool onSpikes = GetTileAtChar() == TileId.Spikes;
        if (!onSpikes)
        {
            if (!TileIsFloor(GetTileInfrontofChar()) && DistanceToEdgeWeight() < 3)
                _ch.X = _ch.DxForward(-3);
            _hazards.StartChompers(_ch);
        }

        // The original jumps from the "on spikes" test straight into the alive branch's
        // spike check (goto loc_5EE6), dead or not. For the alive, the check reads the
        // tile behind (well into the block) or the tile underfoot.
        if (onSpikes
            || (_ch.Alive && ((DistanceToEdgeWeight() >= 12 && GetTileBehindChar() == TileId.Spikes)
                              || GetTileAtChar() == TileId.Spikes)))
        {
            if (IsSpikeHarmful() != 0)
            {
                Spiked();
                return;
            }
        }
        else if (!_ch.Alive)
        {
            TakeHp(100);
            _seq.Start(_ch, Seq.HardLand);
            PlaySeq();
            _ch.FallY = 0;
            return;
        }

        int seq;
        if (_ch.FallY < 22)
        {
            seq = Seq.SoftLand;
        }
        else if (_ch.FallY < 33)
        {
            seq = TakeHp(1) ? Seq.HardLand : Seq.MedLand;
        }
        else
        {
            TakeHp(100);
            seq = Seq.HardLand;
        }

        _seq.Start(_ch, seq);
        PlaySeq();
        _ch.FallY = 0;
    }

    /// <summary>take_hp: returns true when this killed him.</summary>
    private bool TakeHp(int count)
    {
        if (count >= _ch.Hp)
        {
            _ch.Hp = 0;
            _ch.Alive = false;
            _fx.Died = true;
            return true;
        }
        _ch.Hp -= count;
        return false;
    }

    // ── wall collisions (seg004) ──────────────────────────────────────────────

    private static readonly int[] WallDistFromLeft = [0, 10, 0, -1, 0, 0];
    private static readonly int[] WallDistFromRight = [0, 0, 10, 13, 0, 0];

    // Collision data per column for this frame's row, the rows above and below, and
    // last frame. A bump is a column whose edge flag turned on since last frame.
    private readonly int[] _prevCollRoom = new int[10], _currRowCollRoom = new int[10];
    private readonly int[] _belowRowCollRoom = new int[10], _aboveRowCollRoom = new int[10];
    private readonly byte[] _prevCollFlags = new byte[10], _currRowCollFlags = new byte[10];
    private readonly byte[] _belowRowCollFlags = new byte[10], _aboveRowCollFlags = new byte[10];
    private int _collisionRow, _prevCollisionRow;
    private int _bumpColLeftOfWall, _bumpColRightOfWall;
    private int _leftCheckedCol, _rightCheckedCol;
    private int _collTileLeftXpos;

    /// <summary>clear_coll_rooms: forget last frame's collision data (new level, new room).</summary>
    private void ClearCollRooms()
    {
        Array.Fill(_prevCollRoom, -1);
        Array.Fill(_currRowCollRoom, -1);
        Array.Fill(_belowRowCollRoom, -1);
        Array.Fill(_aboveRowCollRoom, -1);
        _prevCollisionRow = -1;
    }

    /// <summary>check_collisions: find wall edges the kid has just moved into.</summary>
    private void CheckCollisions()
    {
        _bumpColLeftOfWall = _bumpColRightOfWall = -1;
        if (_ch.Action == CharAction.Turn) return;

        _collisionRow = _ch.Row;
        MoveCollToPrev();
        _prevCollisionRow = _collisionRow;
        _rightCheckedCol = Math.Min(Coord.ColM7(_charXRightColl) + 2, 11);
        _leftCheckedCol = Coord.ColM7(_charXLeftColl) - 1;
        GetRowCollisionData(_collisionRow, _currRowCollRoom, _currRowCollFlags);
        GetRowCollisionData(_collisionRow + 1, _belowRowCollRoom, _belowRowCollFlags);
        GetRowCollisionData(_collisionRow - 1, _aboveRowCollRoom, _aboveRowCollFlags);

        for (int column = 9; column >= 0; column--)
        {
            if (_currRowCollRoom[column] < 0 || _prevCollRoom[column] != _currRowCollRoom[column])
                continue;
            if ((_prevCollFlags[column] & 0x0F) == 0 && (_currRowCollFlags[column] & 0x0F) != 0)
                _bumpColLeftOfWall = column;
            if ((_prevCollFlags[column] & 0xF0) == 0 && (_currRowCollFlags[column] & 0xF0) != 0)
                _bumpColRightOfWall = column;
        }
    }

    /// <summary>move_coll_to_prev: last frame's row data becomes "previous".</summary>
    private void MoveCollToPrev()
    {
        int[] rooms;
        byte[] flags;
        if (_collisionRow == _prevCollisionRow || _collisionRow + 3 == _prevCollisionRow
            || _collisionRow - 3 == _prevCollisionRow)
        {
            rooms = _currRowCollRoom; flags = _currRowCollFlags;
        }
        else if (_collisionRow + 1 == _prevCollisionRow || _collisionRow - 2 == _prevCollisionRow)
        {
            rooms = _aboveRowCollRoom; flags = _aboveRowCollFlags;
        }
        else
        {
            rooms = _belowRowCollRoom; flags = _belowRowCollFlags;
        }

        for (int column = 0; column < 10; column++)
        {
            _prevCollRoom[column] = rooms[column];
            _prevCollFlags[column] = flags[column];
            _belowRowCollRoom[column] = -1;
            _aboveRowCollRoom[column] = -1;
            _currRowCollRoom[column] = -1;
            // FIX_COLL_FLAGS (on by default in SDLPoP): stale flags would otherwise
            // leak into a column that had no data this frame.
            _currRowCollFlags[column] = 0;
            _belowRowCollFlags[column] = 0;
            _aboveRowCollFlags[column] = 0;
        }
    }

    /// <summary>get_row_collision_data.</summary>
    private void GetRowCollisionData(int row, int[] rooms, byte[] flags)
    {
        int room = _ch.Room;
        _collTileLeftXpos = Coord.BlockEdge(_leftCheckedCol) + 7;
        for (int column = _leftCheckedCol; column <= _rightCheckedCol; column++)
        {
            int leftWall = GetLeftWallXpos(room, column, row);
            int rightWall = GetRightWallXpos(room, column, row);
            byte f = 0;
            if (leftWall < _charXRightColl) f |= 0x0F;      // kid is past the wall's left edge
            if (rightWall > _charXLeftColl) f |= 0xF0;      // kid is before the wall's right edge
            flags[_tileCol] = f;
            rooms[_tileCol] = _currRoom;
            _collTileLeftXpos += Coord.BlockWidth;
        }
    }

    /// <summary>get_left_wall_xpos.</summary>
    private int GetLeftWallXpos(int room, int column, int row)
    {
        int type = WallType(GetTile(room, column, row));
        return type != 0 ? WallDistFromLeft[type] + _collTileLeftXpos : 0xFF;
    }

    /// <summary>get_right_wall_xpos.</summary>
    private int GetRightWallXpos(int room, int column, int row)
    {
        int type = WallType(GetTile(room, column, row));
        return type != 0 ? _collTileLeftXpos - WallDistFromRight[type] + 13 : 0;
    }

    /// <summary>check_bumped: react to a wall edge crossed this frame.</summary>
    private void CheckBumped()
    {
        if (_ch.Action is CharAction.HangClimb or CharAction.HangStraight) return;
        if (_ch.Frame is >= 135 and < 149) return;      // climbing up

        if (_bumpColLeftOfWall >= 0) CheckBumpedLookRight();
        else if (_bumpColRightOfWall >= 0) CheckBumpedLookLeft();
    }

    /// <summary>check_bumped_look_left.</summary>
    private void CheckBumpedLookLeft()
    {
        if (!_ch.FacingRight && IsObstacleAtCol(_bumpColRightOfWall))
            Bumped(GetRightWallXpos(_currRoom, _tileCol, _tileRow) - _charXLeftColl, pushLeft: false);
    }

    /// <summary>check_bumped_look_right.</summary>
    private void CheckBumpedLookRight()
    {
        if (_ch.FacingRight && IsObstacleAtCol(_bumpColLeftOfWall))
            Bumped(GetLeftWallXpos(_currRoom, _tileCol, _tileRow) - _charXRightColl, pushLeft: true);
    }

    /// <summary>is_obstacle_at_col.</summary>
    private bool IsObstacleAtCol(int tileCol)
    {
        int row = _ch.Row;
        if (row < 0) row += 3;
        if (row >= 3) row -= 3;
        GetTile(_currRowCollRoom[tileCol], tileCol, row);
        return IsObstacle();
    }

    /// <summary>
    /// is_obstacle: potions never block; a gate only while low; a chomper only while
    /// closed; a mirror not to a running jump from right to left, which goes through it.
    /// </summary>
    private bool IsObstacle()
    {
        if (_currTile2 == TileId.Flask) return false;
        if (_currTile2 == TileId.Gate && !CanBumpIntoGate()) return false;
        if (_currTile2 == TileId.Slicer && Modif() != RoomView.SlicerExtended) return false;
        if (_currTile2 == TileId.Mirror && _ch.Frame is >= 39 and < 44 && !_ch.FacingRight)
        {
            SetModif(0x56);                 // the mirror is jumped through
            JumpedThroughMirror = true;
            return false;
        }
        _collTileLeftXpos = XposInDrawnRoom(Coord.BlockEdge(_tileCol)) + 7;
        return true;
    }

    /// <summary>xpos_in_drawn_room: the tile's x as seen from the kid's room.</summary>
    private int XposInDrawnRoom(int xpos)
    {
        int drawn = _ch.Room;
        if (_currRoom == drawn) return xpos;

        int left = _level.Left(drawn), right = _level.Right(drawn), below = _level.Below(drawn);
        int belowLeft = below != 0 ? _level.Left(below) : left != 0 ? _level.Below(left) : 0;
        int belowRight = below != 0 ? _level.Right(below) : right != 0 ? _level.Below(right) : 0;

        if (_currRoom == left || _currRoom == belowLeft) return xpos - 140;
        if (_currRoom == right || _currRoom == belowRight) return xpos + 140;
        return xpos;
    }

    /// <summary>
    /// bumped: move the kid back out of the wall by <paramref name="dx"/>, then either
    /// bump against it standing (floor there) or bounce off it into a fall.
    /// </summary>
    private void Bumped(int dx, bool pushLeft)
    {
        if (!_ch.Alive || _ch.Frame == 177) return;

        _ch.X += (sbyte)dx;
        if (pushLeft)
        {
            if (_currTile2 == TileId.Block)
                GetTile(_currRoom, --_tileCol, _tileRow);
        }
        else if (_currTile2 is TileId.PanelWOF or TileId.PanelWF or TileId.Block)
        {
            ++_tileCol;
            if (_currRoom == 0 && _tileCol == 10)
            {
                _currRoom = _ch.Room;
                _tileCol = 0;
            }
            GetTile(_currRoom, _tileCol, _tileRow);
        }

        if (TileIsFloor(_currTile2)) BumpedFloor();
        else BumpedFall();
    }

    /// <summary>bumped_fall.</summary>
    private void BumpedFall()
    {
        _ch.X = _ch.DxForward(-4);
        if (_ch.Action == CharAction.InFreefall)
            _ch.FallX = 0;
        else
        {
            _seq.Start(_ch, Seq.BumpFall);
            PlaySeq();
        }
        _fx.SmackWall = true;
    }

    /// <summary>bumped_floor.</summary>
    private void BumpedFloor()
    {
        // Unsigned: a kid below the floor line also counts as "too high to stand".
        if ((ushort)(Coord.FloorY(_ch.Row) - (byte)_ch.Y) >= 15)
        {
            BumpedFall();
            return;
        }

        _ch.Y = Coord.FloorY(_ch.Row);
        if (_ch.FallY >= 22)
        {
            _ch.X = _ch.DxForward(-5);
            return;
        }

        _ch.FallY = 0;
        int frame = _ch.Frame;
        bool hard = frame is 24 or 25 or >= 40 and < 43 or >= 102 and < 107;
        _seq.Start(_ch, hard ? Seq.HardBump : Seq.Bump);
        PlaySeq();
        _fx.SmackWall = true;
    }

    /// <summary>check_gate_push: a closing gate shoves a standing kid out from under it.</summary>
    private void CheckGatePush()
    {
        int frame = _ch.Frame;
        if (!(_ch.Action == CharAction.Turn || frame == 15 || frame is >= 108 and < 111)) return;

        GetTileAtChar();
        int origCol = _tileCol;
        if ((_currTile2 == TileId.Gate || GetTile(_currRoom, --_tileCol, _tileRow) == TileId.Gate)
            && _tileCol is >= 0 and < 10
            && (_currRowCollFlags[_tileCol] & _prevCollFlags[_tileCol]) == 0xFF
            && CanBumpIntoGate())
        {
            _fx.SmackWall = true;
            _ch.X += origCol <= _tileCol ? -5 : 5;
        }
    }

    // ── distance to the next edge or wall (seg004 get_edge_distance) ─────────

    private const int EdgeCloser = 0, EdgeWall = 1, EdgeFloor = 2;
    private int _edgeType;

    /// <summary>
    /// get_edge_distance: how far the kid can step forward, and what stops him
    /// (<see cref="_edgeType"/>): a wall, the edge of the floor (or a loose floor /
    /// item to stop short of), or nothing within a full step (11).
    /// </summary>
    private int GetEdgeDistance()
    {
        DetermineCol();
        LoadFrameToObj();
        SetCharCollision();

        int distance;
        var tile = GetTileAtChar();
        if (WallType(tile) != 0)
        {
            _tileCol = _ch.Col;
            distance = DistFromWallForward(tile);
            if (distance >= 0) return WallOrFloor(distance, tile);
        }

        tile = GetTileInfrontofChar();
        if (tile == TileId.PanelWOF && _ch.FacingRight)
        {
            _edgeType = EdgeCloser;
            distance = DistanceToEdgeWeight();
        }
        else
        {
            if (WallType(tile) != 0)
            {
                _tileCol = _inFrontX;
                distance = DistFromWallForward(tile);
                if (distance >= 0) return WallOrFloor(distance, tile);
            }

            if (tile == TileId.Loose)
            {
                _edgeType = EdgeCloser;
                distance = DistanceToEdgeWeight();
            }
            else if (tile is TileId.PressPlate or TileId.Sword or TileId.Flask)
            {
                distance = DistanceToEdgeWeight();
                if (distance != 0) _edgeType = EdgeCloser;
                else { _edgeType = EdgeFloor; distance = 11; }
            }
            else if (TileIsFloor(tile))
            {
                _edgeType = EdgeFloor;
                distance = 11;
            }
            else
            {
                _edgeType = EdgeCloser;
                distance = DistanceToEdgeWeight();
            }
        }
        _currTile2 = tile;
        return distance;
    }

    private int WallOrFloor(int distance, TileId tile)
    {
        if (distance <= 13) _edgeType = EdgeWall;
        else { _edgeType = EdgeFloor; distance = 11; }
        _currTile2 = tile;
        return distance;
    }

    /// <summary>dist_from_wall_forward: -1 when there is no wall to walk into.</summary>
    private int DistFromWallForward(TileId tile)
    {
        if (tile == TileId.Gate && !CanBumpIntoGate()) return -1;
        _collTileLeftXpos = Coord.BlockEdge(_tileCol) + 7;
        int type = WallType(tile);
        if (type == 0) return -1;
        return !_ch.FacingRight
            ? _charXLeftColl - (_collTileLeftXpos + 13 - WallDistFromRight[type])
            : WallDistFromLeft[type] + _collTileLeftXpos - _charXRightColl;
    }
}
