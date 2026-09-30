using POPGame.Data;
using POPGame.Input;

namespace POPGame.Sim;

public sealed partial class KidEngine
{
    // Latched controls (SDLPoP control_forward/backward/up/down/shift2). A press is
    // Held until something consumes it, which sets it to Ignore so a key that stays
    // down doesn't repeat the move; releasing the key makes it Released again.
    private const int Held = -1, Released = 0, Ignore = 1;

    // Raw controls this frame: x -1 = left / forward, +1 = right / back; y -1 = up.
    private int _controlX, _controlY;
    private bool _controlShift;
    private int _controlForward, _controlBackward, _controlUp, _controlDown, _controlShift2;
    private int _ctrl1Forward, _ctrl1Backward, _ctrl1Up, _ctrl1Down, _ctrl1Shift2;

    private void ResetControls()
    {
        _controlForward = _controlBackward = _controlUp = _controlDown = _controlShift2 = Released;
        _ctrl1Forward = _ctrl1Backward = _ctrl1Up = _ctrl1Down = _ctrl1Shift2 = Released;
    }

    /// <summary>control_kid (seg006).</summary>
    private void ControlKid(InputState input)
    {
        if (_grabTimer != 0) --_grabTimer;

        if (DemoMode)
        {
            DoAutoMoves();
            Control();
            return;
        }

        // rest_ctrl_1
        _controlForward = _ctrl1Forward;
        _controlBackward = _ctrl1Backward;
        _controlUp = _ctrl1Up;
        _controlDown = _ctrl1Down;
        _controlShift2 = _ctrl1Shift2;

        ReadKeybControl(input);
        ReadUserControl();
        UserControl();

        // save_ctrl_1
        _ctrl1Forward = _controlForward;
        _ctrl1Backward = _controlBackward;
        _ctrl1Up = _controlUp;
        _ctrl1Down = _controlDown;
        _ctrl1Shift2 = _controlShift2;
    }

    // ── the attract-mode demo (level 0) ───────────────────────────────────────

    /// <summary>
    /// Level 0's demo: the kid is driven by a fixed move table instead of the keyboard
    /// (do_demo / do_auto_moves). It is deterministic, which makes it a ground truth
    /// to compare against a DOSBox recording of the original's attract mode.
    /// </summary>
    public bool DemoMode { get; set; }

    // custom->demo_moves: (time, move). Move -1 ends the table.
    private static readonly (int Time, int Move)[] DemoMoves =
    [
        (0x00, 0), (0x01, 1), (0x0D, 0), (0x1E, 1), (0x25, 5), (0x2F, 0), (0x30, 1), (0x41, 0),
        (0x49, 2), (0x4B, 0), (0x63, 2), (0x64, 0), (0x73, 5), (0x80, 6), (0x88, 3), (0x9D, 7),
        (0x9E, 0), (0x9F, 1), (0xAB, 4), (0xB1, 0), (0xB2, 1), (0xBC, 0), (0xC1, 1), (0xCD, 0),
        (0xE9, -1),
    ];

    private int _demoTime, _demoIndex;

    /// <summary>do_auto_moves (seg002). (The sword fight that follows is not ported.)</summary>
    private void DoAutoMoves()
    {
        if (_demoTime >= 0xFE) return;
        ++_demoTime;
        int index = _demoIndex;
        if (DemoMoves[index].Time <= _demoTime) ++_demoIndex;
        else index = _demoIndex - 1;

        switch (DemoMoves[index].Move)
        {
            case 0:
                _controlShift = false;
                _controlY = _controlX = 0;
                _controlShift2 = _controlDown = _controlUp = _controlBackward = _controlForward = Released;
                break;
            case 1: _controlX = -1; _controlForward = Held; break;
            case 2: _controlBackward = Held; _controlX = 1; break;
            case 3: _controlY = -1; _controlUp = Held; break;
            case 4: _controlDown = Held; _controlY = 1; break;
            case 5: _controlY = -1; _controlUp = Held; _controlX = -1; _controlForward = Held; break;
            case 6: _controlShift = true; _controlShift2 = Held; break;
            case 7: _controlShift = false; break;
        }
    }

    /// <summary>read_keyb_control (seg000): left wins over right, up over down.</summary>
    private void ReadKeybControl(InputState input)
    {
        _controlY = input.Up ? -1 : input.Down ? 1 : 0;
        _controlX = input.Left ? -1 : input.Right ? 1 : 0;
        _controlShift = input.Action;
    }

    /// <summary>
    /// read_user_control (seg006). Runs before the facing flip, so here "forward" is
    /// still "left"; <see cref="UserControl"/> swaps them for a kid facing right.
    /// </summary>
    private void ReadUserControl()
    {
        _controlForward = Latch(_controlForward, _controlX == -1);
        _controlBackward = Latch(_controlBackward, _controlX == 1);
        _controlUp = Latch(_controlUp, _controlY == -1);
        _controlDown = Latch(_controlDown, _controlY == 1);
        _controlShift2 = Latch(_controlShift2, _controlShift);
    }

    private static int Latch(int state, bool down)
    {
        if (state < Released) return state;         // still held, not yet consumed
        if (!down) return Released;
        return state == Released ? Held : state;    // a fresh press, or still ignored
    }

    /// <summary>user_control: controls are facing-relative.</summary>
    private void UserControl()
    {
        if (_ch.FacingRight)
        {
            FlipControlX();
            Control();
            FlipControlX();
        }
        else Control();
    }

    private void FlipControlX()
    {
        _controlX = -_controlX;
        (_controlForward, _controlBackward) = (_controlBackward, _controlForward);
    }

    /// <summary>release_arrows.</summary>
    private int ReleaseArrows()
    {
        _controlBackward = _controlForward = _controlUp = _controlDown = Released;
        return Ignore;
    }

    private void StartSeq(int id) => _seq.Start(_ch, id);

    /// <summary>control (seg005): what the kid is doing decides which keys matter.</summary>
    private void Control()
    {
        int frame = _ch.Frame;
        if (!_ch.Alive)
        {
            if (frame is 15 or 166 or 158 or 171) StartSeq(Sim.Seq.DropDead);
            return;
        }

        if (_ch.Action is CharAction.Bumped or CharAction.InFreefall) ReleaseArrows();
        else if (frame == 15 || frame is >= 50 and < 53) ControlStanding();
        else if (frame == 48) ControlTurning();
        else if (frame < 4) ControlStartrun();
        else if (frame is >= 67 and < 70) ControlJumpup();
        else if (frame < 15) ControlRunning();
        else if (frame is >= 87 and < 100) ControlHanging();
        else if (frame == 109) ControlCrouched();
    }

    /// <summary>control_crouched. (Picking up items is not ported.)</summary>
    private void ControlCrouched()
    {
        if (_controlY != 1)
        {
            StartSeq(Sim.Seq.StandUp);
        }
        else if (_controlForward == Held)
        {
            _controlForward = Ignore;
            StartSeq(Sim.Seq.Crawl);                 // crouch-hop
        }
    }

    /// <summary>control_standing. (Swords and picking up items are not ported.)</summary>
    private void ControlStanding()
    {
        if (_controlShift)
        {
            if (_controlBackward == Held) BackPressed();
            else if (_controlUp == Held) UpPressed();
            else if (_controlDown == Held) DownPressed();
            else if (_controlX == -1 && _controlForward == Held) SafeStep();
        }
        else if (_controlForward == Held)
        {
            if (_controlUp == Held) StandingJump();     // keyboard mode
            else ForwardPressed();
        }
        else if (_controlBackward == Held) BackPressed();
        else if (_controlUp == Held)
        {
            if (_controlForward == Held) StandingJump();
            else UpPressed();
        }
        else if (_controlDown == Held) DownPressed();
        else if (_controlX == -1) ForwardPressed();
    }

    /// <summary>up_pressed: climb an open exit door, else jump.</summary>
    private void UpPressed()
    {
        bool door = GetTileAtChar() == TileId.Exit
                    || GetTileBehindChar() == TileId.Exit
                    || GetTileInfrontofChar() == TileId.Exit;
        if (door && StartRoom != _ch.Room && _levelDoorOpen())
        {
            GoUpLeveldoor();
            return;
        }

        if (_controlX == -1) StandingJump();
        else CheckJumpUp();
    }

    /// <summary>go_up_leveldoor.</summary>
    private void GoUpLeveldoor()
    {
        _ch.X = Coord.BlockEdge(_tileCol) + 10;
        _ch.Face = -1;
        StartSeq(Sim.Seq.ClimbStairs);
    }

    /// <summary>
    /// down_pressed: at the edge in front, step off it; with the edge behind, climb down
    /// and hang from it; otherwise crouch.
    /// </summary>
    private void DownPressed()
    {
        _controlDown = Ignore;
        if (!TileIsFloor(GetTileInfrontofChar()) && DistanceToEdgeWeight() < 3)
        {
            _ch.X = _ch.DxForward(5);
            LoadFramDetCol();
            return;
        }

        if (!TileIsFloor(GetTileBehindChar()) && DistanceToEdgeWeight() >= 8)
        {
            _throughTile = GetTileBehindChar();
            GetTileAtChar();
            if (CanGrab()
                && (_ch.FacingRight || GetTileAtChar() != TileId.Gate || Modif() >= 6))
            {
                _ch.X = _ch.DxForward(DistanceToEdgeWeight() - 9);
                StartSeq(Sim.Seq.ClimbDown);
                return;
            }
        }
        Crouch();
    }

    /// <summary>control_turning: still holding forward after a turn starts a run.</summary>
    private void ControlTurning()
    {
        if (!_controlShift && _controlX == -1 && _controlY >= 0)
            StartSeq(Sim.Seq.TurnRun);
    }

    /// <summary>crouch.</summary>
    private void Crouch()
    {
        StartSeq(Sim.Seq.Stoop);
        _controlDown = ReleaseArrows();
    }

    /// <summary>back_pressed.</summary>
    private void BackPressed()
    {
        _controlBackward = ReleaseArrows();
        StartSeq(Sim.Seq.Turn);
    }

    /// <summary>forward_pressed: near a wall, take a careful step instead of running.</summary>
    private void ForwardPressed()
    {
        int distance = GetEdgeDistance();
        if (_edgeType == EdgeWall && _currTile2 != TileId.Slicer && distance < 8)
        {
            if (_controlForward == Held) SafeStep();
        }
        else StartSeq(Sim.Seq.StartRun);
    }

    /// <summary>control_running.</summary>
    private void ControlRunning()
    {
        if (_controlX == 0 && _ch.Frame is 7 or 11)
        {
            _controlForward = ReleaseArrows();
            StartSeq(Sim.Seq.RunStop);
        }
        else if (_controlX == 1)
        {
            _controlBackward = ReleaseArrows();
            StartSeq(Sim.Seq.RunTurn);
        }
        else if (_controlY == -1 && _controlUp == Held)
        {
            RunJump();
        }
        else if (_controlDown == Held)
        {
            _controlDown = Ignore;
            StartSeq(Sim.Seq.RDiveRoll);             // crouch while running
        }
    }

    /// <summary>
    /// safe_step: a careful step exactly up to the edge or wall ahead (stepfwd 1..14 are
    /// sequences 29..42). At the edge itself the first press tests with the foot and
    /// the second steps off.
    /// </summary>
    private void SafeStep()
    {
        _controlShift2 = Ignore;
        _controlForward = Ignore;
        int distance = GetEdgeDistance();
        if (distance != 0)
        {
            _ch.Repeat = 1;
            StartSeq(distance + 28);
        }
        else if (_edgeType != EdgeWall && _ch.Repeat != 0)
        {
            _ch.Repeat = 0;
            StartSeq(Sim.Seq.TestFoot);
        }
        else
        {
            StartSeq(Sim.Seq.StepFwd1 + 10);         // seq 39: full step, off the ledge
        }
    }

    /// <summary>control_startrun.</summary>
    private void ControlStartrun()
    {
        if (_controlY == -1 && _controlX == -1) StandingJump();
    }

    /// <summary>control_jumpup.</summary>
    private void ControlJumpup()
    {
        if (_controlX == -1 || _controlForward == Held) StandingJump();
    }

    /// <summary>standing_jump.</summary>
    private void StandingJump()
    {
        _controlUp = _controlForward = Ignore;
        StartSeq(Sim.Seq.StandJump);
    }

    /// <summary>
    /// check_jump_up: grab the ledge above and in front; failing that, step back under
    /// the ledge above (if there is one) and grab it; otherwise just jump up.
    /// </summary>
    private void CheckJumpUp()
    {
        _controlUp = ReleaseArrows();
        _throughTile = GetTileAboveChar();
        GetTileFrontAboveChar();
        if (CanGrab())
        {
            GrabUpWithFloorBehind();
            return;
        }

        _throughTile = GetTileBehindAboveChar();
        GetTileAboveChar();
        if (CanGrab()) JumpUpOrGrab();
        else JumpUp();
    }

    /// <summary>jump_up_or_grab.</summary>
    private void JumpUpOrGrab()
    {
        int distance = DistanceToEdgeWeight();
        if (distance < 6)
        {
            JumpUp();
        }
        else if (!TileIsFloor(GetTileBehindChar()))
        {
            GrabUpNoFloorBehind();
        }
        else
        {
            _ch.X = _ch.DxForward(distance - 14);
            LoadFramDetCol();
            GrabUpWithFloorBehind();
        }
    }

    /// <summary>grab_up_no_floor_behind.</summary>
    private void GrabUpNoFloorBehind()
    {
        GetTileAboveChar();
        _ch.X = _ch.DxForward(DistanceToEdgeWeight() - 10);
        StartSeq(Sim.Seq.JumpBackHang);
    }

    /// <summary>
    /// jump_up: jump straight up, touching the ceiling if there is one over the kid's
    /// hand (6 units behind his weight).
    /// </summary>
    private void JumpUp()
    {
        _controlUp = ReleaseArrows();
        int distance = GetEdgeDistance();
        if (distance < 4 && _edgeType == EdgeWall)
            _ch.X = _ch.DxForward(distance - 3);

        GetTile(_ch.Room, Coord.BlockX(BackDeltaX(0) + DxWeight() - 6), _ch.Row - 1);
        if (_currTile2 != TileId.Block && !TileIsFloor(_currTile2))
            StartSeq(Sim.Seq.HighJump);              // nothing above
        else
            StartSeq(Sim.Seq.JumpUp);                // touch the ceiling
    }

    /// <summary>control_hanging.</summary>
    private void ControlHanging()
    {
        if (!_ch.Alive)
        {
            HangFall();
            return;
        }

        if (_grabTimer == 0 && _controlY == -1)
        {
            CanClimbUp();
        }
        else if (_controlShift)
        {
            if (_ch.Action != CharAction.HangStraight
                && (GetTileAtChar() == TileId.Block
                    || (!_ch.FacingRight && _currTile2 is TileId.PanelWF or TileId.PanelWOF)))
            {
                StartSeq(Sim.Seq.HangStraight);      // hang against the wall
            }
            else if (!TileIsFloor(GetTileAboveChar()))
            {
                HangFall();                     // the ledge fell away
            }
        }
        else HangFall();
    }

    /// <summary>can_climb_up: a mirror, chomper or low gate blocks the climb.</summary>
    private void CanClimbUp()
    {
        int seq = Sim.Seq.ClimbUp;
        _controlUp = _controlShift2 = ReleaseArrows();
        GetTileAboveChar();
        if ((_currTile2 is TileId.Mirror or TileId.Slicer && _ch.FacingRight)
            || (_currTile2 == TileId.Gate && !_ch.FacingRight && Modif() < 6))
        {
            seq = Sim.Seq.ClimbFail;
        }
        StartSeq(seq);
    }

    /// <summary>hang_fall: let go, dropping to the floor below or back onto this one.</summary>
    private void HangFall()
    {
        _controlDown = ReleaseArrows();
        if (!TileIsFloor(GetTileBehindChar()) && !TileIsFloor(GetTileAtChar()))
        {
            StartSeq(Sim.Seq.HangFall);
            return;
        }

        if (GetTileAtChar() == TileId.Block
            || (!_ch.FacingRight && _currTile2 is TileId.PanelWF or TileId.PanelWOF))
        {
            _ch.X = _ch.DxForward(-7);
        }
        StartSeq(Sim.Seq.HangDrop);
    }

    /// <summary>
    /// grab_up_with_floor_behind: pick the jumphang sequence that lands the hands on
    /// the ledge, shifting x so it comes out exactly.
    /// </summary>
    private void GrabUpWithFloorBehind()
    {
        int distance = DistanceToEdgeWeight();
        int edgeDistance = GetEdgeDistance();
        if (distance < 4 && edgeDistance < 4 && _edgeType != EdgeWall)
        {
            _ch.X = _ch.DxForward(distance);
            StartSeq(Sim.Seq.JumpHangMed);
        }
        else
        {
            _ch.X = _ch.DxForward(distance - 4);
            StartSeq(Sim.Seq.JumpHangLong);
        }
    }

    /// <summary>
    /// run_jump: align the take-off with the edge of the floor ahead (up to two tiles
    /// ahead). Too far from the edge, keep running and try again next frame.
    /// </summary>
    private void RunJump()
    {
        if (_ch.Frame < 7) return;

        int xpos = _ch.DxForward(4);
        int col = Coord.ColM7(xpos);
        for (int tilesForward = 0; tilesForward < 2; tilesForward++)
        {
            col += _ch.FaceSign;
            GetTile(_ch.Room, col, _ch.Row);
            if (_currTile2 == TileId.Spikes || !TileIsFloor(_currTile2))
            {
                int adjust = DistanceToEdge(xpos) + 14 * tilesForward - 14;
                if ((ushort)adjust < unchecked((ushort)-8) || adjust >= 2)
                {
                    if (adjust < 128) return;
                    adjust = -3;
                }
                _ch.X = _ch.DxForward(adjust + 4);
                break;
            }
        }
        _controlUp = ReleaseArrows();
        StartSeq(Sim.Seq.RunJump);
    }
}
