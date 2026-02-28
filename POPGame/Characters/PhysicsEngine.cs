using POPGame.Data;
using POPGame.Engine;
using POPGame.World;

namespace POPGame.Characters;

/// <summary>
/// Applies gravity, floor collision, ledge-grab, and screen-transition logic
/// to a character each tick.
/// </summary>
public class PhysicsEngine
{
    private readonly Level         _level;
    private readonly HazardSystem  _hazards;

    public PhysicsEngine(Level level, HazardSystem hazards)
    {
        _level   = level;
        _hazards = hazards;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Main update — call once per tick per character
    // ─────────────────────────────────────────────────────────────────────────

    public void Update(CharState ch, GameState state)
    {
        if (!ch.IsAlive) return;
        if (ch.StunFrames > 0) { ch.StunFrames--; return; }
        if (ch.GraceFrames > 0) ch.GraceFrames--;

        switch (ch.Action)
        {
            case PlayerAction.Jumping:
            case PlayerAction.FreeFall:
                UpdateAirborne(ch, state);
                break;

            case PlayerAction.Hanging:
                // No gravity; hanging is handled by PlayerController
                break;

            default:
                UpdateGrounded(ch, state);
                break;
        }

        ClampPosition(ch);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Ground-check helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>True if there is a solid floor tile at the row below the character.</summary>
    public bool HasFloorBelow(CharState ch)
    {
        int s0      = ch.Screen - 1;
        int nextRow = ch.BlockY + 1;

        if (nextRow >= Level.Rows)
        {
            // Check if screen below exists
            int below = _level.Below(ch.Screen);
            if (below == 0) return true; // treat edge of world as solid
            int s0b = below - 1;
            return IsFloorTile(_level.GetTileId(s0b, 0, ch.BlockX));
        }

        return IsFloorTile(_level.GetTileId(s0, nextRow, ch.BlockX));
    }

    /// <summary>True if the cell directly beneath the character is solid.</summary>
    public bool HasFloorAt(CharState ch)
    {
        int s0  = ch.Screen - 1;
        int row = ch.BlockY;
        return IsFloorTile(_level.GetTileId(s0, row, ch.BlockX));
    }

    /// <summary>True if tile in front of character (same row) blocks movement.</summary>
    public bool IsBlocked(CharState ch)
    {
        int nextCol = ch.BlockX + ch.FaceDir;
        if (nextCol < 0 || nextCol >= Level.Cols)
        {
            // Screen edge — check if neighbour screen exists
            int neighbour = ch.FaceDir > 0 ? _level.Right(ch.Screen) : _level.Left(ch.Screen);
            return neighbour == 0;
        }
        int s0 = ch.Screen - 1;
        TileId t = _level.GetTileId(s0, ch.BlockY, nextCol);

        // Gates are only solid when closed (spec at max)
        if (t == TileId.Gate)
        {
            byte spec = _level.GetSpec(s0, ch.BlockY, nextCol);
            return spec >= Constants.GMaxVal;
        }

        return IsSolidTile(t);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Screen transition
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// If character has stepped off the edge of the current screen, move them
    /// to the adjacent screen.  Returns true if transition occurred.
    /// </summary>
    public bool CheckScreenTransition(CharState ch)
    {
        if (ch.BlockX < 0)
        {
            int left = _level.Left(ch.Screen);
            if (left > 0) { ch.Screen = left; ch.BlockX = Level.Cols - 1; return true; }
            ch.BlockX = 0;
        }
        else if (ch.BlockX >= Level.Cols)
        {
            int right = _level.Right(ch.Screen);
            if (right > 0) { ch.Screen = right; ch.BlockX = 0; return true; }
            ch.BlockX = Level.Cols - 1;
        }

        if (ch.BlockY < 0)
        {
            int above = _level.Above(ch.Screen);
            if (above > 0) { ch.Screen = above; ch.BlockY = Level.Rows - 1; return true; }
            ch.BlockY = 0;
        }
        else if (ch.BlockY >= Level.Rows)
        {
            int below = _level.Below(ch.Screen);
            if (below > 0) { ch.Screen = below; ch.BlockY = 0; return true; }
            ch.BlockY = Level.Rows - 1;
        }

        return false;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void UpdateGrounded(CharState ch, GameState state)
    {
        // Check if floor has disappeared under character
        if (!HasFloorAt(ch))
        {
            ch.Action  = PlayerAction.FreeFall;
            ch.OnFloor = false;
            ch.YVel    = 0;
        }
        else
        {
            ch.OnFloor = true;
            ch.YVel    = 0;
            // Check pressure plate
            CheckPressurePlate(ch, state);
            // Check loose floor
            CheckLooseFloor(ch, state);
        }
    }

    private void UpdateAirborne(CharState ch, GameState state)
    {
        ch.OnFloor = false;
        ch.YVel    = Math.Min(ch.YVel + Constants.FFAccel, Constants.FFTermVel);

        if (ch.YVel > 0)
        {
            // Check if current position has floor (e.g. just arrived at floor row)
            if (HasFloorAt(ch))
            {
                Land(ch, state);
                return;
            }
            // Move down one row
            ch.BlockY++;
            CheckScreenTransition(ch);
            // Check new position
            if (HasFloorAt(ch))
            {
                Land(ch, state);
                return;
            }
        }
        else if (ch.YVel < 0)
        {
            // Rising — move up one row when velocity crosses row boundary
            // Simplified: move up one row every 2 ticks of negative velocity
            if (ch.YVel <= -Constants.VertDist)
            {
                ch.BlockY--;
                if (ch.BlockY < 0) ch.BlockY = 0;
            }

            // Check ledge grab
            if (ch.YVel >= -Constants.GrabSpeed)
                CheckLedgeGrab(ch);
        }
    }

    private void Land(CharState ch, GameState state)
    {
        int vel = ch.YVel;
        ch.YVel    = 0;
        ch.OnFloor = true;

        if (vel >= Constants.DeathVelocity)
        {
            ch.TakeDamage(ch.Life); // fatal
            ch.Action = PlayerAction.Dead;
            if (ch.GuardIndex < 0) state.SetStatus("You fell to your death!", 40);
        }
        else if (vel >= Constants.OofVelocity)
        {
            ch.TakeDamage(1);
            ch.Action = PlayerAction.OnGround;
            if (ch.GuardIndex < 0) state.SetStatus("Ouch!", 20);
        }
        else
        {
            ch.Action = PlayerAction.OnGround;
        }

        CheckPressurePlate(ch, state);
    }

    private void CheckLedgeGrab(CharState ch)
    {
        int s0      = ch.Screen - 1;
        int checkRow = ch.BlockY - 1;
        if (checkRow < 0) return;

        // Look for a ledge tile to the side in the direction of movement
        // (or directly above current position)
        if (_level.GetTileId(s0, checkRow, ch.BlockX) == TileId.Floor ||
            _level.GetTileId(s0, checkRow, ch.BlockX) == TileId.PanelWF)
        {
            ch.Action = PlayerAction.Hanging;
            ch.BlockY = checkRow;
            ch.YVel   = 0;
        }
    }

    private void CheckPressurePlate(CharState ch, GameState state)
    {
        int s0   = ch.Screen - 1;
        TileId t = _level.GetTileId(s0, ch.BlockY, ch.BlockX);
        if (t == TileId.PressPlate || t == TileId.UPressPlate || t == TileId.DPressPlate)
        {
            int cell = ch.BlockY * Level.Cols + ch.BlockX;
            _hazards.TriggerPressPlate(ch.Screen, cell);
        }
    }

    private void CheckLooseFloor(CharState ch, GameState state)
    {
        int s0   = ch.Screen - 1;
        TileId t = _level.GetTileId(s0, ch.BlockY, ch.BlockX);
        if (t == TileId.Loose)
        {
            int cell = ch.BlockY * Level.Cols + ch.BlockX;
            _hazards.TriggerLoose(ch.Screen, cell);
        }
    }

    private void ClampPosition(CharState ch)
    {
        ch.BlockX = Math.Clamp(ch.BlockX, 0, Level.Cols - 1);
        ch.BlockY = Math.Clamp(ch.BlockY, 0, Level.Rows - 1);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tile classification
    // ─────────────────────────────────────────────────────────────────────────

    private static bool IsFloorTile(TileId t) => t switch
    {
        TileId.Space      => false,
        TileId.PillarTop  => false,
        TileId.PanelWOF   => false,  // "panel without floor"
        TileId.ArchTop1   => false,
        TileId.ArchTop2   => false,
        TileId.ArchTop3   => false,
        TileId.ArchTop4   => false,
        _                 => true,   // all other tiles have a walkable floor surface
    };

    private static bool IsSolidTile(TileId t) => t switch
    {
        TileId.Block    => true,
        TileId.Gate     => true,  // closed gate blocks; open gate (spec=0) checked at call site
        TileId.Posts    => true,
        TileId.PillarBot => true,
        _               => false,
    };
}
