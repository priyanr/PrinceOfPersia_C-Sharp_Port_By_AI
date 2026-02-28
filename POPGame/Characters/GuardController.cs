using POPGame.Engine;

namespace POPGame.Characters;

/// <summary>
/// Guard AI: patrol, alert, en garde, strike, block.
/// Mirrors the Apple II GUARDCTRL / GDACT routines with probability tables.
/// </summary>
public class GuardController
{
    private readonly PhysicsEngine _physics;
    private readonly Random        _rng = new();

    public GuardController(PhysicsEngine physics) => _physics = physics;

    public void Update(GameState state)
    {
        var kid = state.Player;

        for (int i = 0; i < state.Guards.Length; i++)
        {
            var g = state.Guards[i];
            if (!g.IsAlive) continue;

            _physics.Update(g, state);
            if (g.StunFrames > 0) continue;
            if (g.GraceFrames > 0) g.GraceFrames--;

            if (g.Screen != kid.Screen)
            {
                Patrol(g, state);
                continue;
            }

            int dist = Math.Abs(kid.BlockX - g.BlockX);

            if (dist > Constants.SwordThres / 10)        // beyond sword range
            {
                Alert(g, kid);
            }
            else if (dist > Constants.StrikeRange2 / 10) // approaching range
            {
                EnGardeAdvance(g, kid, state);
            }
            else                                           // in strike range
            {
                InRangeTick(g, kid, state);
            }

            _physics.CheckScreenTransition(g);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void Patrol(CharState g, GameState state)
    {
        g.GdAction    = GuardState.Patrol;
        g.PatrolTimer++;
        if (g.PatrolTimer < 20) return;
        g.PatrolTimer = 0;

        // Random walk: advance or stand
        if (_rng.Next(2) == 0 && !_physics.IsBlocked(g))
            g.BlockX += g.FaceDir;
    }

    private void Alert(CharState g, CharState kid)
    {
        g.GdAction = GuardState.Alert;
        // Face toward kid and walk
        g.FaceDir = kid.BlockX > g.BlockX ? 1 : -1;
        if (!_physics.IsBlocked(g))
            g.BlockX += g.FaceDir;
    }

    private void EnGardeAdvance(CharState g, CharState kid, GameState state)
    {
        g.GdAction = GuardState.EnGarde;
        g.FaceDir  = kid.BlockX > g.BlockX ? 1 : -1;

        if (g.RefractLeft > 0) { g.RefractLeft--; return; }

        int prog = Math.Clamp(g.GuardProg, 0, Constants.AdvProb.Length - 1);
        if (_rng.Next(256) < Constants.AdvProb[prog] && !_physics.IsBlocked(g))
        {
            g.BlockX += g.FaceDir;
            g.RefractLeft = Constants.RefractTimer[prog];
        }
    }

    private void InRangeTick(CharState g, CharState kid, GameState state)
    {
        g.GdAction = GuardState.EnGarde;
        g.FaceDir  = kid.BlockX > g.BlockX ? 1 : -1;

        if (g.RefractLeft > 0) { g.RefractLeft--; return; }

        int prog = Math.Clamp(g.GuardProg, 0, Constants.StrikeProb.Length - 1);
        int roll = _rng.Next(256);

        // Kid striking — try to block
        if (kid.Action == PlayerAction.Strike && _rng.Next(256) < Constants.BlockProb[prog])
        {
            g.GdAction    = GuardState.Blocking;
            g.RefractLeft = Constants.RefractTimer[prog];
            return;
        }

        // Try to strike kid
        if (roll < Constants.StrikeProb[prog])
        {
            g.GdAction    = GuardState.Strike;
            g.RefractLeft = Constants.RefractTimer[prog];
            // Damage resolved in CombatSystem
        }
    }
}
