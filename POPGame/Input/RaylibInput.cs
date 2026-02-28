using Raylib_cs;

namespace POPGame.Input;

/// <summary>
/// Raylib-based keyboard polling. Replaces ConsoleKey-based InputHandler.
/// </summary>
public class RaylibInput
{
    public void Poll(InputState s)
    {
        // Held-down flags
        s.Left   = Raylib.IsKeyDown(KeyboardKey.Left);
        s.Right  = Raylib.IsKeyDown(KeyboardKey.Right);
        s.Up     = Raylib.IsKeyDown(KeyboardKey.Up);
        s.Down   = Raylib.IsKeyDown(KeyboardKey.Down);
        s.Action = Raylib.IsKeyDown(KeyboardKey.Space)
                || Raylib.IsKeyDown(KeyboardKey.LeftShift);
        s.Pause  = Raylib.IsKeyPressed(KeyboardKey.Escape);

        // Fresh-press (rising-edge) flags
        s.FreshLeft   = Raylib.IsKeyPressed(KeyboardKey.Left);
        s.FreshRight  = Raylib.IsKeyPressed(KeyboardKey.Right);
        s.FreshUp     = Raylib.IsKeyPressed(KeyboardKey.Up);
        s.FreshDown   = Raylib.IsKeyPressed(KeyboardKey.Down);
        s.FreshAction = Raylib.IsKeyPressed(KeyboardKey.Space)
                     || Raylib.IsKeyPressed(KeyboardKey.LeftShift);

        // Any key pressed this frame (for "press any key" prompts)
        s.AnyFresh = Raylib.GetKeyPressed() != 0;
    }
}
