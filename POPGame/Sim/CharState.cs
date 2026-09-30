namespace POPGame.Sim;

/// <summary>Values the sequence table's <c>act</c> opcode writes to CharAction.</summary>
public enum CharAction : byte
{
    Stand        = 0,
    RunJump      = 1,
    HangClimb    = 2,
    InMidair     = 3,
    InFreefall   = 4,
    Bumped       = 5,
    HangStraight = 6,
    Turn         = 7,
}

/// <summary>Sequence ids, from SEQDATA.S. Only the ones the engine references are named.</summary>
public static class Seq
{
    public const int StartRun = 1, Stand = 2, StandJump = 3, RunJump = 4, Turn = 5,
                     RunTurn = 6, StepFall = 7, JumpHangMed = 8, Hang = 9, ClimbUp = 10,
                     HangDrop = 11, FreeFall = 12, RunStop = 13, JumpUp = 14, FallHang = 15,
                     JumpBackHang = 16, SoftLand = 17, JumpFall = 18, StepFall2 = 19,
                     MedLand = 20, RJumpFall = 21, HardLand = 22, HangFall = 23,
                     JumpHangLong = 24, HangStraight = 25, RDiveRoll = 26, SDiveRoll = 27,
                     HighJump = 28,
                     StepFwd1 = 29,      // stepfwd 1..14 are 29..42
                     FullStep = 42, TurnRun = 43, TestFoot = 44, BumpFall = 45,
                     HardBump = 46, Bump = 47, SuperHiJump = 48, StandUp = 49, Stoop = 50,
                     Impale = 51, Crush = 52, DeadFall = 53, Halve = 54,
                     EnGarde = 55, Advance = 56, Retreat = 57, Strike = 58, Flee = 59,
                     TurnEnGarde = 60, StrikeBlock = 61, ReadyBlock = 62, LandEnGarde = 63,
                     BumpEngFwd = 64, BumpEngBack = 65, BlockToStrike = 66, StrikeAdv = 67,
                     ClimbDown = 68, BlockedStrike = 69, ClimbStairs = 70, DropDead = 71,
                     StepBack = 72, ClimbFail = 73, Stabbed = 74, FastStrike = 75,
                     StrikeRet = 76, AlertStand = 77, DrinkPotion = 78, Crawl = 79,
                     AlertTurn = 80, FightFall = 81, Running = 84, StabKill = 85,
                     FastAdvance = 86, GoAlertStand = 87, Arise = 88, TurnDraw = 89,
                     GuardEnGarde = 90, PickUpSword = 91, Resheathe = 92, FastSheathe = 93;
}

/// <summary>
/// Runtime state of one animated character. Mirrors SDLPoP's <c>char_type</c> (the
/// original's Char* variables, EQ.S): position, facing, the sequence-table cursor, and
/// the fall velocities the <c>setfall</c> opcode seeds.
/// </summary>
public sealed class CharState
{
    public int Room = 1;              // 1-indexed screen; 0 = fell out of the level
    public int X;                     // x units (Char.x); see Coord
    public int Y;                     // pixels (Char.y): the character's floor line
    public sbyte Face = -1;           // -1 = left (sprites are drawn facing left), 0 = right

    public int Frame = 15;            // current frame number, 1..240
    public CharAction Action = CharAction.Stand;

    public int SeqPtr;                // cursor into the sequence bytecode
    public int CurrentSeq;            // sequence id, for debugging / decisions

    public int FallX, FallY;          // Char.fall_x / fall_y, seeded by 'setfall'
    public int Repeat;                // Char.repeat: safe_step's "second try" flag

    public int Hp = 3, MaxHp = 3;
    public bool Alive = true;
    public bool HasSword;

    public bool FacingRight => Face >= 0;
    public int FaceSign => Face < 0 ? -1 : 1;

    /// <summary>
    /// Char.curr_row: the block row the character occupies. Kept as its own field, not
    /// derived from Y, because the sequence table's up/down opcodes and the fall code
    /// move it independently of the pixel position.
    /// </summary>
    public int Row;

    /// <summary>
    /// Char.curr_col: the column under the character's weight, set by determine_col
    /// from the frame's foot offset (see <see cref="KidEngine"/>). It is not derived
    /// from X on the fly, because the original only refreshes it at fixed points in
    /// the frame and the stale value matters.
    /// </summary>
    public int Col;

    /// <summary>char_dx_forward: X moved by <paramref name="dx"/> in the facing direction.</summary>
    public int DxForward(int dx) => X + (Face < 0 ? -dx : dx);

    /// <summary>Applies a delta in the direction the character faces (ADDCHARX in CTRLSUBS.S).</summary>
    public void AddX(int dx) => X = DxForward(dx);

    public void Flip() => Face = (sbyte)~Face;
}
