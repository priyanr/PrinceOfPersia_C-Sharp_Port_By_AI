using POPGame.Data;
using POPGame.Engine;
using POPGame.Input;
using POPGame.World;

namespace POPGame.Characters;

/// <summary>
/// Translates InputState into character actions and moves the player character
/// through the state machine each tick.
/// Mirrors the Apple II PLAYERCTRL / GenCtrl / STANDING / RUNNING routines.
/// </summary>
public class PlayerController
{
    private readonly PhysicsEngine _physics;
    private readonly HazardSystem  _hazards;
    private readonly Level         _level;
    private readonly Random        _rng = new();

    public PlayerController(PhysicsEngine physics, HazardSystem hazards, Level level)
    {
        _physics = physics;
        _hazards = hazards;
        _level   = level;
    }

    public void Update(GameState state, InputState input)
    {
        var ch = state.Player;
        if (!ch.IsAlive) return;

        // Decrement stun / grace via physics
        _physics.Update(ch, state);
        if (ch.StunFrames > 0) return;  // physics already counted stun down

        switch (ch.Action)
        {
            case PlayerAction.OnGround:
            case PlayerAction.Running:
            case PlayerAction.Turning:
                HandleGround(ch, state, input);
                break;

            case PlayerAction.Jumping:
            case PlayerAction.FreeFall:
                HandleAirborne(ch, state, input);
                break;

            case PlayerAction.Hanging:
                HandleHanging(ch, state, input);
                break;

            case PlayerAction.Crouching:
                HandleCrouching(ch, state, input);
                break;

            case PlayerAction.EnGarde:
            case PlayerAction.Strike:
            case PlayerAction.Blocking:
                // Combat handled by CombatSystem; player can still retreat/advance
                HandleFight(ch, state, input);
                break;

            case PlayerAction.ClimbUp:
                DoClimbUp(ch);
                break;

            case PlayerAction.ClimbDown:
                DoClimbDown(ch);
                break;
        }

        _physics.CheckScreenTransition(ch);

        // Open exit check
        if (!state.ExitOpen && AllGuardsOnScreenDead(state))
            state.ExitOpen = true;

        CheckItemPickup(ch, state);
        CheckExitTile(ch, state);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // State handlers
    // ─────────────────────────────────────────────────────────────────────────

    private void HandleGround(CharState ch, GameState state, InputState input)
    {
        if (input.Up)
        {
            // Jump
            ch.Action = PlayerAction.Jumping;
            ch.YVel   = Constants.JumpInitVel;
            ch.OnFloor = false;
            return;
        }

        if (input.Down)
        {
            ch.Action = PlayerAction.Crouching;
            return;
        }

        bool forward = (input.Right && ch.FaceDir > 0) || (input.Left && ch.FaceDir < 0);
        bool back    = (input.Left  && ch.FaceDir > 0) || (input.Right && ch.FaceDir < 0);

        if (back)
        {
            // Turn around
            ch.FaceDir = -ch.FaceDir;
            ch.Action  = PlayerAction.Turning;
            return;
        }

        if (forward)
        {
            // Step forward if not blocked
            if (!_physics.IsBlocked(ch))
            {
                ch.BlockX += ch.FaceDir;
                ch.Action  = PlayerAction.Running;
                _physics.CheckScreenTransition(ch);
            }
            else
            {
                // Bump into wall — stay OnGround
                ch.Action = PlayerAction.OnGround;
            }
            return;
        }

        ch.Action = PlayerAction.OnGround;
    }

    private void HandleAirborne(CharState ch, GameState state, InputState input)
    {
        // Horizontal movement during jump
        if (ch.Action == PlayerAction.Jumping)
        {
            bool forward = (input.Right && ch.FaceDir > 0) || (input.Left && ch.FaceDir < 0);
            if (forward && !_physics.IsBlocked(ch) && ch.YVel < -5)
            {
                ch.BlockX += ch.FaceDir;
                _physics.CheckScreenTransition(ch);
            }
        }
        // Gravity handled by PhysicsEngine.Update called above
    }

    private void HandleHanging(CharState ch, GameState state, InputState input)
    {
        if (input.Up)
        {
            ch.Action = PlayerAction.ClimbUp;
            return;
        }

        if (input.Down || (input.Action && ch.FaceDir != 0))
        {
            // Drop
            ch.Action  = PlayerAction.FreeFall;
            ch.YVel    = 0;
            ch.BlockY++;
            return;
        }
    }

    private void HandleCrouching(CharState ch, GameState state, InputState input)
    {
        if (!input.Down)
        {
            ch.Action = PlayerAction.OnGround;
            return;
        }

        // Check if there is a ledge below to climb down
        int s0 = ch.Screen - 1;
        if (ch.BlockY + 1 < Level.Rows)
        {
            TileId below = _level.GetTileId(s0, ch.BlockY + 1, ch.BlockX);
            if (below == TileId.Space || below == TileId.PanelWOF)
            {
                ch.Action  = PlayerAction.ClimbDown;
                return;
            }
        }
    }

    private void HandleFight(CharState ch, GameState state, InputState input)
    {
        // Retreat
        if ((input.Left && ch.FaceDir > 0) || (input.Right && ch.FaceDir < 0))
        {
            if (!_physics.IsBlocked(ch))
                ch.BlockX -= ch.FaceDir;
        }
        // Advance
        if ((input.Right && ch.FaceDir > 0) || (input.Left && ch.FaceDir < 0))
        {
            if (!_physics.IsBlocked(ch))
                ch.BlockX += ch.FaceDir;
        }

        // Strike handled by CombatSystem on Action button
        ch.Action = PlayerAction.EnGarde;
    }

    private void DoClimbUp(CharState ch)
    {
        // Move up one row over several ticks (simplified: instant)
        ch.BlockY--;
        if (ch.BlockY < 0) ch.BlockY = 0;
        ch.Action  = PlayerAction.OnGround;
        ch.OnFloor = true;
        ch.YVel    = 0;
        _physics.CheckScreenTransition(ch);
    }

    private void DoClimbDown(CharState ch)
    {
        ch.BlockY++;
        ch.Action  = PlayerAction.Hanging;
        _physics.CheckScreenTransition(ch);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Item / exit checks
    // ─────────────────────────────────────────────────────────────────────────

    private void CheckItemPickup(CharState ch, GameState state)
    {
        int s0   = ch.Screen - 1;
        TileId t = _level.GetTileId(s0, ch.BlockY, ch.BlockX);

        if (t == TileId.Sword && !ch.HasSword)
        {
            ch.HasSword = true;
            int cell = ch.BlockY * Level.Cols + ch.BlockX;
            _level.SetTileType(s0, ch.BlockY, ch.BlockX, TileId.Space);
            state.SetStatus("You picked up the sword!", 30);
        }

        if (t == TileId.Flask)
        {
            ch.Life = Math.Min(ch.Life + 1, ch.MaxLife + 1);
            ch.MaxLife = ch.Life;
            _level.SetTileType(s0, ch.BlockY, ch.BlockX, TileId.Space);
            state.SetStatus("Health restored!", 30);
        }
    }

    private void CheckExitTile(CharState ch, GameState state)
    {
        int s0   = ch.Screen - 1;
        TileId t = _level.GetTileId(s0, ch.BlockY, ch.BlockX);

        if ((t == TileId.Exit || t == TileId.Exit2) && state.ExitOpen)
        {
            if (state.LevelIndex >= Constants.LastPlayableLevel)
                state.Phase = GamePhase.YouWin;
            else
                state.Phase = GamePhase.NextLevel;
        }
    }

    private static bool AllGuardsOnScreenDead(GameState state)
    {
        foreach (var g in state.Guards)
            if (g.Screen == state.Player.Screen && g.IsAlive) return false;
        return true;
    }
}
