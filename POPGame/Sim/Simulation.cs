using POPGame.Data;
using POPGame.Dos;
using POPGame.Input;

namespace POPGame.Sim;

/// <summary>
/// Owns one level's simulation: the level data, the kid, and the per-tick order of
/// operations the original uses — control decides the sequence, the sequence poses
/// and moves the character, then physics resolves the world against it.
/// </summary>
public sealed class Simulation
{
    public DosTables Tables { get; }
    public Level Level { get; private set; }
    public RoomView View { get; private set; }
    public CharState Kid { get; } = new();
    public SeqEffects Effects { get; } = new();
    public int LevelNumber { get; private set; }

    private readonly SeqRunner _seq;
    private KidControl _control;
    private Physics _physics;
    private Hazards _hazards = null!;

    public Simulation(DosTables tables, Level level, int levelNumber)
    {
        Tables = tables;
        _seq = new SeqRunner(tables);
        Level = level;
        LevelNumber = levelNumber;
        View = new RoomView(level);
        _control = new KidControl(_seq, View);
        _physics = new Physics(View, _seq);
        _hazards = new Hazards(level);

        ResetKid();
    }

    public void LoadLevel(Level level, int levelNumber)
    {
        // Restarting a level must undo any gates, plates and loose floors it changed.
        level.Reset();
        Level = level;
        LevelNumber = levelNumber;
        View = new RoomView(level);
        _control = new KidControl(_seq, View);
        _physics = new Physics(View, _seq);
        _hazards = new Hazards(level);
        ResetKid();
    }

    private void ResetKid()
    {
        int block = Level.KidStartBlock;
        Kid.Room = Level.KidStartScrn;
        Kid.Row = block / Coord.Cols;
        int col = block % Coord.Cols;

        // Start in the middle of the starting block, standing on its floor.
        Kid.X = Coord.BlockEdge(col) + Coord.BlockWidth / 2;
        Kid.Y = Coord.FloorY(Kid.Row);
        Kid.Face = (sbyte)(Level.KidStartFace == 0xFF ? -1 : 0);

        Kid.Hp = Kid.MaxHp;
        Kid.Alive = true;
        Kid.XVel = Kid.YVel = 0;
        Kid.FallCount = 0;

        _seq.Start(Kid, Seq.Stand);
        Kid.Frame = 15;
        Kid.Action = CharAction.Stand;
    }

    public void Tick(InputState input)
    {
        Effects.Clear();

        _control.Update(Kid, input);
        _seq.Animate(Kid, Effects);
        _physics.Update(Kid, Effects);
        _hazards.Tick(Kid, Effects);
        if (_hazards.LevelComplete) Effects.NextLevel = true;
    }

    public FrameDef KidFrame => Tables.Frames[Kid.Frame];
}
