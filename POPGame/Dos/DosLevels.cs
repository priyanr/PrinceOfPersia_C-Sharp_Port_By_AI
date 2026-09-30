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
        var lv = Level.Load(res[..2304].ToArray());
        AlterModsAllrm(lv);
        return lv;
    }

    /// <summary>
    /// alter_mods_allrm / load_alter_mod (SDLPoP seg008): the level file's modifiers
    /// are flags that the game turns into working values once, at level load. A gate's
    /// 1 means it starts open (height 188), anything else shut; a loose floor starts
    /// still; a potion's type moves up to bits 3-7, leaving the low bits for its bubble.
    /// Walls keep their raw "no blue" flag; the room drawer works out their neighbours.
    /// Done in the pristine copy too, so a level restart stays right.
    /// </summary>
    private static void AlterModsAllrm(Level lv)
    {
        for (int i = 0; i < lv.BlueType.Length; i++)
        {
            byte mod = lv.BlueSpec[i];
            switch ((TileId)(lv.BlueType[i] & 0x1F))
            {
                case TileId.Gate: mod = (byte)(mod == 1 ? 188 : 0); break;
                case TileId.Loose: mod = 0; break;
                case TileId.Flask: mod = (byte)(mod << 3); break;
                default: continue;
            }
            lv.BlueSpec[i] = lv.LiveBlueSpec[i] = mod;
        }
    }
}
