using POPGame.Input;
using POPGame.Rendering;

namespace POPGame.Engine;

/// <summary>
/// Runs the simulation with a scripted input sequence and writes each frame to PNG.
/// No window and no GPU, so it works anywhere and gives a repeatable way to compare
/// our output against screenshots of the original.
///
/// Script syntax: whitespace-separated tokens of "&lt;keys&gt;&lt;count&gt;", where keys are
/// any of L R U D S (shift) or "." for no input. Example:
///   ".5 R20 RU2 R15"
/// holds nothing for 5 ticks, right for 20, right+up for 2, then right for 15.
/// </summary>
public static class HeadlessRun
{
    /// <summary>
    /// Renders one candidate dungeon image per column at a given offset, for picking
    /// the right piece for a tile by eye.
    /// </summary>
    public static int Probe(string outFile, int[] candidates, int pieceY)
    {
        var renderer = new DosRenderer();
        renderer.DrawPieceProbe(candidates, pieceY);
        renderer.Frame.SavePng(outFile);
        Console.WriteLine($"probe {string.Join(",", candidates)} at y={pieceY} -> {outFile}");
        return 0;
    }

    /// <summary>Renders one room of a level, for checking tile art away from the start.</summary>
    public static int Room(string outFile, int level, int room)
    {
        var sim = GameSetup.Build(level, out _);
        sim.Kid.Room = room;
        var renderer = new DosRenderer();
        renderer.Draw(sim);
        renderer.Frame.SavePng(outFile);
        Console.WriteLine($"level {level} room {room} -> {outFile}");
        return 0;
    }

    public static int Run(int level, string outDir, string script, int everyNth = 1)
    {
        Directory.CreateDirectory(outDir);

        var sim = GameSetup.Build(level, out _);
        var renderer = new DosRenderer();
        var input = new InputState();

        var steps = Parse(script).ToList();
        Console.WriteLine($"level {level}: {steps.Count} ticks -> {outDir}");

        int written = 0;
        for (int tick = 0; tick < steps.Count; tick++)
        {
            Apply(input, steps[tick], tick == 0 || steps[tick] != steps[tick - 1]);
            sim.Tick(input);

            if (tick % everyNth != 0) continue;

            renderer.Draw(sim);
            renderer.Frame.SavePng(Path.Combine(outDir, $"f{tick:D4}.png"));
            written++;

            var k = sim.Kid;
            Console.WriteLine($"  t{tick,3} keys={steps[tick],-5} rm{k.Room} x{k.X,3} y{k.Y,3} " +
                              $"row{k.Row} frame{k.Frame,3} act{(int)k.Action} seq{k.CurrentSeq}");
        }

        Console.WriteLine($"wrote {written} frames");
        return 0;
    }

    private static void Apply(InputState s, string keys, bool fresh)
    {
        s.Left = keys.Contains('L');
        s.Right = keys.Contains('R');
        s.Up = keys.Contains('U');
        s.Down = keys.Contains('D');
        s.Action = keys.Contains('S');

        s.FreshLeft = fresh && s.Left;
        s.FreshRight = fresh && s.Right;
        s.FreshUp = fresh && s.Up;
        s.FreshDown = fresh && s.Down;
        s.FreshAction = fresh && s.Action;
    }

    private static IEnumerable<string> Parse(string script)
    {
        foreach (string token in script.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            int split = 0;
            while (split < token.Length && !char.IsDigit(token[split])) split++;

            string keys = token[..split].Replace(".", "");
            int count = split < token.Length ? int.Parse(token[split..]) : 1;

            for (int i = 0; i < count; i++) yield return keys;
        }
    }
}
