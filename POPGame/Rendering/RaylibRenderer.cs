using Raylib_cs;
using POPGame.Characters;
using POPGame.Data;
using POPGame.Engine;

namespace POPGame.Rendering;

/// <summary>
/// Full-frame renderer into the 320×240 virtual canvas.
/// Called each tick inside BeginTextureMode / EndTextureMode.
/// </summary>
public class RaylibRenderer
{
    public void Draw(GameState state)
    {
        switch (state.Phase)
        {
            case GamePhase.Title:
                DrawTitle();
                break;
            case GamePhase.Playing:
                DrawGameScreen(state);
                break;
            case GamePhase.Paused:
                DrawGameScreen(state);
                DrawOverlay(EgaPalette.Black, 140, "PAUSED", EgaPalette.Cyan,
                            "Press any key to resume", EgaPalette.White);
                break;
            case GamePhase.Dead:
                DrawGameScreen(state);
                DrawOverlay(EgaPalette.DarkRed, 120, "YOU DIED", EgaPalette.Red,
                            "Press any key to retry", EgaPalette.White);
                break;
            case GamePhase.NextLevel:
                DrawNextLevel(state);
                break;
            case GamePhase.YouWin:
                DrawYouWin();
                break;
            default:
                Raylib.ClearBackground(EgaPalette.Black);
                break;
        }
    }

    // ─── Phase screens ────────────────────────────────────────────────────────

    private static void DrawTitle()
    {
        Raylib.ClearBackground(EgaPalette.Black);
        Raylib.DrawText("PRINCE OF PERSIA",       16,  38, 20, EgaPalette.Yellow);
        Raylib.DrawText("Apple II — C# Edition",  34,  64, 10, EgaPalette.Gray);
        Raylib.DrawText("Save the Princess from Jaffar's dungeon!", 6, 96, 10, EgaPalette.White);
        Raylib.DrawText("Controls:",              20, 126, 10, EgaPalette.DarkCyan);
        Raylib.DrawText("Arrow keys   - Move / Jump / Crouch",     20, 141, 10, EgaPalette.DarkCyan);
        Raylib.DrawText("Space/Shift  - Sword strike",             20, 156, 10, EgaPalette.DarkCyan);
        Raylib.DrawText("Esc          - Pause",                    20, 171, 10, EgaPalette.DarkCyan);
        Raylib.DrawText("Press any key to begin...",               50, 210, 10, EgaPalette.Yellow);
    }

    private static void DrawYouWin()
    {
        Raylib.ClearBackground(EgaPalette.Black);
        Raylib.DrawRectangle(10, 70, 300, 100, EgaPalette.Brown);
        Raylib.DrawText("YOU SAVED THE PRINCESS!",  14,  88, 14, EgaPalette.Yellow);
        Raylib.DrawText("Congratulations, Prince!", 32, 114, 12, EgaPalette.White);
        Raylib.DrawText("Press any key to exit.",   60, 148, 10, EgaPalette.DarkCyan);
    }

    private static void DrawNextLevel(GameState state)
    {
        Raylib.ClearBackground(EgaPalette.Black);
        Raylib.DrawRectangle(40, 80, 240, 80, EgaPalette.DarkGreen);
        Raylib.DrawText($"LEVEL {state.LevelIndex} CLEARED!", 52, 100, 14, EgaPalette.Yellow);
        Raylib.DrawText("Entering next level...",             62, 130, 10, EgaPalette.White);
    }

    // Semi-transparent overlay for Paused / Dead phases
    private static void DrawOverlay(Color boxColor, int boxX,
                                     string headline, Color headlineColor,
                                     string subtitle,  Color subtitleColor)
    {
        // Dark veil
        Raylib.DrawRectangle(0, 0, 320, 240,
            new Color { R = 0, G = 0, B = 0, A = 140 });
        // Box
        Raylib.DrawRectangle(boxX, 88, 320 - boxX * 2, 64, boxColor);
        Raylib.DrawText(headline, boxX + 14, 100, 20, headlineColor);
        Raylib.DrawText(subtitle, boxX +  6, 130, 10, subtitleColor);
    }

    // ─── Game screen ──────────────────────────────────────────────────────────

    private static void DrawGameScreen(GameState state)
    {
        Raylib.ClearBackground(EgaPalette.Black);
        RaylibHud.Draw(state);

        var lv  = state.Level;
        var kid = state.Player;
        int scrn = kid.Screen;
        int s0   = scrn - 1;

        if (s0 < 0 || s0 >= Level.NumScreens) return;

        for (int row = 0; row < Level.Rows; row++)
        {
            for (int col = 0; col < Level.Cols; col++)
            {
                int px = col  * TileSprites.TileW;
                int py = 24   + row * TileSprites.TileH;

                TileId tile = lv.GetTileId(s0, row, col);
                byte   spec = lv.GetSpec(s0, row, col);
                bool gateOpen = spec < Engine.Constants.GMaxVal;

                TileSprites.Draw(tile, gateOpen, state.ExitOpen, px, py);

                // Prince
                if (kid.BlockY == row && kid.BlockX == col)
                    CharSprites.DrawPrince(kid, px, py);

                // Guards on this screen
                foreach (var g in state.Guards)
                {
                    if (g.Screen == scrn && g.BlockY == row && g.BlockX == col)
                        CharSprites.DrawGuard(g, px, py);
                }
            }
        }

        // Status message overlay at bottom
        if (state.StatusTimer > 0 && !string.IsNullOrEmpty(state.StatusMessage))
        {
            Raylib.DrawRectangle(0, 226, 320, 14, EgaPalette.DarkGray);
            Raylib.DrawText(state.StatusMessage, 4, 228, 10, EgaPalette.Yellow);
        }
    }
}
