using POPGame.Data;

namespace POPGame.Dos;

/// <summary>
/// Level source backed by the DOS LEVELS.DAT. Resource id 2000+N holds level N as
/// the same 2304-byte blob the Apple II LEVELn files use (verified 99-100% byte
/// identical), so <see cref="Level.Load(byte[])"/> parses it unchanged.
/// </summary>
public sealed class DosLevels
{
    public const int FirstId = 2000;
    public const int LastLevel = 14;

    private readonly DatFile _dat;

    public DosLevels(string levelsDatPath) => _dat = new DatFile(levelsDatPath);

    public static DosLevels Load() => new(DosGame.File("LEVELS.DAT"));

    public bool Has(int level) => _dat.Has(FirstId + level);

    public Level Get(int level)
    {
        var res = _dat.Resource(FirstId + level);
        return Level.Load(res[..2304].ToArray());
    }
}
