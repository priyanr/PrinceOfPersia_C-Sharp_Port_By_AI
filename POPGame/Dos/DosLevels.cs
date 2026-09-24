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
        NormaliseGates(lv);
        return lv;
    }

    /// <summary>
    /// In the DOS data a gate's BLUESPEC is a flag, not a height: 1 means it starts
    /// open and anything else means shut (LOAD_ALTER_MOD turns 1 into the fully raised
    /// 188 and the rest into 0). The simulation keeps gate height as 0..GateOpen, so
    /// convert once here, in the pristine copy too so a level restart stays right.
    /// </summary>
    private static void NormaliseGates(Level lv)
    {
        for (int i = 0; i < lv.BlueType.Length; i++)
        {
            if ((TileId)(lv.BlueType[i] & 0x1F) != TileId.Gate) continue;
            lv.BlueSpec[i] = (byte)(lv.BlueSpec[i] == 1 ? Sim.RoomView.GateOpen : 0);
            lv.LiveBlueSpec[i] = lv.BlueSpec[i];
        }
    }
}
