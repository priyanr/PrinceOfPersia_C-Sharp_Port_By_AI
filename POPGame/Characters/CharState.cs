using POPGame.Engine;

namespace POPGame.Characters;

public enum PlayerAction
{
    OnGround,
    Running,
    Turning,
    Jumping,
    FreeFall,
    Hanging,
    ClimbUp,
    ClimbDown,
    Crouching,
    EnGarde,
    Strike,
    Blocking,
    Dead,
}

public enum GuardState
{
    Patrol,
    Alert,
    EnGarde,
    Strike,
    Blocking,
    Stunned,
    Dead,
}

/// <summary>
/// Runtime state shared by kid and guards.
/// Coordinates use block-grid (BlockX 0-9, BlockY 0-2) on current screen.
/// Fine X within block: SubX 0-2 (for sub-block movement).
/// </summary>
public class CharState
{
    // Position (block grid on current screen)
    public int  Screen = 1;   // 1-indexed
    public int  BlockX = 0;   // 0-9 (column)
    public int  BlockY = 2;   // 0-2 (row; 2 = floor)
    public int  SubX   = 1;   // sub-block X position (0-2)

    // Direction: +1 = right, -1 = left
    public int  FaceDir = -1;

    // Physics
    public int  YVel  = 0;    // positive = falling down
    public bool OnFloor = true;

    // Life / combat
    public int  Life    = 3;
    public int  MaxLife = 3;
    public bool HasSword = false;
    public bool InFight  = false;

    // Stun / grace
    public int  StunFrames  = 0;
    public int  GraceFrames = 0;

    // State machine
    public PlayerAction Action    = PlayerAction.OnGround;
    public GuardState   GdAction  = GuardState.Patrol;

    // Guard-specific
    public int  GuardProg   = 0;   // AI programme index 0-11
    public int  RefractLeft = 0;   // refractory frames after action
    public int  PatrolTimer = 0;

    // Guard index (-1 for kid)
    public int  GuardIndex = -1;

    // Whether character is alive
    public bool IsAlive => Life > 0 && Action != PlayerAction.Dead;

    public void TakeDamage(int dmg = 1)
    {
        if (GraceFrames > 0) return;
        Life -= dmg;
        if (Life <= 0) { Life = 0; Action = PlayerAction.Dead; }
    }

    public void ApplyStun(int frames)
    {
        StunFrames  = frames;
        GraceFrames = Constants.GracePerod;
    }
}
