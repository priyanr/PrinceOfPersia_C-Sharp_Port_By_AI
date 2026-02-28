using POPGame.Engine;

// ── Locate levels directory ─────────────────────────────────────────────────
static string FindLevelsDir()
{
    var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
    for (int i = 0; i < 8 && dir != null; i++)
    {
        string candidate = Path.Combine(dir.FullName,
            "originalcode",
            "Prince-of-Persia-Apple-II-master",
            "01 POP Source",
            "Levels");
        if (Directory.Exists(candidate)) return candidate;
        dir = dir.Parent;
    }
    // Hard-coded fallback
    return Path.Combine("F:", "priyan", "Projects", "AI", "POPCS",
        "originalcode", "Prince-of-Persia-Apple-II-master", "01 POP Source", "Levels");
}

string levelsDir = FindLevelsDir();
if (!Directory.Exists(levelsDir))
{
    Console.Error.WriteLine($"ERROR: Levels directory not found:\n  {levelsDir}");
    return;
}

var loop = new GameLoop(levelsDir);
loop.Run();
