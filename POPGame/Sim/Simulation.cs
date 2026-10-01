using POPGame.Data;
using POPGame.Dos;
using POPGame.Input;

namespace POPGame.Sim;

/// <summary>
/// Owns one level's simulation: the level data, the kid, and the per-tick order of
/// operations the original uses (play_frame / play_kid_frame in SDLPoP) — control
/// decides the sequence, the sequence poses and moves the character, then collisions,
/// the floor, pressure plates and the room exit are resolved against it.
/// </summary>
public sealed class Simulation
{
    public DosTables Tables { get; }
    public Level Level { get; private set; }
    public RoomView View { get; private set; } = null!;
    public CharState Kid { get; } = new();
    public SeqEffects Effects { get; } = new();
    public int LevelNumber { get; private set; }

    private readonly SeqRunner _seq;
    private readonly Func<FrameDef, (int W, int H)> _kidImageSize;
    private KidEngine _engine = null!;
    private Hazards _hazards = null!;

    /// <param name="kidImageSize">
    /// Width and height of a frame's kid sprite (KID.DAT). Collision uses the sprite's
    /// width, as the original's set_char_collision does.
    /// </param>
    public Simulation(DosTables tables, Level level, int levelNumber,
                      Func<FrameDef, (int W, int H)> kidImageSize)
    {
        Tables = tables;
        _seq = new SeqRunner(tables);
        _kidImageSize = kidImageSize;
        Level = level;
        LevelNumber = levelNumber;
        Setup();
    }

    public void LoadLevel(Level level, int levelNumber)
    {
        // Restarting a level must undo any gates, plates and loose floors it changed.
        level.Reset();
        Level = level;
        LevelNumber = levelNumber;
        Setup();
    }

    private void Setup()
    {
        View = new RoomView(Level);
        _hazards = new Hazards(Level, LevelNumber) { Kid = Kid };
        _hazards.SeedRandom(RandomSeed);
        _engine = new KidEngine(_seq, Tables, View, _kidImageSize, _hazards, LevelNumber)
        {
            StartRoom = Level.KidStartScrn,
        };
        _hazards.LooseFellOnKid = () => _engine.LooseFellOnKid(Kid, Effects);
        _seq.RowChanged = ch => _hazards.StartChompers(ch);
        _deadCounter = -1;
        StartPos(Level.KidStartScrn, Level.KidStartBlock, Level.KidStartFace == 0xFF ? -1 : 0,
                 fallingEntry: LevelNumber == 1);
        _hazards.FindStartLevelDoor(Kid.Room);
    }

    /// <summary>
    /// check_the_end (seg000): when the kid has moved to another room, that room is
    /// the one drawn, and its chompers on his row start.
    /// </summary>
    private void CheckTheEnd()
    {
        if (Kid.Room == 0 || Kid.Room == _hazards.DrawnRoom) return;
        _hazards.DrawnRoom = Kid.Room;
        _hazards.AnimTileModif();
        _hazards.StartChompers(Kid);
        _hazards.CheckFallFlo();
    }

    /// <summary>
    /// random_seed for this level and on. The live game seeds it from the clock, as the
    /// original does; the headless runner uses a fixed seed so dumps repeat.
    /// </summary>
    public static uint RandomSeed { get; set; } = (uint)Environment.TickCount;

    // Kid.alive once he is dead: 0 on the tick he dies, then one more each tick his
    // death frame (177 spiked, 178 chomped, 185 dead) is showing. -1 while alive.
    private int _deadCounter = -1;

    /// <summary>
    /// The kid is dead and the level can be restarted (Kid.alive &gt; 6: Shift or Enter
    /// restarts, seg000). The original waits for the death music; with no sound it
    /// counts ticks from the death frame, as it does when the sound is off.
    /// </summary>
    public bool CanRestart => _deadCounter > 6;

    /// <summary>"Press Button to Continue" is shown (not on the demo or level 15).</summary>
    public bool ShowPressButton => CanRestart && LevelNumber is not (0 or 15);

    /// <summary>is_dead (seg006): showing a death frame.</summary>
    private static bool IsDeadFrame(int frame) => frame is >= 177 and <= 178 or 185;

    /// <summary>The level's animated tiles and falling floor pieces.</summary>
    public Hazards Hazards => _hazards;

    /// <summary>upside_down: draw the screen flipped (a potion's effect).</summary>
    public bool UpsideDown => _engine.UpsideDown;

    /// <summary>
    /// flash_if_hurt / remove_flash_if_hurt (seg003): the colour the background flashes
    /// this tick, or 0. A pickup flashes for flash_time ticks; losing a hit point
    /// flashes bright red.
    /// </summary>
    public int Flash { get; private set; }

    /// <summary>
    /// Puts the kid at a block the way the level start does (DO_STARTPOS): he starts
    /// facing away from <paramref name="face"/> and turns to it. The headless test hook
    /// uses this to start anywhere.
    /// </summary>
    public void PlaceKid(int room, int block, int face) => StartPos(room, block, face, fallingEntry: false);

    private void StartPos(int room, int block, int face, bool fallingEntry)
    {
        Effects.Clear();
        // Level 1's special entry: the closer in room 5 (col 2, row 0) is pressed so
        // the gate beside the first room, authored open, slams shut as the kid drops in.
        if (fallingEntry) _hazards.PressButton(5, 2, 0);
        _engine.StartPos(Kid, Effects, room, block, face, fallingEntry);
        _deadCounter = -1;
        _hazards.DrawnRoom = 0;
        CheckTheEnd();
    }

    /// <summary>Game ticks run so far (12 a second); torches and potions animate from it.</summary>
    public int TickCount { get; private set; }

    public void Tick(InputState input)
    {
        TickCount++;
        Effects.Clear();

        // play_frame (seg000): falling pieces and animated tiles move first, then the
        // kid (whose frame ends with check_press / check_knock), then the room exit.
        int hp = Kid.Hp;
        _hazards.DoMobs();
        _hazards.ProcessTrobs();
        _engine.PlayKidFrame(Kid, input, Effects);
        if (!Kid.Alive)
        {
            // control_kid / play_kid (seg006).
            if (_deadCounter < 0) _deadCounter = 0;
            else if (IsDeadFrame(Kid.Frame)) _deadCounter++;
        }
        _engine.ExitRoom(Kid);
        CheckTheEnd();

        Flash = 0;
        if (_engine.FlashTime != 0)
        {
            Flash = _engine.FlashColor;
            _engine.FlashTime--;
        }
        else if (Kid.Hp < hp)
        {
            Flash = 12;
        }
    }

    /// <summary>Drive the kid from level 0's demo move table (see <see cref="KidEngine.DemoMode"/>).</summary>
    public bool DemoMode
    {
        get => _engine.DemoMode;
        set => _engine.DemoMode = value;
    }

    /// <summary>add_kid_to_objtable: how the kid is drawn this frame (clip, tile, redraws).</summary>
    public KidDrawInfo KidDraw(int leveldoorYBottom, int leveldoorRight) =>
        _engine.KidDraw(Kid, leveldoorYBottom, leveldoorRight);

    public FrameDef KidFrame => Tables.Frames[Math.Clamp(Kid.Frame, 0, DosTables.FrameCount)];
}
