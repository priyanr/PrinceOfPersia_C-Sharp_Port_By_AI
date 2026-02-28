using Raylib_cs;
using POPGame.Engine;

namespace POPGame.Rendering;

/// <summary>
/// Draws the HUD bar (y = 0–23 virtual pixels): level info, HP pips, timer, sword.
/// </summary>
public static class RaylibHud
{
    public static void Draw(GameState state)
    {
        // Background
        Raylib.DrawRectangle(0, 0, 320, 24, EgaPalette.Black);

        var ch = state.Player;
        int totalScreens = (state.Level != null && state.Level.InfoScreenCount > 1)
            ? state.Level.InfoScreenCount - 1
            : 23;

        // Level / screen info
        string info = $"LVL {state.LevelIndex}  Scr {ch.Screen}/{totalScreens}";
        Raylib.DrawText(info, 4, 7, 10, EgaPalette.White);

        // HP pips (8×8 colored squares)
        int maxPips = Math.Min(ch.MaxLife, 16);  // cap so we don't overflow 320px
        for (int i = 0; i < maxPips; i++)
        {
            Color pip = i < ch.Life ? EgaPalette.Red : EgaPalette.DarkGray;
            Raylib.DrawRectangle(100 + i * 10, 8, 8, 8, pip);
        }

        // Timer (time remaining)
        int totalFrames = state.TimerFrames;
        int minutes  = totalFrames / Constants.FramesPerMinute;
        int secFrames = totalFrames % Constants.FramesPerMinute;
        int seconds  = secFrames * 60 / Constants.FramesPerMinute;
        int remain   = Math.Max(0, Constants.TimeLimitMinutes - minutes);
        Color timerColor = remain <= 5 ? EgaPalette.Red : EgaPalette.DarkCyan;
        Raylib.DrawText($"{remain:D2}m {(60 - seconds) % 60:D2}s", 220, 7, 10, timerColor);

        // Sword indicator
        if (ch.HasSword)
            Raylib.DrawText("[S]", 290, 7, 10, EgaPalette.Yellow);

        // Separator line
        Raylib.DrawRectangle(0, 23, 320, 1, EgaPalette.DarkGray);
    }
}
