namespace POPGame.Engine;

/// <summary>
/// All physics, hazard, and combat constants sourced from the original 6502 ASM source.
/// </summary>
public static class Constants
{
    // ── Physics ───────────────────────────────────────────────────────────────
    public const int BlockHeight    = 63;   // pixel height of one row
    public const int VertDist       = 10;   // vertical step per tick
    public const int ScrnWidth      = 140;  // screen width in game pixels
    public const int ScrnHeight     = 192;  // screen height in game pixels
    public const int GrabSpeed      = 32;   // max Y-velocity to grab a ledge
    public const int DeathVelocity  = 33;   // fall speed that kills on landing
    public const int OofVelocity    = 22;   // fall speed that causes -1 HP on landing
    public const int FFAccel        = 3;    // free-fall Y-velocity increment per tick
    public const int FFTermVel      = 29;   // free-fall terminal velocity
    public const int JumpInitVel    = -20;  // initial Y-velocity for a jump (negative = up)

    // ── Gate ──────────────────────────────────────────────────────────────────
    public const int GMaxVal    = 188;  // gate fully-closed spec value
    public const int GateTimer  = 238;  // ticks before a gate auto-closes
    public const int GateStep   = 20;   // spec reduction per tick when opening

    // ── Spikes ────────────────────────────────────────────────────────────────
    public const int SpikeExt   = 5;    // ticks for spikes to extend (dangerous)
    public const int SpikeRet   = 9;    // ticks for spikes to retract (safe)
    public const int SpikeTimer = 143;  // period of spike cycle

    // ── Slicer ────────────────────────────────────────────────────────────────
    public const int SlicerExt  = 2;    // ticks to extend (lethal)
    public const int SlicerRet  = 6;    // ticks to retract
    public const int SliceTimer = 15;   // period of slicer cycle

    // ── Loose floor ───────────────────────────────────────────────────────────
    public const int LooseTimer  = 10;  // ticks before loose floor falls
    public const int WiggleTime  = 4;   // ticks of wiggling before fall
    public const int PPTimer     = 5;   // pressure-plate active ticks

    // ── Combat ────────────────────────────────────────────────────────────────
    public const int StrikeRange1  = 12;   // min distance for sword strike
    public const int StrikeRange2  = 29;   // max distance for sword strike
    public const int BlockThres    = 32;   // max distance to block a strike
    public const int SwordThres    = 90;   // distance at which guard draws sword
    public const int GdPatience    = 15;   // frames guard waits before advancing
    public const int StunTime      = 12;   // frames of stun after being hit
    public const int GracePerod    = 9;    // post-hit invulnerability frames

    // ── Guard AI probability tables (indexed by guardprog 0-11) ──────────────
    public static readonly int[] StrikeProb   = { 75, 100, 75, 75, 75, 50, 100, 220,   0,  60,  40,  60 };
    public static readonly int[] BlockProb    = {  0, 150,150,200,200,255, 200, 250,   0, 255, 255, 255 };
    public static readonly int[] AdvProb      = {255, 200,200,200,255,255, 200,   0,   0, 255, 100, 100 };
    public static readonly int[] RefractTimer = { 20,  20, 20, 20, 10, 10,  10,  10,   0,  10,   0,   0 };

    // ── Guard base strength per level (basicstrength[]) ───────────────────────
    public static readonly int[] BasicStrength = { 4, 3, 3, 3, 3, 4, 5, 4, 4, 5, 5, 5, 4, 6 };

    // ── Level/game limits ─────────────────────────────────────────────────────
    public const int LastPlayableLevel = 13;  // level index 0-13
    public const int FramesPerMinute   = 1090; // at 10 fps ≈ 109s real per game-minute
    public const int TimeLimitMinutes  = 60;   // total time limit
}
