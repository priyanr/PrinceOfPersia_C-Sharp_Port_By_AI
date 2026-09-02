namespace POPGame.Dos;

/// <summary>
/// Locates the original DOS Prince of Persia installation, which supplies all
/// authentic assets: PRINCE.EXE (frame + sequence tables), LEVELS.DAT and the
/// graphics DATs. Nothing copyrighted is copied into this repository.
///
/// Search order: POP_DOS_DIR environment variable, then a few well-known paths.
/// </summary>
public static class DosGame
{
    private static readonly string[] Candidates =
    [
        @"E:\DOS\games\Prince",
        @"C:\DOS\games\Prince",
        @"D:\DOS\games\Prince",
    ];

    private static string? _dir;

    public static string Dir => _dir ??= Locate();

    public static string File(string name) => Path.Combine(Dir, name);

    public static bool TryLocate(out string dir)
    {
        try { dir = Dir; return true; }
        catch { dir = ""; return false; }
    }

    private static string Locate()
    {
        string? env = Environment.GetEnvironmentVariable("POP_DOS_DIR");
        if (!string.IsNullOrWhiteSpace(env) && IsPopDir(env)) return env;

        foreach (string c in Candidates)
            if (IsPopDir(c)) return c;

        throw new DirectoryNotFoundException(
            "Could not find the DOS Prince of Persia install (needs PRINCE.EXE, LEVELS.DAT, " +
            "VDUNGEON.DAT, KID.DAT). Set POP_DOS_DIR to point at it.");
    }

    private static bool IsPopDir(string dir) =>
        Directory.Exists(dir)
        && System.IO.File.Exists(Path.Combine(dir, "PRINCE.EXE"))
        && System.IO.File.Exists(Path.Combine(dir, "LEVELS.DAT"))
        && System.IO.File.Exists(Path.Combine(dir, "KID.DAT"));
}
