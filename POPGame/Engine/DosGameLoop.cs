using Raylib_cs;
using POPGame.Dos;
using POPGame.Input;
using POPGame.Rendering;
using POPGame.Sim;

namespace POPGame.Engine;

/// <summary>
/// The DOS-accurate game loop: authentic tables and graphics from the original
/// install, the sequence-driven character engine, and the 320x200 framebuffer
/// uploaded as a single texture and scaled up.
/// </summary>
public sealed class DosGameLoop
{
    private const int Scale = 3;
    private const double TickSeconds = 1.0 / 12.0;   // the original runs at ~12 fps

    private readonly int _startLevel;

    public DosGameLoop(int startLevel = 1) => _startLevel = startLevel;

    public void Run()
    {
        var sim = GameSetup.Build(_startLevel, out var levels);
        var renderer = new DosRenderer();

        Raylib.SetConfigFlags(ConfigFlags.VSyncHint);
        Raylib.InitWindow(DosRenderer.ScreenW * Scale, DosRenderer.ScreenH * Scale,
                          "Prince of Persia - POPCS");
        Raylib.SetTargetFPS(60);

        var tex = CreateCanvasTexture();
        var input = new InputState();
        var poll = new RaylibInput();
        double acc = 0;

        while (!Raylib.WindowShouldClose())
        {
            poll.Poll(input);

            acc += Raylib.GetFrameTime();
            while (acc >= TickSeconds)
            {
                acc -= TickSeconds;
                sim.Tick(input);

                if (sim.Effects.NextLevel && levels.Has(sim.LevelNumber + 1))
                    sim.LoadLevel(levels.Get(sim.LevelNumber + 1), sim.LevelNumber + 1);
                else if (sim.Effects.Died || Raylib.IsKeyPressed(KeyboardKey.R))
                    sim.LoadLevel(levels.Get(sim.LevelNumber), sim.LevelNumber);
            }

            renderer.Draw(sim);
            UploadFrame(tex, renderer.Frame);

            Raylib.BeginDrawing();
            Raylib.ClearBackground(Color.Black);
            Raylib.DrawTexturePro(
                tex,
                new Rectangle(0, 0, DosRenderer.ScreenW, DosRenderer.ScreenH),
                new Rectangle(0, 0, DosRenderer.ScreenW * Scale, DosRenderer.ScreenH * Scale),
                System.Numerics.Vector2.Zero, 0f, Color.White);
            DrawDebug(sim);
            Raylib.EndDrawing();
        }

        Raylib.UnloadTexture(tex);
        Raylib.CloseWindow();
    }

    private static Texture2D CreateCanvasTexture()
    {
        var blank = Raylib.GenImageColor(DosRenderer.ScreenW, DosRenderer.ScreenH, Color.Black);
        var tex = Raylib.LoadTextureFromImage(blank);
        Raylib.UnloadImage(blank);
        return tex;
    }

    private static void UploadFrame(Texture2D tex, Framebuffer fb)
    {
        unsafe
        {
            fixed (byte* p = fb.Pixels) Raylib.UpdateTexture(tex, p);
        }
    }

    private static void DrawDebug(Simulation sim)
    {
        var k = sim.Kid;
        Raylib.DrawText($"HP {new string('|', Math.Max(0, k.Hp))}   level {sim.LevelNumber}   room {k.Room}",
                        6, 6, 20, Color.Red);
        Raylib.DrawText("arrows move/jump/crouch   shift = careful step   R = restart level",
                        6, DosRenderer.ScreenH * Scale - 26, 16, Color.DarkGray);
        Raylib.DrawText($"x{k.X} y{k.Y} row{k.Row} frame{k.Frame} act{(int)k.Action} seq{k.CurrentSeq}",
                        6, 30, 14, Color.DarkGray);
    }
}

/// <summary>Shared start-up: load the DOS tables and the requested level.</summary>
public static class GameSetup
{
    public static Simulation Build(int level, out DosLevels levels)
    {
        var tables = DosTables.Load();
        string? problem = tables.Validate();
        if (problem is not null)
            throw new InvalidDataException($"PRINCE.EXE table check failed: {problem}");

        levels = DosLevels.Load();
        return new Simulation(tables, levels.Get(level), level);
    }
}
