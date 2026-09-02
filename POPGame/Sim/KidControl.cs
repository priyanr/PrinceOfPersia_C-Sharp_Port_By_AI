using POPGame.Data;
using POPGame.Input;

namespace POPGame.Sim;

/// <summary>
/// The player control layer — the equivalent of PLAYERCTRL in CTRL.S.
///
/// It never moves the character itself. Its only job is to decide, from the input
/// and the character's current action/frame, which sequence to jump to; all motion
/// then comes out of the sequence table via <see cref="SeqRunner"/> and out of
/// <see cref="Physics"/>.
/// </summary>
public sealed class KidControl
{
    private readonly SeqRunner _seq;
    private readonly RoomView _view;

    public KidControl(SeqRunner seq, RoomView view)
    {
        _seq = seq;
        _view = view;
    }

    /// <summary>
    /// Whether there is a ledge to catch by jumping up. jumphangMed runs straight into
    /// the hang sequence, from which Up climbs; jumpup just bumps the ceiling and
    /// drops back down.
    ///
    /// This is CHECKLEDGE (CTRLSUBS.S:1814), and the shape of it is easy to get
    /// backwards: the cell *directly above* must be CLEAR to jump into, and the cell
    /// above and **in front** — one column in the facing direction — is the ledge you
    /// catch. Testing for a floor directly overhead instead finds no ledge anywhere.
    ///
    /// Because <see cref="RoomView.Tile"/> resolves through the MAP links, "in front"
    /// works across a room boundary: standing at the left edge of a room facing left,
    /// the ledge caught is column 9 of the room next door, and climbing it carries the
    /// character into that room. That is how level 1 gets from room 2 to room 6.
    /// </summary>
    private bool CanGrabAbove(CharState c)
    {
        int above = c.Row - 1;

        // Clear overhead? A solid block is never jumped into even though CMPSPACE
        // calls it floorless, and a floorless panel is in the way when facing right.
        var over = _view.Tile(c.Room, above, c.BlockX);
        if (over == TileId.Block) return false;
        if (over == TileId.PanelWOF && c.FacingRight) return false;
        if (RoomView.HasFloor(over)) return false;

        // ...and a solid floorpiece with an exposed edge in front to catch.
        int ahead = c.BlockX + c.FaceSign;
        var ledge = _view.Tile(c.Room, above, ahead);
        if (ledge == TileId.Loose && _view.Spec(c.Room, above, ahead) != 0) return false;
        if (ledge == TileId.PanelWF && !c.FacingRight) return false;
        return RoomView.HasFloor(ledge);
    }

    /// <summary>Standing at the lip of a drop, facing out over it.</summary>
    private bool AtLedgeEdge(CharState c)
    {
        int ahead = c.BlockX + c.FaceSign;
        return !_view.FloorAt(c.Room, c.Row, ahead)
            && _view.FloorAt(c.Room, c.Row + 1, ahead);
    }

    // The original disambiguates states by action plus frame number, because a
    // single action value is shared by several sequences.
    private static bool IsStanding(CharState c) => c.Action == CharAction.Stand && c.Frame == 15;
    private static bool IsRunning(CharState c) => c.Action == CharAction.RunJump && c.Frame is >= 1 and <= 14;
    private static bool IsTurning(CharState c) => c.Frame is >= 45 and <= 52;
    private static bool IsHanging(CharState c) =>
        c.Action is CharAction.HangClimb or CharAction.HangStraight;

    // softland and stoop both park on frame 109 and hold there until the player does
    // something. Other sequences (medland) pass through 109 without stopping, so the
    // sequence has to be part of the test.
    private static bool IsCrouching(CharState c) =>
        c.Frame == 109 && c.CurrentSeq is Seq.SoftLand or Seq.Stoop;

    public void Update(CharState ch, InputState input)
    {
        if (!ch.Alive) return;

        bool fwd  = input.Right == ch.FacingRight && (input.Right || input.Left);
        bool back = input.Left  == ch.FacingRight && (input.Right || input.Left);

        if (IsHanging(ch))
        {
            if (input.Up) _seq.Start(ch, Seq.ClimbUp);
            else if (input.Down || !input.Action) _seq.Start(ch, Seq.HangDrop);
            return;
        }

        if (ch.Action is CharAction.InMidair or CharAction.InFreefall)
            return;     // no steering mid-air; Physics handles the ledge grab

        if (IsCrouching(ch))
        {
            // Down holds the crouch; anything else gets back on his feet.
            if (input.Up || input.Left || input.Right) _seq.Start(ch, Seq.StandUp);
            return;
        }

        if (IsRunning(ch))
        {
            if (input.Up) _seq.Start(ch, Seq.RunJump);
            else if (input.Down) _seq.Start(ch, Seq.RDiveRoll);
            else if (back) _seq.Start(ch, Seq.RunTurn);
            else if (!fwd) _seq.Start(ch, Seq.RunStop);
            return;
        }

        if (!IsStanding(ch)) return;    // mid-animation: let the sequence finish

        if (input.Up)
        {
            if (fwd) _seq.Start(ch, Seq.StandJump);
            else _seq.Start(ch, CanGrabAbove(ch) ? Seq.JumpHangMed : Seq.JumpUp);
        }
        else if (input.Down)
        {
            // At the lip of a drop, Down lowers him over the edge instead of crouching.
            _seq.Start(ch, AtLedgeEdge(ch) ? Seq.ClimbDown : Seq.Stoop);
        }
        else if (back)
        {
            _seq.Start(ch, Seq.Turn);
        }
        else if (fwd)
        {
            // Shift is the "careful" modifier: a single measured step instead of a run.
            _seq.Start(ch, input.Action ? Seq.StepFwd1 : Seq.StartRun);
        }
    }
}
