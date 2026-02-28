using POPGame.Engine;
using POPGame.Input;

namespace POPGame.Characters;

/// <summary>
/// Resolves sword strike / block interactions between player and guards.
/// Mirrors the Apple II CHARCOLIDE / GDSTRIKE / KIDSTRIKE routines.
/// </summary>
public class CombatSystem
{
    private readonly Random _rng = new();

    public void Check(GameState state, InputState input)
    {
        var kid = state.Player;
        if (!kid.IsAlive) return;

        foreach (var g in state.Guards)
        {
            if (!g.IsAlive) continue;
            if (g.Screen != kid.Screen) continue;

            int dist = Math.Abs(kid.BlockX - g.BlockX);
            bool inStrikeRange = dist >= Constants.StrikeRange1 / 10 &&
                                 dist <= Constants.StrikeRange2 / 10;

            // Enter en-garde if close enough and kid has sword
            if (dist <= Constants.SwordThres / 10 && kid.HasSword)
            {
                kid.InFight = true;
                kid.FaceDir = g.BlockX > kid.BlockX ? 1 : -1;

                if (kid.Action != PlayerAction.Strike &&
                    kid.Action != PlayerAction.Blocking &&
                    kid.Action != PlayerAction.Dead)
                    kid.Action = PlayerAction.EnGarde;
            }
            else
            {
                kid.InFight = false;
            }

            // Player strikes on Action button
            if (input.FreshAction && kid.InFight && kid.HasSword)
            {
                kid.Action = PlayerAction.Strike;
                ResolvePlayerStrike(kid, g, inStrikeRange, state);
            }

            // Guard strikes
            if (g.GdAction == GuardState.Strike && inStrikeRange)
                ResolveGuardStrike(g, kid, state);

            // Reset strike state next frame
            if (kid.Action == PlayerAction.Strike) kid.Action = PlayerAction.EnGarde;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void ResolvePlayerStrike(CharState kid, CharState g, bool inRange, GameState state)
    {
        if (!inRange) return;

        // Guard may block
        int prog = Math.Clamp(g.GuardProg, 0, Constants.BlockProb.Length - 1);
        bool blocked = g.GdAction == GuardState.Blocking ||
                       _rng.Next(256) < Constants.BlockProb[prog];

        if (blocked)
        {
            state.SetStatus("Blocked!", 15);
        }
        else
        {
            g.TakeDamage(1);
            g.ApplyStun(Constants.StunTime);
            state.SetStatus(g.IsAlive ? "Hit!" : "Guard defeated!", 25);
        }
    }

    private void ResolveGuardStrike(CharState g, CharState kid, GameState state)
    {
        // Kid may block with Up key — handled by PlayerAction.Blocking
        bool blocked = kid.Action == PlayerAction.Blocking;

        if (blocked)
        {
            state.SetStatus("You blocked!", 15);
        }
        else if (kid.GraceFrames == 0)
        {
            kid.TakeDamage(1);
            kid.ApplyStun(Constants.StunTime);
            state.SetStatus(kid.IsAlive ? "You were hit!" : "You are dead!", 30);
        }
    }
}
