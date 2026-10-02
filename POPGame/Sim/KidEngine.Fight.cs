using POPGame.Data;

namespace POPGame.Sim;

/// <summary>
/// Sword fighting and the guard: the sword controls (seg005 control_with_sword,
/// swordfight, parry ...), the guard's autocontrol (seg002 autocontrol_guard_*), being
/// hurt (hurt_by_sword, check_hurting), seeing the kid (seg003 check_can_guard_see_kid)
/// and the guard entering, leaving and following between rooms (seg002).
///
/// SDLPoP shares Char / Opp as globals that are loaded from Kid or Guard and saved back.
/// Here <c>_ch</c> / <c>_opp</c> point at the live characters, so nothing is copied and
/// every routine keeps its SDLPoP name and order.
/// </summary>
public sealed partial class KidEngine
{
    // Frames (SDLPoP frame_*).
    private const int FrameParry150 = 150, FrameStrike1 = 151, FrameStrike2 = 152, FrameStrike3 = 153,
                      FramePoking = 154, FrameWalkWithSword157 = 157, FrameStandWithSword158 = 158,
                      FrameParry161 = 161, FrameBlockToStrike = 162, FrameWalkWithSword165 = 165,
                      FrameStandInactive = 166, FrameBlocked = 167, FrameBack = 168,
                      FrameBeginBlock = 169, FrameStandWithSword170 = 170, FrameStandWithSword171 = 171;

    // Sequences (SDLPoP seq_*).
    private const int SeqDrawSword = 55, SeqGuardForwardWithSword = 56, SeqBackWithSword = 57,
                      SeqGuardStrike = 58, SeqTurnWithSword = 60, SeqParryAfterStrike = 61,
                      SeqParry = 62, SeqGuardActiveAfterFall = 63, SeqPushedBackWithSword = 64,
                      SeqBumpForwardWithSword = 65, SeqStrikeAfterParry = 66, SeqAttackWasParried = 69,
                      SeqHitBySword = 74, SeqKidStrike = 75, SeqGuardStandInactive = 77,
                      SeqStandFlipped = 80, SeqKidPushedOffLedge = 81, SeqGuardPushedOffLedge = 82,
                      SeqGuardFall = 83, SeqStabbedToDeath = 85, SeqKidForwardWithSword = 86,
                      SeqGuardBecomeInactive = 87, SeqTurnDrawSword = 89, SeqEnGarde = 90,
                      SeqPutSwordAway = 92, SeqPutSwordAwayFast = 93, SeqChomped = 54;

    /// <summary>The kid and the (one) guard of the room being played. Set by <see cref="Simulation"/>.</summary>
    public CharState Kid { get; set; } = null!;
    public CharState Guard { get; set; } = new() { Present = false, Alive = false, CharId = CharIds.Guard };

    /// <summary>The guard's own sequence effects (the kid's are the caller's).</summary>
    public SeqEffects GuardFx { get; } = new();

    /// <summary>Opp: the character Char is fighting, i.e. the other of kid and guard.</summary>
    private CharState _opp = null!;

    private SeqEffects _kidFx = null!;

    // ── globals (SDLPoP data.h) ───────────────────────────────────────────────

    /// <summary>can_guard_see_kid: 0 can't see, 1 sees but won't come, 2 sees and comes.</summary>
    public int CanGuardSeeKid { get; private set; }

    /// <summary>is_guard_notice: the kid made a noise the guard heard.</summary>
    private bool _isGuardNotice;

    /// <summary>justblocked: ticks since the guard's last strike was parried (it blocks more then).</summary>
    private int _justBlocked;

    /// <summary>kid_sword_strike: ticks since the kid last struck (a guard waits before advancing).</summary>
    private int _kidSwordStrike;

    /// <summary>guard_refrac: the guard's cooldown between moves.</summary>
    private int _guardRefrac;

    /// <summary>guard_skill: 0..11. The demo drives the kid with skill 10 / 11.</summary>
    private int _guardSkill;

    /// <summary>offguard: the kid has put his sword away (down) and only draws it again on purpose.</summary>
    private bool _offguard;

    /// <summary>holding_sword: the kid went for his sword when a guard came (decides the death music).</summary>
    private bool _holdingSword;

    /// <summary>droppedout: the kid dropped a level while fighting; the guard may follow him down.</summary>
    private bool _droppedOut;

    /// <summary>guard_notice_timer: level 13 only (Jaffar), counts down.</summary>
    private int _guardNoticeTimer;

    /// <summary>curr_guard_color: the guard's clothes, 0 = the file's own palette.</summary>
    public int GuardColor { get; private set; }

    /// <summary>Whether the demo's kid has killed the guard and now runs for the exit (checkpoint).</summary>
    private bool _demoCheckpoint;

    /// <summary>A noise the guard hears (a footstep, a bump, a landing).</summary>
    public void NoticeNoise() => _isGuardNotice = true;

    /// <summary>
    /// Points Char / Opp / the effects at a character and its opponent
    /// (loadkid_and_opp / loadshad_and_opp).
    /// </summary>
    private void Select(CharState ch, CharState opp)
    {
        _ch = ch;
        _opp = opp;
        _fx = ch == Kid ? _kidFx : GuardFx;
    }

    // ── seg006: distance between the two ──────────────────────────────────────

    /// <summary>
    /// char_opp_dist: &gt; 0 if Opp is in front of Char, &lt; 0 if behind; 999 in another
    /// room. Two facing each other are 13 apart when their x are equal.
    /// </summary>
    private int CharOppDist()
    {
        if (_ch.Room != _opp.Room) return 999;
        int distance = _opp.X - _ch.X;
        if (!_ch.FacingRight) distance = -distance;
        if (distance >= 0 && _ch.Face != _opp.Face) distance += 13;
        return distance;
    }

    private TileId GetTileInfrontof2Char() => GetTile(_ch.Room, _ch.Col + 2 * _ch.FaceSign, _ch.Row);

    private static bool Below(int value, int limit) => (ushort)value < (ushort)limit;

    // ── seg005: the sword controls ────────────────────────────────────────────

    /// <summary>draw_sword: the kid draws, a guard goes to "en garde" (the shadow draws like the kid).</summary>
    private void DrawSword()
    {
        int seq = SeqDrawSword;
        _controlForward = _controlShift2 = ReleaseArrows();
        if (_ch.CharId == CharIds.Kid) _offguard = false;
        else if (_ch.CharId != CharIds.Shadow) seq = SeqEnGarde;
        _ch.SwordDrawn = true;
        StartSeq(seq);
    }

    private static bool IsStandingWithSword(int frame) =>
        frame is FrameStandWithSword158 or FrameStandWithSword170 or FrameStandWithSword171;

    /// <summary>back_with_sword.</summary>
    private void BackWithSword()
    {
        if (!IsStandingWithSword(_ch.Frame)) return;
        _controlBackward = Ignore;
        StartSeq(SeqBackWithSword);
    }

    /// <summary>forward_with_sword.</summary>
    private void ForwardWithSword()
    {
        if (!IsStandingWithSword(_ch.Frame)) return;
        _controlForward = Ignore;
        StartSeq(_ch.CharId != CharIds.Kid ? SeqGuardForwardWithSword : SeqKidForwardWithSword);
    }

    /// <summary>control_with_sword.</summary>
    private void ControlWithSword()
    {
        if (_ch.Action >= CharAction.HangClimb) return;

        if (GetTileAtChar() == TileId.Loose || CanGuardSeeKid >= 2)
        {
            int distance = CharOppDist();
            if (Below(distance, 90))
            {
                Swordfight();
                return;
            }
            if (distance < 0)
            {
                if (Below(distance, unchecked((ushort)-4)))
                {
                    StartSeq(SeqTurnWithSword);         // turn with sword (after switching places)
                    return;
                }
                Swordfight();
                return;
            }
        }

        if (_ch.CharId == CharIds.Kid && _ch.Alive) _holdingSword = false;
        if (_ch.CharId < CharIds.Guard)
        {
            // Frame 171: the guard died; the kid puts his sword away.
            if (_ch.Frame == FrameStandWithSword171)
            {
                _ch.SwordDrawn = false;
                StartSeq(SeqPutSwordAway);
            }
        }
        else Swordfight();
    }

    /// <summary>swordfight.</summary>
    private void Swordfight()
    {
        int frame = _ch.Frame;
        int charid = _ch.CharId;
        if (frame == FrameParry161 && _controlShift2 >= Released)
        {
            StartSeq(SeqBackWithSword);                 // back with sword (when parrying)
            return;
        }
        if (_controlShift2 == Held)
        {
            if (charid == CharIds.Kid) _kidSwordStrike = 15;
            SwordStrike();
            if (_controlShift2 == Ignore) return;
        }

        if (_controlDown == Held)
        {
            if (IsStandingWithSword(frame))
            {
                _controlDown = Ignore;
                _ch.SwordDrawn = false;
                int seq;
                if (charid == CharIds.Kid)
                {
                    _offguard = true;
                    _guardRefrac = 9;
                    _holdingSword = false;
                    seq = SeqPutSwordAwayFast;
                }
                else if (charid == CharIds.Shadow) seq = SeqPutSwordAway;
                else seq = SeqGuardBecomeInactive;      // stand inactive (when the kid leaves sight)
                StartSeq(seq);
            }
        }
        else if (_controlUp == Held) Parry();
        else if (_controlForward == Held) ForwardWithSword();
        else if (_controlBackward == Held) BackWithSword();
    }

    /// <summary>sword_strike.</summary>
    private void SwordStrike()
    {
        int frame = _ch.Frame;
        int seq;
        if (frame is FrameWalkWithSword157 or FrameStandWithSword158 or FrameStandWithSword170
            or FrameStandWithSword171 or FrameWalkWithSword165)
        {
            seq = _ch.CharId == CharIds.Kid ? SeqKidStrike : SeqGuardStrike;
        }
        else if (frame is FrameParry150 or FrameParry161)
        {
            seq = SeqStrikeAfterParry;
        }
        else return;
        _controlShift2 = Ignore;
        StartSeq(seq);
    }

    /// <summary>parry.</summary>
    private void Parry()
    {
        int charFrame = _ch.Frame;
        int oppFrame = _opp.Frame;
        int charId = _ch.CharId;
        int seq = SeqParry;
        bool doPlaySeq = false;
        if (IsStandingWithSword(charFrame) || charFrame is FrameBack or FrameWalkWithSword165)
        {
            if (CharOppDist() >= 32 && charId != CharIds.Kid)
            {
                BackWithSword();
                return;
            }
            if (charId == CharIds.Kid)
            {
                if (oppFrame == FrameBack) return;
                if (oppFrame != FrameStrike1 && oppFrame != FrameStrike2 && oppFrame != FrameBlockToStrike)
                {
                    if (oppFrame == FrameStrike3) doPlaySeq = true;
                    // (the original's "else if not the kid: back_with_sword" can't happen here)
                }
            }
            else if (oppFrame != FrameStrike2) return;
        }
        else
        {
            if (charFrame != FrameBlocked) return;
            seq = SeqParryAfterStrike;
        }
        _controlUp = Ignore;
        StartSeq(seq);
        if (doPlaySeq) PlaySeq();
    }

    /// <summary>control_guard_inactive (seg006): a guard standing off guard draws on a down + forward.</summary>
    private void ControlGuardInactive()
    {
        if (_ch.Frame != FrameStandInactive || _controlDown != Held) return;
        if (_controlForward == Held)
        {
            DrawSword();
        }
        else
        {
            _controlDown = Ignore;
            StartSeq(SeqStandFlipped);
        }
    }

    // ── seg002: the guard's autocontrol ───────────────────────────────────────

    private void Move0Nothing()
    {
        _controlShift = false;
        _controlY = _controlX = 0;
        _controlShift2 = _controlDown = _controlUp = _controlBackward = _controlForward = Released;
    }

    private void Move1Forward() { _controlX = -1; _controlForward = Held; }
    private void Move2Backward() { _controlBackward = Held; _controlX = 1; }
    private void Move3Up() { _controlY = -1; _controlUp = Held; }
    private void Move4Down() { _controlDown = Held; _controlY = 1; }
    private void MoveDownBack() { _controlDown = Held; Move2Backward(); }
    private void MoveDownForw() { _controlDown = Held; Move1Forward(); }
    private void Move6Shift() { _controlShift = true; _controlShift2 = Held; }

    /// <summary>autocontrol_opponent: decides what a computer-driven character presses this frame.</summary>
    private void AutocontrolOpponent()
    {
        Move0Nothing();
        int charid = _ch.CharId;
        if (charid == CharIds.Kid)
        {
            AutocontrolGuard();                         // autocontrol_kid (the demo)
            return;
        }

        if (_justBlocked != 0) --_justBlocked;
        if (_kidSwordStrike != 0) --_kidSwordStrike;
        if (_guardRefrac != 0) --_guardRefrac;
        if (charid == CharIds.Mouse)
        {
            // autocontrol_mouse: not ported (the mouse isn't in the game yet)
        }
        else if (charid == CharIds.Skeleton)
        {
            _ch.SwordDrawn = true;                      // autocontrol_skeleton
            AutocontrolGuard();
        }
        else if (charid == CharIds.Shadow)
        {
            // autocontrol_shadow: the level 4/5/6/12 shadow scripts are not ported
        }
        else AutocontrolGuard();                        // (autocontrol_Jaffar is the same)
    }

    /// <summary>autocontrol_guard.</summary>
    private void AutocontrolGuard()
    {
        if (!_ch.SwordDrawn) AutocontrolGuardInactive();
        else AutocontrolGuardActive();
    }

    /// <summary>autocontrol_guard_inactive: notice the kid, turn to him, draw.</summary>
    private void AutocontrolGuardInactive()
    {
        if (!Kid.Alive) return;
        int distance = CharOppDist();
        if (_opp.Row != _ch.Row || Below(distance, unchecked((ushort)-8)))
        {
            // If the kid made a sound ...
            if (_isGuardNotice)
            {
                _isGuardNotice = false;
                if (distance < 0)
                {
                    // ... and he is behind the guard, the guard turns around.
                    if (Below(distance, unchecked((ushort)-4))) Move4Down();
                    return;
                }
            }
            else if (distance < 0)
            {
                return;
            }
        }
        if (CanGuardSeeKid != 0)
        {
            // If the guard can see the kid, he moves to a fighting pose.
            if (_levelNumber != 13 || _guardNoticeTimer == 0) MoveDownForw();
        }
    }

    /// <summary>autocontrol_guard_active.</summary>
    private void AutocontrolGuardActive()
    {
        int charFrame = _ch.Frame;
        if (charFrame == FrameStandInactive || charFrame < 150 || CanGuardSeeKid == 1) return;

        if (CanGuardSeeKid == 0)
        {
            if (_droppedOut) GuardFollowsKidDown();
            else if (_ch.CharId != CharIds.Skeleton) MoveDownBack();
            return;
        }

        // can_guard_see_kid == 2
        int oppFrame = _opp.Frame;
        int distance = CharOppDist();
        if (distance >= 12
            // frames 102..117: falling and landing
            && oppFrame >= 102 && oppFrame < 118
            && _opp.Action == CharAction.Bumped)
        {
            return;
        }

        if (distance < 35)
        {
            if ((!_ch.SwordDrawn && distance < 8) || distance < 12)
            {
                if (_ch.Face == _opp.Face) Move2Backward();     // turn around
                else Move1Forward();
            }
            else AutocontrolGuardKidInSight(distance);
            return;
        }

        if (_guardRefrac != 0) return;
        if (_ch.Face != _opp.Face)
        {
            // frames 7..14: running; frames 34..43: run-jump
            if (oppFrame >= 7 && oppFrame < 15)
            {
                if (distance < 40) Move6Shift();
                return;
            }
            if (oppFrame >= 34 && oppFrame < 44)
            {
                if (distance < 50) Move6Shift();
                return;
            }
        }
        AutocontrolGuardKidFar();
    }

    /// <summary>autocontrol_guard_kid_far: close in while there is floor ahead, else back off.</summary>
    private void AutocontrolGuardKidFar()
    {
        if (TileIsFloor(GetTileInfrontofChar()) || TileIsFloor(GetTileInfrontof2Char())) Move1Forward();
        else Move2Backward();
    }

    /// <summary>guard_follows_kid_down: follow a fallen kid down unless it would be suicide.</summary>
    private void GuardFollowsKidDown()
    {
        var oppAction = _opp.Action;
        if (oppAction is CharAction.HangClimb or CharAction.HangStraight) return;

        if (WallType(GetTileInfrontofChar()) != 0
            || (!TileIsFloor(_currTile2)
                && ((GetTile(_currRoom, _tileCol, ++_tileRow) == TileId.Spikes
                     || _currTile2 == TileId.Loose
                     || WallType(_currTile2) != 0
                     || !TileIsFloor(_currTile2))
                    || _ch.Row + 1 != _opp.Row)))
        {
            _droppedOut = false;                        // don't follow
            Move2Backward();
        }
        else Move1Forward();
    }

    /// <summary>autocontrol_guard_kid_in_sight.</summary>
    private void AutocontrolGuardKidInSight(int distance)
    {
        if (_opp.SwordDrawn)
        {
            AutocontrolGuardKidArmed(distance);
        }
        else if (_guardRefrac == 0)
        {
            if (distance < 29) Move6Shift();
            else Move1Forward();
        }
    }

    /// <summary>autocontrol_guard_kid_armed.</summary>
    private void AutocontrolGuardKidArmed(int distance)
    {
        if (distance < 10 || distance >= 29)
        {
            GuardAdvance();
            return;
        }
        GuardBlock();
        if (_guardRefrac != 0) return;
        if (distance < 12 || distance >= 29) GuardAdvance();
        else GuardStrike();
    }

    /// <summary>prandom(255): the game's random numbers, shared with <see cref="Hazards"/>.</summary>
    private int PRandom(int max) => _hazards.PRandom(max);

    /// <summary>guard_advance.</summary>
    private void GuardAdvance()
    {
        if (_guardSkill == 0 || _kidSwordStrike == 0)
        {
            if (_tables.AdvProb[_guardSkill] > PRandom(255)) Move1Forward();
        }
    }

    /// <summary>guard_block: parry the kid's strike (more surely right after a parried attack).</summary>
    private void GuardBlock()
    {
        int oppFrame = _opp.Frame;
        if (oppFrame is not (FrameStrike2 or FrameStrike3 or FrameBlockToStrike)) return;
        int prob = _justBlocked != 0 ? _tables.ImpBlockProb[_guardSkill] : _tables.BlockProb[_guardSkill];
        if (prob > PRandom(255)) Move3Up();
    }

    /// <summary>guard_strike.</summary>
    private void GuardStrike()
    {
        int oppFrame = _opp.Frame;
        if (oppFrame is FrameBeginBlock or FrameStrike1) return;
        int charFrame = _ch.Frame;
        int prob = charFrame is FrameParry161 or FrameParry150
            ? _tables.RestrikeProb[_guardSkill]
            : _tables.StrikeProb[_guardSkill];
        if (prob > PRandom(255)) Move6Shift();
    }

    // ── seg002: being hurt ────────────────────────────────────────────────────

    /// <summary>
    /// hurt_by_sword: a hit costs one hit point, or kills outright when the sword
    /// isn't drawn. A dying character is stabbed where he stands, or pushed off a
    /// ledge behind him.
    /// </summary>
    private void HurtBySword()
    {
        if (!_ch.Alive) return;
        if (!_ch.SwordDrawn)
        {
            // Being hurt when not in fighting pose means death.
            TakeHp(100);
            StabbedDeath();
        }
        else
        {
            bool dead = false;
            // You can't hurt skeletons.
            if (_ch.CharId != CharIds.Skeleton) dead = TakeHp(1);
            if (dead)
            {
                StabbedDeath();
            }
            else
            {
                StartSeq(SeqHitBySword);
                _ch.Y = Coord.FloorY(_ch.Row);
                _ch.FallY = 0;
            }
        }
        PlaySeq();
    }

    /// <summary>loc_4276 of hurt_by_sword.</summary>
    private void StabbedDeath()
    {
        int distance = 0;
        if (GetTileBehindChar() != TileId.Space || (distance = DistanceToEdgeWeight()) < 4)
        {
            StartSeq(SeqStabbedToDeath);
            if (_ch.CharId != CharIds.Kid && !_ch.FacingRight
                && (_currTile2 == TileId.Gate || GetTileAtChar() == TileId.Gate))
            {
                _ch.X = Coord.BlockEdge(_tileCol - (_currTile2 != TileId.Gate ? 1 : 0)) + 7;
                _ch.X = _ch.DxForward(10);
            }
            _ch.Y = Coord.FloorY(_ch.Row);
            _ch.FallY = 0;
        }
        else
        {
            _ch.X = _ch.DxForward(distance - 20);
            LoadFramDetCol();
            _ch.Row++;
            StartSeq(SeqKidPushedOffLedge);             // killed and pushed off the ledge
        }
    }

    /// <summary>check_sword_hurt: whoever check_hurting marked as hit is hurt now.</summary>
    public void CheckSwordHurt()
    {
        if (Guard.Action == CharAction.Hurt)
        {
            if (Kid.Action == CharAction.Hurt) Kid.Action = CharAction.RunJump;
            Select(Guard, Kid);
            HurtBySword();
            _guardRefrac = _tables.RefracTimer[_guardSkill];
        }
        else if (Kid.Action == CharAction.Hurt)
        {
            Select(Kid, Guard);
            HurtBySword();
        }
    }

    /// <summary>check_sword_hurting: each of the two may be striking the other.</summary>
    public void CheckSwordHurting()
    {
        int kidFrame = Kid.Frame;
        // frames 217..228: going up the stairs
        if (kidFrame == 0 || (kidFrame >= 219 && kidFrame < 229)) return;
        // (the original doesn't ask whether there is a guard; with none, a strike at the
        // empty room would leave the absent guard's action at "hurt" for good)
        if (!Guard.Present) return;
        Select(Guard, Kid);
        CheckHurting();
        Select(Kid, Guard);
        CheckHurting();
    }

    /// <summary>
    /// check_hurting: a strike (frame 153, then 154 pokes) that meets a parry is blocked;
    /// one that doesn't hurts the opponent if he is within reach.
    /// </summary>
    private void CheckHurting()
    {
        if (!_ch.SwordDrawn) return;
        if (_ch.Row != _opp.Row) return;
        int charFrame = _ch.Frame;
        if (charFrame != FrameStrike3 && charFrame != FramePoking) return;

        int distance = CharOppDist();
        int oppFrame = _opp.Frame;
        if (distance < 0 || distance >= 29 || (oppFrame != FrameParry161 && oppFrame != FrameParry150))
        {
            // The opponent is not parrying.
            if (_ch.Frame == FramePoking)
            {
                int minHurtRange = _opp.SwordDrawn ? 12 : 8;
                distance = CharOppDist();
                if (distance >= minHurtRange && distance < 29) _opp.Action = CharAction.Hurt;
            }
        }
        else
        {
            _opp.Frame = FrameParry161;
            if (_ch.CharId != CharIds.Kid) _justBlocked = 4;
            StartSeq(SeqAttackWasParried);
            PlaySeq();
        }
    }

    // ── seg003: bumping, seeing ───────────────────────────────────────────────

    /// <summary>
    /// bump_into_opponent: a kid without his sword who runs or falls into an armed guard
    /// facing him is stopped by him.
    /// </summary>
    private void BumpIntoOpponent()
    {
        if (CanGuardSeeKid >= 2
            && !_ch.SwordDrawn
            && _opp.SwordDrawn
            && _opp.Action < CharAction.HangClimb
            && _ch.Face != _opp.Face)
        {
            int distance = CharOppDist();
            if (Math.Abs(distance) <= 15)
            {
                _ch.Y = Coord.FloorY(_ch.Row);
                _ch.FallY = 0;
                StartSeq(Sim.Seq.Bump);
                PlaySeq();
            }
        }
    }

    /// <summary>get_tile_at_kid: the tile of the kid's row at an x.</summary>
    private TileId GetTileAtKid(int xpos) => GetTile(Kid.Room, Coord.ColM7(xpos), Kid.Row);

    /// <summary>
    /// check_can_guard_see_kid: 0 can't see him (a wall or doortop between, another row or
    /// room), 1 sees him but won't come (a gap, a chomper, a loose floor or a low gate
    /// between), 2 sees him and comes.
    /// </summary>
    public void CheckCanGuardSeeKid()
    {
        int kidFrame = Kid.Frame;
        if (Guard.CharId == CharIds.Mouse)
        {
            CanGuardSeeKid = 0;
            return;
        }
        if ((Guard.CharId != CharIds.Shadow || _levelNumber == 12)
            // frames 217..228: going up the stairs
            && kidFrame != 0 && (kidFrame < 219 || kidFrame >= 229)
            && Guard.Present && Kid.Alive && Guard.Alive
            && Kid.Room == Guard.Room && Kid.Row == Guard.Row)
        {
            CanGuardSeeKid = 2;
            int leftPos = Coord.BlockEdge(Kid.Col) + 7;
            int rightPos = Coord.BlockEdge(Guard.Col) + 7;
            if (leftPos > rightPos) (leftPos, rightPos) = (rightPos, leftPos);

            // A chomper is on the left side of a tile, so it doesn't count.
            if (GetTileAtKid(leftPos) == TileId.Slicer) leftPos += Coord.BlockWidth;
            // A gate is on the right side of a tile, so it doesn't count.
            if (GetTileAtKid(rightPos) == TileId.Gate) rightPos -= Coord.BlockWidth;

            if (rightPos < leftPos) return;
            while (leftPos <= rightPos)
            {
                // Can't see through these tiles.
                if (GetTileAtKid(leftPos) == TileId.Block
                    || _currTile2 is TileId.PanelWF or TileId.PanelWOF)
                {
                    CanGuardSeeKid = 0;
                    return;
                }
                // Can see through these, but won't go through them.
                if (_currTile2 == TileId.Loose
                    || _currTile2 == TileId.Slicer
                    || (_currTile2 == TileId.Gate && Modif() < 112)
                    || !TileIsFloor(_currTile2))
                {
                    CanGuardSeeKid = 1;
                }
                leftPos += Coord.BlockWidth;
            }
        }
        else
        {
            CanGuardSeeKid = 0;
        }
    }

    // ── the guard's frame (seg000 play_guard_frame, seg006 play_guard) ────────

    /// <summary>play_guard_frame.</summary>
    public void PlayGuardFrame()
    {
        if (!Guard.Present) return;
        Select(Guard, Kid);
        LoadFramDetCol();
        PlayGuard();
        if (_ch.Room != _hazards.DrawnRoom) return;

        PlaySeq();
        if (_ch.X is < 44 or >= 211) return;
        FallAccel();
        FallSpeed();
        LoadFrameToObj();
        LoadFramDetCol();
        SetCharCollision();
        CheckGuardBumped();
        CheckAction();
        _hazards.CheckPress(_ch, _frame);
        CheckSpikeBelow();
        CheckSpiked();
        CheckChompedGuard();
    }

    /// <summary>play_guard (seg006): out of hit points the guard dies, else the guard thinks and acts.</summary>
    private void PlayGuard()
    {
        if (_ch.CharId == CharIds.Mouse)
        {
            AutocontrolOpponent();
            return;
        }
        if (_ch.Alive)
        {
            if (_ch.Hp == 0)
            {
                _ch.Alive = false;
                OnGuardKilled();
            }
            else
            {
                AutocontrolOpponent();
                Control();
                return;
            }
        }
        if (_ch.CharId == CharIds.Shadow) ClearChar();
        AutocontrolOpponent();
        Control();
    }

    /// <summary>on_guard_killed (seg006): the demo's kid runs for the exit; Jaffar's death flashes.</summary>
    private void OnGuardKilled()
    {
        if (_levelNumber == 0)
        {
            _demoCheckpoint = true;
            _demoIndex = _demoTime = 0;
        }
        else if (_levelNumber == 13)
        {
            Flash(15, 18);                              // Jaffar's death: a white flash
            // (the exit door opens too: leveldoor_open = 2 — not ported yet)
        }
    }

    /// <summary>clear_char: the character is gone from the level.</summary>
    private void ClearChar()
    {
        _ch.Present = false;
        _ch.Alive = false;
        _ch.Action = CharAction.Stand;
        _ch.Hp = 0;
    }

    /// <summary>
    /// check_guard_bumped: a guard in a fight (run_jump action, sword drawn) pushed into a
    /// wall or a low gate stumbles back from it.
    /// </summary>
    private void CheckGuardBumped()
    {
        if (!(_ch.Action == CharAction.RunJump && _ch.Alive && _ch.SwordDrawn)) return;

        if (GetTileAtChar() == TileId.Block
            || _currTile2 == TileId.PanelWF
            || (_currTile2 == TileId.Gate && CanBumpIntoGate())
            || (_ch.FacingRight && (GetTile(_currRoom, --_tileCol, _tileRow) == TileId.PanelWF
                                    || (_currTile2 == TileId.Gate && CanBumpIntoGate()))))
        {
            LoadFrameToObj();
            SetCharCollision();
            if (IsObstacle())
            {
                int deltaX = DistFromWallBehind(_currTile2);
                if (deltaX < 0 && deltaX > -13)
                {
                    _ch.X = _ch.DxForward(-deltaX);
                    StartSeq(SeqBumpForwardWithSword);  // pushed to the wall with sword (guard)
                    PlaySeq();
                    LoadFramDetCol();
                }
            }
        }
    }

    /// <summary>dist_from_wall_behind.</summary>
    private int DistFromWallBehind(TileId tile)
    {
        int type = WallType(tile);
        if (type == 0) return 99;
        return _ch.FacingRight
            ? _charXLeftColl - (_collTileLeftXpos + 13 - WallDistFromRight[type])
            : WallDistFromLeft[type] + _collTileLeftXpos - _charXRightColl;
    }

    /// <summary>check_chomped_guard.</summary>
    private void CheckChompedGuard()
    {
        GetTileAtChar();
        if (!CheckChompedHere())
        {
            GetTile(_currRoom, ++_tileCol, _tileRow);
            CheckChompedHere();
        }
    }

    /// <summary>check_chomped_here: a shut chomper whose block overlaps the character.</summary>
    private bool CheckChompedHere()
    {
        if (_currTile2 != TileId.Slicer || (Modif() & 0x7F) != 2) return false;
        _collTileLeftXpos = Coord.BlockEdge(_tileCol) + 7;
        if (GetLeftWallXpos(_currRoom, _tileCol, _tileRow) < _charXRightColl
            && GetRightWallXpos(_currRoom, _tileCol, _tileRow) > _charXLeftColl)
        {
            Chomped();
            return true;
        }
        return false;
    }

    // ── guards entering and leaving rooms (seg002) ────────────────────────────

    /// <summary>
    /// check_shadow (seg002): the special shadows of levels 5, 6 and 12 take the place of
    /// a guard in their rooms (they are not ported yet, so the room stays empty); anywhere
    /// else the room's guard enters.
    /// </summary>
    public void CheckShadow()
    {
        _offguard = false;
        int drawn = _hazards.DrawnRoom;
        if (_levelNumber == 12 && UnitedWithShadow == 0 && drawn == 15) return;
        if (_levelNumber == 6 && drawn == 1) return;
        if (_levelNumber == 5 && drawn == 24) return;
        EnterGuard();
    }

    /// <summary>get_guard_hp.</summary>
    private int GuardHpForLevel() => _tables.ExtraStrength[_guardSkill] + _tables.GuardHp[Math.Clamp(_levelNumber, 0, 15)];

    /// <summary>
    /// enter_guard: the room's guard is placed where the level (or leave_guard) left it.
    /// The original builds him in the Char copy of the kid, so whatever the level data
    /// doesn't set is the kid's.
    /// </summary>
    public void EnterGuard()
    {
        int room = _hazards.DrawnRoom;
        if (room < 1 || room > Level.NumScreens) return;
        int r = room - 1;
        int guardTile = _level.GdStartBlock[r];
        if (guardTile >= 30) return;

        var g = Kid.Clone();
        g.Present = true;
        g.HpDelta = 0;
        g.Room = room;
        g.Row = guardTile / Coord.Cols;
        g.Y = Coord.FloorY(g.Row);
        g.X = _level.GdStartX[r];
        g.Col = Coord.ColM7(g.X);
        g.Face = (sbyte)_level.GdStartFace[r];

        // Only regular guards have different colours.
        GuardColor = _tables.GuardType[Math.Clamp(_levelNumber, 0, 15)] == 0 ? _level.GdStartColor[r] & 0x0F : 0;

        g.CharId = _tables.GuardType[Math.Clamp(_levelNumber, 0, 15)] == 2 ? CharIds.Skeleton : CharIds.Guard;
        byte seqHi = _level.GdStartSeqH[r];
        if (seqHi == 0)
        {
            if (g.CharId == CharIds.Skeleton)
            {
                g.SwordDrawn = true;
                _seq.Start(g, SeqGuardActiveAfterFall);
            }
            else
            {
                g.SwordDrawn = false;
                _seq.Start(g, SeqGuardStandInactive);
            }
        }
        else
        {
            g.SeqPtr = _level.GdStartSeqL[r] + (seqHi << 8);
        }

        Guard = g;
        Select(g, Kid);
        PlaySeq();
        _guardSkill = _level.GdStartProg[r];
        if (_guardSkill >= DosTablesGuardSkills) _guardSkill = 3;

        int frame = g.Frame;
        if (frame is 185 or 177 or 178)
        {
            g.Alive = false;
            g.Hp = 0;
        }
        else
        {
            g.Alive = true;
            _justBlocked = 0;
            _guardRefrac = 0;
            _isGuardNotice = false;
            g.MaxHp = g.Hp = GuardHpForLevel();
        }
        g.FallY = 0;
        g.FallX = 0;
        g.Action = CharAction.RunJump;
    }

    private const int DosTablesGuardSkills = Dos.DosTables.GuardSkills;

    /// <summary>leave_guard: the guard is put away in the room he is in (a dead one keeps his pose).</summary>
    private void LeaveGuard()
    {
        if (!Guard.Present || Guard.CharId is CharIds.Shadow or CharIds.Mouse) return;
        int r = Guard.Room - 1;
        if (r < 0 || r >= Level.NumScreens) { Guard.Present = false; return; }

        _level.GdStartBlock[r] = (byte)GetTilepos(0, Guard.Row);
        _level.GdStartColor[r] = (byte)(GuardColor & 0x0F);
        _level.GdStartX[r] = (byte)Guard.X;
        _level.GdStartFace[r] = (byte)Guard.Face;
        _level.GdStartProg[r] = (byte)_guardSkill;
        if (Guard.Alive)
        {
            _level.GdStartSeqH[r] = 0;
        }
        else
        {
            _level.GdStartSeqL[r] = (byte)Guard.SeqPtr;
            _level.GdStartSeqH[r] = (byte)(Guard.SeqPtr >> 8);
        }
        Guard.Present = false;
        Guard.Hp = 0;
    }

    /// <summary>
    /// The guard's half of exit_room: when the kid has left through <paramref name="dir"/>
    /// (0 left, 1 right, 2 up, 3 down), an armed guard who can still reach him follows him
    /// into the next room; otherwise the guard stays behind (leave_guard).
    /// </summary>
    private void ExitRoomGuard(int dir)
    {
        if (!Guard.Present) return;
        bool leave;
        if (Guard.Alive && Guard.SwordDrawn)
        {
            int kidRoom = Kid.Room - 1;
            if (kidRoom >= 0 && kidRoom <= 23
                && (_level.GdStartBlock[kidRoom] >= 30 || _level.GdStartSeqH[kidRoom] != 0))
            {
                leave = dir switch
                {
                    0 => Guard.X >= 91,
                    1 => Guard.X < 165,
                    2 => Guard.Row >= 0,
                    _ => Guard.Row < 3,
                };
            }
            else leave = true;
        }
        else leave = true;

        if (leave)
        {
            LeaveGuard();
        }
        else
        {
            // follow_guard
            _level.GdStartBlock[Kid.Room - 1] = 0xFF;
            _level.GdStartBlock[Guard.Room - 1] = 0xFF;
            _ch = Guard;
            GotoOtherRoom(dir);
        }
    }

    /// <summary>
    /// check_guard_fallout: a guard that has fallen out of the bottom of the room is gone
    /// for good (a skeleton reappears in room 3 of level 3).
    /// </summary>
    public void CheckGuardFallout()
    {
        if (!Guard.Present || (byte)Guard.Y < 211) return;
        if (Guard.CharId == CharIds.Shadow)
        {
            if (Guard.Action != CharAction.InFreefall) return;
            ClearGuard();
        }
        else if (Guard.CharId == CharIds.Skeleton && _level.Below(Guard.Room) == 3)
        {
            Guard.Room = _level.Below(Guard.Room);
            Guard.X = 133;
            Guard.Row = 1;
            Guard.Face = 0;
            Guard.Alive = true;
            LeaveGuard();
        }
        else
        {
            Select(Guard, Kid);
            OnGuardKilled();
            if (_hazards.DrawnRoom is >= 1 and <= Level.NumScreens)
                _level.GdStartBlock[_hazards.DrawnRoom - 1] = 0xFF;
            ClearGuard();
        }
    }

    private void ClearGuard()
    {
        Guard.Present = false;
        Guard.Alive = false;
        Guard.Hp = 0;
    }

    /// <summary>Resets the fight state when a level starts.</summary>
    private void ResetFight()
    {
        CanGuardSeeKid = 0;
        _isGuardNotice = false;
        _justBlocked = _kidSwordStrike = _guardRefrac = 0;
        _offguard = _holdingSword = _droppedOut = false;
        _guardNoticeTimer = 0;
        _demoCheckpoint = false;
        Guard = new CharState { Present = false, Alive = false, CharId = CharIds.Guard, Hp = 0, MaxHp = 0 };
    }
}
