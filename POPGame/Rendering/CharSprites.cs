using System.Numerics;
using Raylib_cs;
using POPGame.Characters;

namespace POPGame.Rendering;

/// <summary>
/// Draws the Prince and Guards. When authentic Apple II textures are loaded
/// (via CharAtlas), uses those; otherwise falls back to stick-figure rectangles.
/// </summary>
public static class CharSprites
{
    private const int TileW = 32;
    private const int TileH = 72;

    public static void DrawPrince(CharState ch, int px, int py)
    {
        bool flip = ch.FaceDir > 0;  // Apple II sprites face LEFT natively

        if (CharAtlas.IsLoaded)
        {
            var tex = CharAtlas.GetKidTexture(ch.Action);
            if (tex.Id != 0)
            {
                DrawCharTexture(tex, px, py, flip);
                return;
            }
        }

        DrawPlayerPose(ch.Action, flip, px, py);
    }

    public static void DrawGuard(CharState g, int px, int py)
    {
        bool flip = g.FaceDir > 0;  // Apple II sprites face LEFT natively

        if (CharAtlas.IsLoaded)
        {
            var tex = CharAtlas.GetGuardTexture(g.GdAction);
            if (tex.Id != 0)
            {
                DrawCharTexture(tex, px, py, flip);
                return;
            }
        }

        DrawGuardPose(g.GdAction, g.IsAlive, flip, px, py);
    }

    /// <summary>
    /// Draw a character texture scaled to fit within the tile cell, centered horizontally,
    /// anchored to the bottom of the cell. Supports horizontal flip for facing direction.
    /// </summary>
    private static void DrawCharTexture(Texture2D tex, int px, int py, bool flip)
    {
        // Use tile-consistent scale factor (Apple II native → screen)
        float scale = (float)TileH / TileAtlas.AppleTileHeight; // 72/63 ≈ 1.143

        float drawW = tex.Width * scale;
        float drawH = tex.Height * scale;

        // Center horizontally, anchor to bottom of tile
        float drawX = px + (TileW - drawW) / 2f;
        float drawY = py + TileH - drawH;

        // Source rect: negative width = horizontal flip
        float srcW = flip ? -tex.Width : tex.Width;

        Raylib.DrawTexturePro(
            tex,
            new Rectangle(0, 0, srcW, tex.Height),
            new Rectangle(drawX, drawY, drawW, drawH),
            Vector2.Zero, 0f, Color.White);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Fallback stick-figure rendering
    // ─────────────────────────────────────────────────────────────────────────

    private static void B(int lx, int ly, int lw, int lh, Color c, int px, int py, bool flip)
    {
        int drawX = flip ? (TileW - lx - lw) : lx;
        Raylib.DrawRectangle(px + drawX, py + ly, lw, lh, c);
    }

    private static void DrawPlayerPose(PlayerAction action, bool flip, int px, int py)
    {
        Color body = action == PlayerAction.Dead ? EgaPalette.DarkRed : EgaPalette.White;

        switch (action)
        {
            case PlayerAction.Dead:
                DrawDeadPose(body, flip, px, py); break;
            case PlayerAction.Crouching:
                DrawCrouchingPose(body, flip, px, py); break;
            case PlayerAction.Hanging:
                DrawHangingPose(body, flip, px, py); break;
            case PlayerAction.ClimbUp:
            case PlayerAction.ClimbDown:
                DrawClimbPose(body, flip, px, py); break;
            case PlayerAction.EnGarde:
                DrawEnGardePose(body, flip, px, py); break;
            case PlayerAction.Strike:
                DrawStrikePose(body, flip, px, py); break;
            case PlayerAction.Running:
                DrawRunningPose(body, flip, px, py); break;
            default:
                DrawStandingPose(body, flip, px, py); break;
        }
    }

    private static void DrawGuardPose(GuardState action, bool alive, bool flip, int px, int py)
    {
        bool dead = !alive || action == GuardState.Dead;
        Color body = dead ? EgaPalette.DarkRed : EgaPalette.Red;

        switch (action)
        {
            case GuardState.Dead:
                DrawDeadPose(body, flip, px, py); break;
            case GuardState.EnGarde:
                DrawEnGardePose(body, flip, px, py); break;
            case GuardState.Strike:
                DrawStrikePose(body, flip, px, py); break;
            default:
                DrawStandingPose(body, flip, px, py); break;
        }
    }

    // ─── Poses ───────────────────────────────────────────────────────────────

    private static void DrawStandingPose(Color c, bool flip, int px, int py)
    {
        B(13,  4, 6,  6, c, px, py, flip);
        B(15, 10, 2,  4, c, px, py, flip);
        B(13, 14, 6, 18, c, px, py, flip);
        B(19, 18, 10, 3, c, px, py, flip);
        B( 3, 18, 10, 3, c, px, py, flip);
        B(17, 32,  4, 20, c, px, py, flip);
        B(11, 32,  4, 20, c, px, py, flip);
    }

    private static void DrawRunningPose(Color c, bool flip, int px, int py)
    {
        B(13,  4,  6,  6, c, px, py, flip);
        B(15, 10,  2,  4, c, px, py, flip);
        B(13, 14,  6, 18, c, px, py, flip);
        B(19, 14, 10,  3, c, px, py, flip);
        B( 3, 22, 10,  3, c, px, py, flip);
        B(17, 26,  4, 20, c, px, py, flip);
        B(11, 38,  4, 20, c, px, py, flip);
    }

    private static void DrawCrouchingPose(Color c, bool flip, int px, int py)
    {
        B(13, 36, 6,  6, c, px, py, flip);
        B(15, 32, 2,  4, c, px, py, flip);
        B( 8, 40, 16, 4, c, px, py, flip);
        B( 3, 38, 10, 3, c, px, py, flip);
        B(19, 38, 10, 3, c, px, py, flip);
        B(11, 44,  8, 16, c, px, py, flip);
    }

    private static void DrawHangingPose(Color c, bool flip, int px, int py)
    {
        B(13,  4,  6,  6, c, px, py, flip);
        B(15, 10,  2,  6, c, px, py, flip);
        B(13, 16,  6, 20, c, px, py, flip);
        B( 3,  0, 10,  3, c, px, py, flip);
        B(19,  0, 10,  3, c, px, py, flip);
        B(11, 36,  4, 30, c, px, py, flip);
        B(17, 36,  4, 30, c, px, py, flip);
    }

    private static void DrawClimbPose(Color c, bool flip, int px, int py)
    {
        B(13,  4,  6,  6, c, px, py, flip);
        B(15, 10,  2,  4, c, px, py, flip);
        B(13, 14,  6, 18, c, px, py, flip);
        B( 3,  8, 10,  3, c, px, py, flip);
        B(19,  8, 10,  3, c, px, py, flip);
        B(11, 38,  4, 22, c, px, py, flip);
        B(17, 38,  4, 22, c, px, py, flip);
    }

    private static void DrawEnGardePose(Color c, bool flip, int px, int py)
    {
        B(10,  4, 6,  6, c, px, py, flip);
        B(12, 10, 2,  4, c, px, py, flip);
        B(10, 14, 6, 18, c, px, py, flip);
        B(16, 16, 14, 3, c, px, py, flip);
        B( 3, 18,  8,  3, c, px, py, flip);
        B(15, 32,  4, 20, c, px, py, flip);
        B( 9, 32,  4, 20, c, px, py, flip);
    }

    private static void DrawStrikePose(Color c, bool flip, int px, int py)
    {
        B(10,  4,  6,  6, c,                px, py, flip);
        B(12, 10,  2,  4, c,                px, py, flip);
        B(10, 14,  6, 18, c,                px, py, flip);
        B(12, 16, 20,  3, EgaPalette.Yellow, px, py, flip);
        B( 3, 20,  8,  3, c,                px, py, flip);
        B(15, 32,  4, 20, c,                px, py, flip);
        B( 9, 32,  4, 20, c,                px, py, flip);
    }

    private static void DrawDeadPose(Color c, bool flip, int px, int py)
    {
        B( 2, 56,  6, 6, c, px, py, flip);
        B( 8, 58, 16, 4, c, px, py, flip);
        B( 0, 54, 10, 3, c, px, py, flip);
        B(22, 54, 10, 3, c, px, py, flip);
        B( 2, 62, 12, 4, c, px, py, flip);
        B(18, 62, 12, 4, c, px, py, flip);
    }
}
