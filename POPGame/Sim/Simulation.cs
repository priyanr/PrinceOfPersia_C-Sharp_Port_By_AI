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
        _hazards = new Hazards(Level);
        _engine = new KidEngine(_seq, Tables, View, _kidImageSize, () => _hazards.ExitOpen)
        {
            StartRoom = Level.KidStartScrn,
        };
        StartPos(Level.KidStartScrn, Level.KidStartBlock, Level.KidStartFace == 0xFF ? -1 : 0,
                 fallingEntry: LevelNumber == 1);
    }

    /// <summary>
    /// Puts the kid at a block the way the level start does (DO_STARTPOS): he starts
    /// facing away from <paramref name="face"/> and turns to it. The headless test hook
    /// uses this to start anywhere.
    /// </summary>
    public void PlaceKid(int room, int block, int face) => StartPos(room, block, face, fallingEntry: false);

    private void StartPos(int room, int block, int face, bool fallingEntry)
    {
        Effects.Clear();
        // Level 1's special entry: the closer in room 5 (row 0, col 2) is pressed so
        // the gate beside the first room, authored open, slams shut as the kid drops in.
        if (fallingEntry) _hazards.PressButton(5, 0, 2);
        _engine.StartPos(Kid, Effects, room, block, face, fallingEntry);
    }

    public void Tick(InputState input)
    {
        Effects.Clear();

        _engine.PlayKidFrame(Kid, input, Effects);
        _hazards.Tick(Kid, KidFrame, Effects);
        _engine.ExitRoom(Kid);
    }

    /// <summary>Drive the kid from level 0's demo move table (see <see cref="KidEngine.DemoMode"/>).</summary>
    public bool DemoMode
    {
        get => _engine.DemoMode;
        set => _engine.DemoMode = value;
    }

    public FrameDef KidFrame => Tables.Frames[Math.Clamp(Kid.Frame, 0, DosTables.FrameCount)];
}
