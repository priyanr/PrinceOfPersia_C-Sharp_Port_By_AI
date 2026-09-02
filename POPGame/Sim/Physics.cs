namespace POPGame.Sim;

/// <summary>
/// Gravity, floor/ceiling contact, wall contact and room transitions — the part of
/// MOVER.S / COLL.S that runs after the sequence has posed the character.
/// </summary>
public sealed class Physics
{
    public const int FallAccel = 3;
    public const int TermVel = 33;

    /// <summary>Fall speed at impact that costs a hit point.</summary>
    public const int OofVel = 22;
    /// <summary>Fall speed at impact that kills outright.</summary>
    public const int KillVel = 33;

    private readonly RoomView _view;
    private readonly SeqRunner _seq;

    public Physics(RoomView view, SeqRunner seq)
    {
        _view = view;
        _seq = seq;
    }

    public void Update(CharState ch, SeqEffects fx)
    {
        if (!ch.Alive) return;

        int prevY = ch.Y;

        if (ch.Action == CharAction.InFreefall)
        {
            ch.YVel = Math.Min(ch.YVel + FallAccel, TermVel);
            ch.Y += ch.YVel;
            ch.AddX(ch.XVel);
            ch.FallCount++;
        }

        Transition(ch);

        if (ch.Action == CharAction.InFreefall)
            CheckLanding(ch, prevY, fx);
        else if (ch.Action is CharAction.Stand or CharAction.RunJump or CharAction.Turn)
            CheckGroundLost(ch);

        CheckWalls(ch);
        Transition(ch);
    }

    /// <summary>Moves the character between rooms when it leaves the current one.</summary>
    private void Transition(CharState ch)
    {
        for (int guard = 0; guard < 4; guard++)
        {
            if (ch.X < Coord.ScrnLeft)
            {
                int n = _view.Level.Left(ch.Room);
                if (n == 0) { ch.X = Coord.ScrnLeft; break; }
                ch.Room = n; ch.X += Coord.ScrnWidth;
            }
            else if (ch.X >= Coord.ScrnLeft + Coord.ScrnWidth)
            {
                int n = _view.Level.Right(ch.Room);
                if (n == 0) { ch.X = Coord.ScrnLeft + Coord.ScrnWidth - 1; break; }
                ch.Room = n; ch.X -= Coord.ScrnWidth;
            }
            else if (ch.Row > 2)
            {
                int n = _view.Level.Below(ch.Room);
                if (n == 0) break;
                ch.Room = n; ch.Row -= Coord.Rows; ch.Y -= Coord.Rows * Coord.BlockHeight;
            }
            else if (ch.Row < 0)
            {
                int n = _view.Level.Above(ch.Room);
                if (n == 0) break;
                ch.Room = n; ch.Row += Coord.Rows; ch.Y += Coord.Rows * Coord.BlockHeight;
            }
            else break;
        }
    }

    /// <summary>
    /// While falling, look for the first floor the character's feet crossed this
    /// tick and land on it. Rows are scanned downward from where the fall started.
    /// </summary>
    private void CheckLanding(CharState ch, int prevY, SeqEffects fx)
    {
        int col = ch.BlockX;

        for (int row = ch.Row; row <= Coord.Rows; row++)
        {
            int fy = Coord.FloorY(row);
            if (prevY > fy || ch.Y < fy) continue;
            if (!_view.FloorAt(ch.Room, row, col)) continue;

            ch.Y = fy;
            ch.Row = row;
            Land(ch, fx);
            return;
        }

        // Still airborne: keep the logical row in step with the pixel position.
        ch.Row = ch.RowFromY;
    }

    private void Land(CharState ch, SeqEffects fx)
    {
        int vel = ch.YVel;
        ch.YVel = 0;
        ch.XVel = 0;
        ch.FallCount = 0;

        if (vel >= KillVel)
        {
            ch.Hp = 0;
            ch.Alive = false;
            fx.Died = true;
            _seq.Start(ch, Seq.HardLand);
        }
        else if (vel >= OofVel)
        {
            ch.Hp--;
            if (ch.Hp <= 0) { ch.Alive = false; fx.Died = true; }
            _seq.Start(ch, Seq.HardLand);
        }
        else if (vel >= 12)
        {
            _seq.Start(ch, Seq.MedLand);
        }
        else
        {
            _seq.Start(ch, Seq.SoftLand);
        }
    }

    /// <summary>Grounded characters that walk off an edge start falling.</summary>
    private void CheckGroundLost(CharState ch)
    {
        int col = ch.BlockX;
        if (_view.FloorAt(ch.Room, ch.Row, col))
        {
            // Sequences nudge Y with chy as they play; once the character is simply
            // standing, pull it back onto the row's centre plane so the small offsets
            // do not accumulate across a level.
            if (ch.Action == CharAction.Stand && ch.Frame == 15)
                ch.Y = Coord.FloorY(ch.Row);
            return;
        }

        ch.YVel = 0;
        ch.XVel = 0;
        ch.FallCount = 0;
        _seq.Start(ch, Seq.StepFall);
    }

    /// <summary>
    /// First and last frame of the climbup animation, which lifts the character over
    /// the lip of the ledge it is pulling itself onto (CLAUDE.md: climbup is 135..149).
    /// </summary>
    private const int ClimbFirstFrame = 135, ClimbLastFrame = 148;

    /// <summary>
    /// Whether the character is momentarily allowed through a barrier.
    ///
    /// COLLISIONS (COLL.S:409) waives the check while hanging or climbing, and
    /// CHECKBARR (COLL.S:80) waives it while turning. Without this a character that
    /// has just pulled itself onto a ledge is standing inside a solid block, so the
    /// push-out below shoves it off the ledge and it falls straight back down.
    /// </summary>
    private static bool PassesThroughBarriers(CharState ch) =>
        ch.Action is CharAction.HangClimb or CharAction.HangStraight or CharAction.Turn
        || ch.Frame is >= ClimbFirstFrame and <= ClimbLastFrame;

    /// <summary>
    /// Stops the character at a wall in the direction it is moving. Walking into one
    /// knocks the character back, which is what the bump sequences are for; without
    /// this a run against a wall just plays on the spot forever.
    /// </summary>
    private void CheckWalls(CharState ch)
    {
        if (PassesThroughBarriers(ch)) return;

        int col = ch.BlockX;
        if (!_view.IsBarrier(ch.Room, ch.Row, col)) return;

        // Push back out of the solid block it just entered.
        int edge = Coord.BlockEdge(col);
        int before = ch.X;
        ch.X = ch.Face < 0
            ? edge + Coord.BlockWidth   // moving left: sit against its right edge
            : edge - 1;                 // moving right: sit against its left edge

        if (ch.X == before) return;     // already resting against it

        if (ch.Action == CharAction.InFreefall) _seq.Start(ch, Seq.BumpFall);
        else if (ch.CurrentSeq is not (Seq.Bump or Seq.HardBump)) _seq.Start(ch, Seq.Bump);
    }
}
