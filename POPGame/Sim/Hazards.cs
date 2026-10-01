using POPGame.Data;
using POPGame.Dos;

namespace POPGame.Sim;

/// <summary>
/// The level's moving parts, ported from SDLPoP seg007: animated tiles ("trobs":
/// pressure plates, gates, the exit door, shaking loose floors) and the falling
/// loose-floor pieces ("mobs"), plus check_press (seg006) and check_knock (seg003).
///
/// Modifiers (BLUESPEC) are in the original's units, as load_alter_mod leaves them:
/// a gate's is its height 0..188 (0xFF = held open for good), the exit door's rises
/// to 43, a loose floor's counts up to its fall. The routines keep their SDLPoP names
/// and its shared globals (<c>trob</c>, <c>curmob</c>, <c>curr_room</c>,
/// <c>curr_tilepos</c>, <c>curr_modifier</c>), so each one can be checked against
/// the source. Sounds, redraw bookkeeping and the torch/potion/sword animations
/// (cosmetic, drawn from the tick) are left out. Chompers run here too
/// (start_chompers / animate_chomper); what they do to the kid is check_chomped_kid
/// in the kid engine.
/// </summary>
public sealed class Hazards
{
    private readonly Level _level;
    private readonly Links _links;
    private readonly int _levelNumber;

    /// <summary>
    /// fell_on_your_head (seg007): a falling piece has hit the kid. The kid engine
    /// supplies it, since it plays his sequences.
    /// </summary>
    public Action? LooseFellOnKid { get; set; }

    /// <summary>The kid, for check_loose_fall_on_kid and check_press.</summary>
    public CharState? Kid { get; set; }

    public Hazards(Level level, int levelNumber)
    {
        _level = level;
        _links = new Links(level);
        _levelNumber = levelNumber;
    }

    /// <summary>
    /// leveldoor_open: 0 shut, 1 once an opener has raised the exit door all the way
    /// (animate_leveldoor), 2 on Jaffar's death. The kid leaves the level by pressing
    /// Up at an open door.
    /// </summary>
    public int LeveldoorOpen { get; set; }

    public bool ExitOpen => LeveldoorOpen != 0;

    /// <summary>drawn_room: the room on screen (the kid's), which chompers keep running in.</summary>
    public int DrawnRoom { get; set; }

    // ── prandom (seg009) ──────────────────────────────────────────────────────

    private uint _randomSeed;

    /// <summary>
    /// random_seed. The original seeds it from the clock; the headless runner fixes it
    /// so dumps repeat. (The room drawer's masonry PRNG is separate: the original saves
    /// and restores the seed around it.)
    /// </summary>
    public void SeedRandom(uint seed) => _randomSeed = seed;

    /// <summary>prandom: Microsoft C's LCG, top 16 bits, modulo max+1.</summary>
    private int PRandom(int max)
    {
        _randomSeed = _randomSeed * 214013 + 2531011;
        return (int)((_randomSeed >> 16) % (uint)(max + 1));
    }

    // ── entering a room (seg000 check_the_end) ────────────────────────────────

    private const int TorchWithDebris = 30;

    /// <summary>
    /// anim_tile_modif (seg000): on entering a room its potions, torches and sword start
    /// animating from a random frame, and so do the torches in the rightmost column of
    /// the room to the left (they show in this one).
    /// </summary>
    public void AnimTileModif()
    {
        if (!ValidRoom(DrawnRoom)) return;
        _currRoom = DrawnRoom;
        for (int tilepos = 0; tilepos < Level.CellsPerScreen; ++tilepos)
        {
            switch (GetCurrTile(tilepos))
            {
                case (int)TileId.Flask: StartAnimPotion(DrawnRoom, tilepos); break;
                case (int)TileId.Torch:
                case TorchWithDebris: StartAnimTorch(DrawnRoom, tilepos); break;
                case (int)TileId.Sword: StartAnimSword(DrawnRoom, tilepos); break;
            }
        }

        int roomL = _level.Left(DrawnRoom);
        for (int row = 0; row <= 2; row++)
        {
            int tile = GetTile(roomL, 9, row);
            if (tile is (int)TileId.Torch or TorchWithDebris && ValidRoom(_currRoom))
                StartAnimTorch(roomL, row * Coord.Cols + 9);
        }
    }

    /// <summary>
    /// check_fall_flo (seg000): on level 13, entering room 23 or 16 sets the floor of the
    /// room above (tiles 22..27) falling, each after a random delay.
    /// </summary>
    public void CheckFallFlo()
    {
        if (_levelNumber != LooseTilesLevel || DrawnRoom is not (23 or 16)) return;
        _currRoom = _level.Above(DrawnRoom);
        if (!ValidRoom(_currRoom)) return;
        for (_currTilepos = 22; _currTilepos <= 27; ++_currTilepos)
            MakeLooseFall((byte)-(PRandom(0xFF) & 0x0F));
    }

    /// <summary>start_anim_torch.</summary>
    private void StartAnimTorch(int room, int tilepos)
    {
        SetMod(room, tilepos, (byte)PRandom(8));
        AddTrob(room, tilepos, 1);
    }

    /// <summary>start_anim_potion: bubbles from a random frame 1..7; the type stays in bits 3-7.</summary>
    private void StartAnimPotion(int room, int tilepos)
    {
        SetMod(room, tilepos, (byte)((ModAt(room, tilepos) & 0xF8) | (PRandom(6) + 1)));
        AddTrob(room, tilepos, 1);
    }

    /// <summary>start_anim_sword: a random wait before its first glint.</summary>
    private void StartAnimSword(int room, int tilepos)
    {
        SetMod(room, tilepos, (byte)(PRandom(0xFF) & 0x1F));
        AddTrob(room, tilepos, 1);
    }

    /// <summary>is_trob_in_drawn_room: a trob outside the drawn room stops.</summary>
    private bool IsTrobInDrawnRoom()
    {
        if (_trob.Room == DrawnRoom) return true;
        _trob.Type = -1;
        return false;
    }

    /// <summary>
    /// animate_torch: a random flame frame (get_torch_frame), while the torch is in the
    /// drawn room or in the rightmost column of the room to its left.
    /// </summary>
    private void AnimateTorch()
    {
        if (_trob.Room == DrawnRoom || (_trob.Room == _level.Left(DrawnRoom) && _trob.Tilepos % 10 == 9))
            _currModifier = (byte)GetTorchFrame(_currModifier);
        else
            _trob.Type = -1;
    }

    /// <summary>get_torch_frame: mostly a random frame 0..8, else the next one.</summary>
    private int GetTorchFrame(int curr)
    {
        int next = PRandom(255);
        if (next != curr)
        {
            if (next < 9) return next;
            next = curr;
        }
        ++next;
        if (next >= 9) next = 0;
        return next;
    }

    /// <summary>animate_potion: the bubble steps through frames 1..7 (bubble_next_frame).</summary>
    private void AnimatePotion()
    {
        if (_trob.Type < 0 || !IsTrobInDrawnRoom()) return;
        int type = _currModifier & 0xF8;
        int next = (_currModifier & 0x07) + 1;
        if (next >= 8) next = 1;
        _currModifier = (byte)(next | type);
    }

    /// <summary>
    /// animate_sword: counts down to its glint (drawn on modifier 1), then waits a
    /// random 40..103 ticks for the next.
    /// </summary>
    private void AnimateSword()
    {
        if (!IsTrobInDrawnRoom()) return;
        --_currModifier;
        if (_currModifier == 0) _currModifier = (byte)((PRandom(255) & 0x3F) + 0x28);
    }

    // Level data these routines tune (SDLPoP's custom-> values for the DOS game).
    private const int LooseFloorDelay = 11;
    private const int LooseTilesLevel = 13;

    // ── room addressing (get_room_address / get_tile / get_curr_tile) ─────────

    private int _currRoom, _currTilepos;
    private int _currTile;
    private byte _currModifier;

    private static int Index(int room, int tilepos) => (room - 1) * Level.CellsPerScreen + tilepos;

    private int TileAt(int room, int tilepos) => _level.LiveBlueType[Index(room, tilepos)] & 0x1F;
    private void SetTile(int room, int tilepos, int tile) => _level.LiveBlueType[Index(room, tilepos)] = (byte)tile;
    private byte ModAt(int room, int tilepos) => _level.LiveBlueSpec[Index(room, tilepos)];
    private void SetMod(int room, int tilepos, byte value) => _level.LiveBlueSpec[Index(room, tilepos)] = value;

    private static bool ValidRoom(int room) => room is >= 1 and <= Level.NumScreens;

    /// <summary>get_curr_tile: the tile at curr_room/tilepos, loading curr_modifier.</summary>
    private int GetCurrTile(int tilepos)
    {
        _currModifier = ModAt(_currRoom, tilepos);
        return _currTile = TileAt(_currRoom, tilepos);
    }

    /// <summary>
    /// get_tile (seg006): the tile at (room, col, row), following the room links.
    /// Sets curr_room and curr_tilepos. Beyond the level's edge there is wall.
    /// </summary>
    private int GetTile(int room, int col, int row)
    {
        _currRoom = room;
        while (true)
        {
            if (col < 0) { col += Coord.Cols; if (_currRoom != 0) _currRoom = _level.Left(_currRoom); }
            else if (col >= Coord.Cols) { col -= Coord.Cols; if (_currRoom != 0) _currRoom = _level.Right(_currRoom); }
            else if (row < 0) { row += Coord.Rows; if (_currRoom != 0) _currRoom = _level.Above(_currRoom); }
            else if (row >= Coord.Rows) { row -= Coord.Rows; if (_currRoom != 0) _currRoom = _level.Below(_currRoom); }
            else break;
        }
        _currTilepos = row * Coord.Cols + col;
        if (!ValidRoom(_currRoom)) return (int)TileId.Block;
        return TileAt(_currRoom, _currTilepos);
    }

    // ── trobs ─────────────────────────────────────────────────────────────────

    private struct Trob
    {
        public int Room, Tilepos;
        public sbyte Type;
    }

    private const int TrobsMax = 30;
    private readonly List<Trob> _trobs = [];
    private Trob _trob;

    /// <summary>process_trobs: animate every active tile once; drop the finished ones.</summary>
    public void ProcessTrobs()
    {
        if (_trobs.Count == 0) return;
        bool needDelete = false;
        for (int index = 0; index < _trobs.Count; index++)
        {
            _trob = _trobs[index];
            AnimateTile();
            var t = _trobs[index];
            t.Type = _trob.Type;
            _trobs[index] = t;
            if (_trob.Type < 0) needDelete = true;
        }
        if (needDelete) _trobs.RemoveAll(t => t.Type < 0);
    }

    /// <summary>animate_tile.</summary>
    private void AnimateTile()
    {
        _currRoom = _trob.Room;
        switch ((TileId)GetCurrTile(_trob.Tilepos))
        {
            case TileId.Torch:
            case (TileId)TorchWithDebris: AnimateTorch(); break;
            case TileId.Flask: AnimatePotion(); break;
            case TileId.Sword: AnimateSword(); break;
            case TileId.PressPlate:
            case TileId.UPressPlate: AnimateButton(); break;
            case TileId.Spikes: AnimateSpike(); break;
            case TileId.Loose: AnimateLoose(); break;
            case TileId.Space: AnimateEmpty(); break;
            case TileId.Slicer: AnimateChomper(); break;
            case TileId.Gate: AnimateDoor(); break;
            case TileId.Exit: AnimateLeveldoor(); break;
            default: _trob.Type = -1; break;
        }
        SetMod(_trob.Room, _trob.Tilepos, _currModifier);
    }

    /// <summary>add_trob: start animating a tile, or change how an animating one moves.</summary>
    private void AddTrob(int room, int tilepos, int type)
    {
        _trob = new Trob { Room = room, Tilepos = tilepos, Type = (sbyte)type };
        int found = _trobs.FindIndex(t => t.Tilepos == tilepos && t.Room == room);
        if (found == -1)
        {
            if (_trobs.Count >= TrobsMax) return;
            _trobs.Add(_trob);
        }
        else
        {
            var t = _trobs[found];
            t.Type = _trob.Type;
            _trobs[found] = t;
        }
    }

    /// <summary>start_anim_spike: spikes still in spring out; ones going back in come out again.</summary>
    public void StartAnimSpike(int room, int tilepos)
    {
        if (!ValidRoom(room)) return;
        sbyte oldModifier = (sbyte)ModAt(room, tilepos);
        if (oldModifier > 0) return;
        if (oldModifier == 0) AddTrob(room, tilepos, 1);
        else if (oldModifier != -1) SetMod(room, tilepos, 0x8F);   // 0xFF: disabled
    }

    /// <summary>animate_spike: out, hold, back in. (Spikes don't hurt yet.)</summary>
    private void AnimateSpike()
    {
        if (_trob.Type < 0) return;
        if (_currModifier == 0xFF) return;          // disabled spike
        if ((_currModifier & 0x80) != 0)
        {
            --_currModifier;
            if ((_currModifier & 0x7F) != 0) return;
            _currModifier = 6;
        }
        else
        {
            ++_currModifier;
            if (_currModifier == 5) _currModifier = 0x8F;
            else if (_currModifier == 9)
            {
                _currModifier = 0;
                _trob.Type = -1;
            }
        }
    }

    // ── chompers ──────────────────────────────────────────────────────────────

    /// <summary>custom->chomper_speed: frames in a chomper's cycle (it is shut on frame 2).</summary>
    private const int ChomperSpeed = 15;

    /// <summary>
    /// start_chompers (seg007): the chompers on the kid's row start (or keep) chomping,
    /// each a few frames behind the last so a row of them doesn't bite in step. The
    /// original calls this whenever the kid changes row or room and when he lands.
    /// </summary>
    public void StartChompers(CharState kid)
    {
        int timing = 15;
        if ((uint)kid.Row >= 3 || !ValidRoom(kid.Room)) return;
        _currRoom = kid.Room;
        for (int column = 0, tilepos = kid.Row * Coord.Cols; column < Coord.Cols; ++column, ++tilepos)
        {
            if (GetCurrTile(tilepos) != (int)TileId.Slicer) continue;
            int modifier = _currModifier & 0x7F;
            if (modifier == 0 || modifier >= 6)
            {
                StartAnimChomper(kid.Room, tilepos, (byte)(timing | (_currModifier & 0x80)));
                timing = NextChomperTiming(timing);
            }
        }
    }

    /// <summary>next_chomper_timing: 15, 12, 9, 6, 13, 10, 7, 14, 11, 8, repeat.</summary>
    private static int NextChomperTiming(int timing)
    {
        timing -= 3;
        if (timing < 6) timing += 10;
        return timing;
    }

    /// <summary>start_anim_chomper.</summary>
    private void StartAnimChomper(int room, int tilepos, byte modifier)
    {
        int oldModifier = ModAt(room, tilepos);
        if (oldModifier == 0 || oldModifier >= 6)
        {
            SetMod(room, tilepos, modifier);
            AddTrob(room, tilepos, 1);
        }
    }

    /// <summary>
    /// animate_chomper: step the jaws through their cycle, keeping the blood bit (0x80).
    /// Past frame 6 (open) it stops once the kid has left its room or row, or has died
    /// somewhere else.
    /// </summary>
    private void AnimateChomper()
    {
        if (_trob.Type < 0) return;
        int blood = _currModifier & 0x80;
        int frame = (_currModifier & 0x7F) + 1;
        if (frame > ChomperSpeed) frame = 1;
        _currModifier = (byte)(blood | frame);
        if (Kid is not { } kid) return;
        if ((_trob.Room != DrawnRoom || _trob.Tilepos / Coord.Cols != kid.Row || (!kid.Alive && blood == 0))
            && (_currModifier & 0x7F) >= 6)
        {
            _trob.Type = -1;
        }
    }

    // ── gates ─────────────────────────────────────────────────────────────────

    private static readonly byte[] GateCloseSpeeds = [0, 0, 0, 20, 40, 60, 80, 100, 120];
    private static readonly sbyte[] DoorDelta = [-1, 4, 4];

    /// <summary>
    /// animate_door. trob.type: 0 closing, 1 opening, 2 opening for good, 3..8 slamming
    /// shut ever faster (a closer).
    /// </summary>
    private void AnimateDoor()
    {
        int animType = _trob.Type;
        if (animType < 0) return;

        if (animType >= 3)
        {
            if (animType < 8)
            {
                ++animType;
                _trob.Type = (sbyte)animType;
            }
            int newMod = _currModifier - GateCloseSpeeds[animType];
            _currModifier = (byte)newMod;
            if (newMod < 0)
            {
                _currModifier = 0;
                _trob.Type = -1;
            }
        }
        else if (_currModifier != 0xFF)
        {
            _currModifier = (byte)(_currModifier + DoorDelta[animType]);
            if (animType == 0)
            {
                if (_currModifier == 0) GateStop();
            }
            else if (_currModifier >= 188)
            {
                if (animType < 2)
                {
                    // Fully up: hold (238 counts down to 188 before it moves) and close.
                    _currModifier = 238;
                    _trob.Type = 0;
                }
                else
                {
                    _currModifier = 0xFF;
                    GateStop();
                }
            }
        }
        else
        {
            GateStop();
        }
    }

    /// <summary>gate_stop.</summary>
    private void GateStop() => _trob.Type = -1;

    /// <summary>trigger_gate: what a button does to a gate; the trob type to animate it with.</summary>
    private int TriggerGate(int room, int tilepos, int buttonType)
    {
        byte modifier = ModAt(room, tilepos);
        if (buttonType == (int)TileId.UPressPlate)
        {
            if (modifier == 0xFF) return -1;        // permanently open
            if (modifier >= 188)
            {
                SetMod(room, tilepos, 238);         // already open: keep it open a while
                return -1;
            }
            SetMod(room, tilepos, (byte)((modifier + 3) & 0xFC));
            return 1;
        }
        if (buttonType == (int)TileId.Rubble)
        {
            // A plate jammed by debris or a dead kid opens its gate for good.
            if (modifier < 188) return 2;
            SetMod(room, tilepos, 0xFF);
            return -1;
        }
        return modifier != 0 ? 3 : -1;              // a closer slams it shut
    }

    // ── the exit door ─────────────────────────────────────────────────────────

    private static readonly byte[] LeveldoorCloseSpeeds = [0, 5, 17, 99, 0];

    /// <summary>
    /// animate_leveldoor. trob.type 0..2 opens it one step a tick up to 43; 3..6 slams
    /// the start room's door shut behind the kid.
    /// </summary>
    private void AnimateLeveldoor()
    {
        int trobType = _trob.Type;
        if (_trob.Type < 0) return;

        if (trobType >= 3)
        {
            ++_trob.Type;
            _currModifier = (byte)(_currModifier - LeveldoorCloseSpeeds[_trob.Type - 3]);
            if ((sbyte)_currModifier < 0)
            {
                _currModifier = 0;
                _trob.Type = -1;
            }
        }
        else
        {
            ++_currModifier;
            if (_currModifier >= 43)
            {
                _trob.Type = -1;
                if (LeveldoorOpen is 0 or 2)
                {
                    LeveldoorOpen = 1;
                    if (_levelNumber == 4)
                    {
                        // Special event: the mirror appears on level 4.
                        GetTile(4, 4, 0);
                        SetTile(_currRoom, _currTilepos, (int)TileId.Mirror);
                    }
                }
            }
        }
    }

    /// <summary>start_level_door: the start room's door begins open and slams shut.</summary>
    private void StartLevelDoor(int room, int tilepos)
    {
        SetMod(room, tilepos, 43);
        AddTrob(room, tilepos, 3);
    }

    /// <summary>find_start_level_door (seg003).</summary>
    public void FindStartLevelDoor(int kidRoom)
    {
        if (!ValidRoom(kidRoom)) return;
        for (int tilepos = 0; tilepos < Level.CellsPerScreen; tilepos++)
            if (TileAt(kidRoom, tilepos) == (int)TileId.Exit)
                StartLevelDoor(kidRoom, tilepos);
    }

    // ── buttons ───────────────────────────────────────────────────────────────

    /// <summary>trigger_1: the trob type for a target of a button, or -1 for none.</summary>
    private int Trigger1(int targetType, int room, int tilepos, int buttonType)
    {
        if (targetType == (int)TileId.Gate) return TriggerGate(room, tilepos, buttonType);
        if (targetType == (int)TileId.Exit) return ModAt(room, tilepos) != 0 ? -1 : 1;
        return -1;
    }

    /// <summary>do_trigger_list: trigger everything on a button's link chain.</summary>
    private void DoTriggerList(int index, int buttonType)
    {
        while (index is >= 0 and < 256)
        {
            int room = _links.Screen(index);
            int tilepos = _links.Cell(index);
            if (ValidRoom(room) && tilepos < Level.CellsPerScreen)
            {
                int targetType = TileAt(room, tilepos);
                int result = Trigger1(targetType, room, tilepos, buttonType);
                if (result >= 0) AddTrob(room, tilepos, result);
            }
            if (_links.IsLast(index)) break;
            index++;
        }
    }

    /// <summary>
    /// trigger_button: press the button at curr_room/curr_tilepos. A plate stays down
    /// for 5 ticks after the last press (a timer of 31 means jammed for good).
    /// <paramref name="buttonType"/> 0 and <paramref name="modifier"/> -1 mean "the
    /// tile's own".
    /// </summary>
    private void TriggerButton(int buttonType, int modifier)
    {
        GetCurrTile(_currTilepos);
        if (buttonType == 0) buttonType = _currTile;
        if (modifier == -1) modifier = _currModifier;

        int linkTimer = _links.Timer(modifier);
        if (linkTimer == 0x1F) return;              // jammed
        _links.SetTimer(modifier, 5);
        if (linkTimer < 2) AddTrob(_currRoom, _currTilepos, 1);
        DoTriggerList(modifier, buttonType);
    }

    /// <summary>
    /// Presses the button at a cell (get_tile then trigger_button). Level 1 uses this
    /// at the start: the closer in room 5 is pressed as the kid drops in, so the gate
    /// beside the first room — authored open — slams shut (do_startpos).
    /// </summary>
    public void PressButton(int room, int col, int row)
    {
        GetTile(room, col, row);
        if (ValidRoom(_currRoom)) TriggerButton(0, -1);
    }

    /// <summary>
    /// died_on_button (JAMPP in MOVER.S): a dead kid's weight jams the plate. An opener
    /// becomes floor and opens its gates for good; a closer stays down.
    /// </summary>
    private void DiedOnButton()
    {
        int buttonType = GetCurrTile(_currTilepos);
        int modifier = _currModifier;
        if (_currTile == (int)TileId.UPressPlate)
        {
            SetTile(_currRoom, _currTilepos, (int)TileId.Floor);
            SetMod(_currRoom, _currTilepos, 0);
            buttonType = (int)TileId.Rubble;        // force permanent open
        }
        else
        {
            SetTile(_currRoom, _currTilepos, (int)TileId.DPressPlate);
        }
        TriggerButton(buttonType, modifier);
    }

    /// <summary>animate_button: count the plate's timer down; it pops up below 2.</summary>
    private void AnimateButton()
    {
        if (_trob.Type < 0) return;
        int timer = (_links.Timer(_currModifier) - 1) & 0xFFFF;   // a word in the original
        _links.SetTimer(_currModifier, timer);
        if (timer < 2) _trob.Type = -1;
    }

    // ── loose floors ──────────────────────────────────────────────────────────

    /// <summary>animate_empty.</summary>
    private void AnimateEmpty() => _trob.Type = -1;

    /// <summary>
    /// animate_loose. A modifier with bit 7 set is a board shaking after a knock
    /// (0x80..0x83, then still again). Otherwise something is standing on it: it counts
    /// up from 1 and at 11 the board comes away and falls as a mob.
    /// </summary>
    private void AnimateLoose()
    {
        if (_trob.Type < 0) return;

        ++_currModifier;
        if ((_currModifier & 0x80) != 0)
        {
            // Just shaking. Level 13's auto-falling floors don't stop.
            if (_levelNumber == LooseTilesLevel) return;
            if (_currModifier >= 0x84)
            {
                _currModifier = 0;
                _trob.Type = -1;
            }
        }
        else if (_currModifier >= LooseFloorDelay)
        {
            int room = _trob.Room, tilepos = _trob.Tilepos;
            _currModifier = RemoveLoose(room, tilepos);
            _trob.Type = -1;
            int row = tilepos / Coord.Cols;
            _curmob = new Mob
            {
                Xh = (tilepos % Coord.Cols) << 2,
                Y = YLooseLand[row + 1],
                Room = room,
                Speed = 0,
                Type = 0,
                Row = row,
            };
            AddMob();
        }
    }

    private static readonly int[] YLooseLand = [2, 65, 128, 191, 254];

    /// <summary>remove_loose: the board is gone; the space it leaves takes the level type.</summary>
    private byte RemoveLoose(int room, int tilepos)
    {
        SetTile(room, tilepos, (int)TileId.Space);
        return (byte)DosLevels.LevelType(_levelNumber);
    }

    /// <summary>
    /// make_loose_fall: something is on the loose floor at curr_tilepos; start its
    /// countdown (unless it is a "solid" one, flagged 0x20, or already counting).
    /// </summary>
    private void MakeLooseFall(byte modifier)
    {
        if (!ValidRoom(_currRoom)) return;
        if ((_level.LiveBlueType[Index(_currRoom, _currTilepos)] & 0x20) != 0) return;
        if ((sbyte)ModAt(_currRoom, _currTilepos) > 0) return;
        SetMod(_currRoom, _currTilepos, modifier);
        AddTrob(_currRoom, _currTilepos, 0);
    }

    /// <summary>loose_make_shake: rattle the loose floor at curr_tilepos.</summary>
    private void LooseMakeShake()
    {
        if (ModAt(_currRoom, _currTilepos) == 0 && _levelNumber != LooseTilesLevel)
        {
            SetMod(_currRoom, _currTilepos, 0x80);
            AddTrob(_currRoom, _currTilepos, 1);
        }
    }

    /// <summary>do_knock: every loose floor on a row of a room shakes.</summary>
    private void DoKnock(int room, int tileRow)
    {
        for (int tileCol = 0; tileCol < Coord.Cols; tileCol++)
            if (GetTile(room, tileCol, tileRow) == (int)TileId.Loose && ValidRoom(_currRoom))
                LooseMakeShake();
    }

    /// <summary>
    /// check_knock (seg003): a landing or a bump into the ceiling (the sequences'
    /// jarU/jarD, <see cref="SeqEffects.JarFloor"/>) shakes the loose floors of the
    /// row above (+1) or the kid's own row (-1).
    /// </summary>
    public void CheckKnock(CharState kid, int knock)
    {
        if (knock == 0) return;
        DoKnock(kid.Room, kid.Row - (knock > 0 ? 1 : 0));
    }

    // ── mobs ──────────────────────────────────────────────────────────────────

    /// <summary>A falling loose-floor piece (mob_type). Y is a byte, as in the original.</summary>
    public struct Mob
    {
        public int Xh, Y, Room, Speed, Type, Row;
    }

    private const int MobsMax = 14;
    private readonly List<Mob> _mobs = [];
    private Mob _curmob;
    private int _curmobIndex;

    public IReadOnlyList<Mob> Mobs => _mobs;

    /// <summary>add_mob.</summary>
    private void AddMob()
    {
        if (_mobs.Count >= MobsMax) return;
        _mobs.Add(_curmob);
    }

    /// <summary>do_mobs: move each piece (not the ones added this tick) and drop the landed.</summary>
    public void DoMobs()
    {
        int nMobs = _mobs.Count;
        for (_curmobIndex = 0; nMobs > _curmobIndex; ++_curmobIndex)
        {
            _curmob = _mobs[_curmobIndex];
            MoveMob();
            CheckLooseFallOnKid();
            _mobs[_curmobIndex] = _curmob;
        }
        _mobs.RemoveAll(m => m.Speed == -1);
    }

    /// <summary>move_mob.</summary>
    private void MoveMob()
    {
        if (_curmob.Type == 0) MoveLoose();
        if (_curmob.Speed <= 0) ++_curmob.Speed;
    }

    private static readonly int[] YSomething = [-1, 62, 125, 188, 25];

    /// <summary>move_loose: fall, row by row and room by room, until something stops it.</summary>
    private void MoveLoose()
    {
        if (_curmob.Speed < 0) return;
        if (_curmob.Speed < 29) _curmob.Speed += 3;
        _curmob.Y = (byte)(_curmob.Y + _curmob.Speed);

        if (_curmob.Room == 0)
        {
            if (_curmob.Y >= 210) _curmob.Speed = -2;     // fell out of the level
            return;
        }

        if (_curmob.Y < 226 && YSomething[_curmob.Row + 1] <= _curmob.Y)
        {
            // Fell into a different row.
            int tileTemp = GetTile(_curmob.Room, _curmob.Xh >> 2, _curmob.Row);
            if (tileTemp == (int)TileId.Loose) LooseFall();
            if (tileTemp is (int)TileId.Space or (int)TileId.Loose)
            {
                MobDownARow();
                return;
            }
            DoKnock(_curmob.Room, _curmob.Row);
            _curmob.Y = YSomething[_curmob.Row + 1];
            _curmob.Speed = -2;
            LooseLand();
        }
    }

    /// <summary>
    /// loose_land: the piece shatters on the floor below, leaving rubble. Landing on a
    /// plate presses it (an opener is jammed open for good).
    /// </summary>
    private void LooseLand()
    {
        int buttonType = 0;
        int tiletype = GetTile(_curmob.Room, _curmob.Xh >> 2, _curmob.Row);
        if (!ValidRoom(_currRoom)) return;
        switch ((TileId)tiletype)
        {
            case TileId.UPressPlate:
            case TileId.PressPlate:
                if (tiletype == (int)TileId.UPressPlate)
                {
                    SetTile(_currRoom, _currTilepos, (int)TileId.Rubble);
                    buttonType = (int)TileId.Rubble;
                }
                TriggerButton(buttonType, -1);
                tiletype = GetTile(_curmob.Room, _curmob.Xh >> 2, _curmob.Row);
                goto case TileId.Floor;

            case TileId.Floor:
            case TileId.Spikes:
            case TileId.Flask:
            case TileId.Torch:
            case (TileId)30:                            // torch with debris
                SetTile(_currRoom, _currTilepos,
                        tiletype is (int)TileId.Torch or 30 ? 30 : (int)TileId.Rubble);
                break;
        }
    }

    /// <summary>loose_fall: a piece hit another loose floor, which comes away too.</summary>
    private void LooseFall()
    {
        SetMod(_currRoom, _currTilepos, RemoveLoose(_currRoom, _currTilepos));
        _curmob.Speed >>= 1;
        _mobs[_curmobIndex] = _curmob;
        _curmob.Y = (byte)(_curmob.Y + 6);
        MobDownARow();
        AddMob();
        _curmob = _mobs[_curmobIndex];
    }

    /// <summary>mob_down_a_row.</summary>
    private void MobDownARow()
    {
        ++_curmob.Row;
        if (_curmob.Row >= 3)
        {
            _curmob.Y = (byte)(_curmob.Y - 192);
            _curmob.Row = 0;
            _curmob.Room = _level.Below(_curmob.Room);
        }
    }

    /// <summary>check_loose_fall_on_kid.</summary>
    private void CheckLooseFallOnKid()
    {
        if (Kid is not { } kid) return;
        if (kid.Room == _curmob.Room && kid.Col == _curmob.Xh >> 2
            && _curmob.Y < (byte)kid.Y && (byte)kid.Y - 30 < _curmob.Y)
        {
            LooseFellOnKid?.Invoke();
        }
    }

    // ── check_press (seg006) ──────────────────────────────────────────────────

    /// <summary>
    /// check_press: is the kid pressing a plate or standing on a loose floor? Hanging
    /// and climbing frames press the tile he holds; on-the-ground actions press the tile
    /// underfoot, but only on frames whose foot touches the floor (<c>fcheckmark</c>).
    /// Jumping up into a loose floor above knocks it loose.
    /// </summary>
    public void CheckPress(CharState kid, FrameDef frame)
    {
        int f = kid.Frame;
        var action = kid.Action;
        int tile;

        if (f is >= 87 and < 100 or >= 135 and < 141)
        {
            tile = GetTile(kid.Room, kid.Col, kid.Row - 1);         // the ledge he holds
        }
        else if (action is CharAction.Turn or CharAction.Bumped
                 or CharAction.Stand or CharAction.RunJump)
        {
            if (f == 79 && GetTile(kid.Room, kid.Col, kid.Row - 1) == (int)TileId.Loose)
            {
                MakeLooseFall(1);                                   // break it from below
                return;
            }
            if (!frame.Check) return;
            tile = GetTile(kid.Room, kid.Col, kid.Row);
        }
        else return;

        if (!ValidRoom(_currRoom)) return;
        if (tile is (int)TileId.UPressPlate or (int)TileId.PressPlate)
        {
            if (kid.Alive) TriggerButton(0, -1);
            else DiedOnButton();
        }
        else if (tile == (int)TileId.Loose)
        {
            MakeLooseFall(1);
        }
    }
}
