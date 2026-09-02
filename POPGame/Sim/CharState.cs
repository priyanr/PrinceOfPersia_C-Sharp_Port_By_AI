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
/// Runtime state of one animated character. Mirrors the Char* variables in the
/// original (EQ.S): position, facing, the current sequence-table cursor, and the
/// fall velocities the <c>setfall</c> opcode seeds.
/// </summary>
public sealed class CharState
{
    public int Room = 1;              // 1-indexed screen
    public int X;                     // x units; see Coord
    public int Y;                     // pixels
    public sbyte Face = -1;           // -1 = left (sprites are drawn facing left), 0 = right

    public int Frame = 15;            // current frame number, 1..240
    public CharAction Action = CharAction.Stand;

    public int SeqPtr;                // cursor into the sequence bytecode
    public int CurrentSeq;            // sequence id, for debugging / decisions

    public int XVel, YVel;            // fall velocities, seeded by 'setfall'
    public int FallCount;             // frames spent falling, for landing damage

    public int Hp = 3, MaxHp = 3;
    public bool Alive = true;
    public bool HasSword;

    public bool FacingRight => Face >= 0;
    public int FaceSign => Face < 0 ? -1 : 1;

    /// <summary>
    /// The block row the character logically occupies. Kept as its own field, not
    /// derived from Y, because the sequence table's up/down opcodes move it
    /// independently while a climb or a fall is in progress (ANIMCHAR in COLL.S).
    /// </summary>
    public int Row;

    public int BlockX => Coord.BlockX(X);
    public int RowFromY => Coord.BlockY(Y);

    /// <summary>Applies a delta in the direction the character faces (ADDCHARX in CTRLSUBS.S).</summary>
    public void AddX(int dx) => X += Face < 0 ? -dx : dx;

    public void Flip() => Face = (sbyte)(Face < 0 ? 0 : -1);
}
