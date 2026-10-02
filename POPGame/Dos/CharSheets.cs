namespace POPGame.Dos;

/// <summary>
/// The character sprite sheets (chtabs): the kid (KID.DAT 400), the guard of the level
/// (id 750 of GUARD.DAT, FAT.DAT, SKEL.DAT, VIZIER.DAT or SHADOW.DAT by the level's guard
/// type) and the sword (PRINCE.DAT 700). A frame says which sheet its image is in: the
/// top two bits of its sword byte are added to the kid's chtab, 2 (kid) .. 5 (guard).
/// </summary>
public sealed class CharSheets
{
    private static readonly string[] GuardDats = ["GUARD.DAT", "FAT.DAT", "SKEL.DAT", "VIZIER.DAT", "SHADOW.DAT"];

    public DosImageBank Kid { get; } = new(DosGame.File("KID.DAT"), 400);

    /// <summary>The sword's images (chtab 0); null if PRINCE.DAT has none.</summary>
    public DosImageBank? Sword { get; } = TryBank("PRINCE.DAT", 700);

    /// <summary>The level's guard sheet; null on a level with no guard (or a missing file).</summary>
    public DosImageBank? Guard { get; private set; }

    private int _guardKey = -2;

    /// <summary>load_lev_spr: the guard sheet for a level.</summary>
    public void SetLevel(int level, DosTables tables)
    {
        int type = tables.GuardType[Math.Clamp(level, 0, tables.GuardType.Length - 1)];
        int levelType = DosLevels.LevelType(level);
        int key = type < 0 ? -1 : type * 2 + levelType;
        if (key == _guardKey) return;
        _guardKey = key;
        Guard = null;
        if (type < 0 || type >= GuardDats.Length) return;

        // A regular guard's sheet is first looked for in the level type's own file
        // (GUARD1.DAT palace, GUARD2.DAT dungeon), which open_dat puts ahead of GUARD.DAT.
        if (type == 0) Guard = TryBank(levelType != 0 ? "GUARD1.DAT" : "GUARD2.DAT", 750);
        Guard ??= TryBank(GuardDats[type], 750);
    }

    /// <summary>The sheet a frame's image is in, or null for the sheets this port doesn't load.</summary>
    public DosImageBank? BankFor(FrameDef f) => (f.Sword >> 6) switch
    {
        0 => Kid,
        3 => Guard,
        _ => null,
    };

    /// <summary>Width and height of a frame's image (collision uses the sprite's width).</summary>
    public (int W, int H) Size(FrameDef f) =>
        BankFor(f)?[f.Image + 1] is { } img ? (img.Width, img.Height) : (0, 0);

    private static DosImageBank? TryBank(string file, int paletteId)
    {
        try
        {
            string path = DosGame.File(file);
            if (!File.Exists(path) || !new DatFile(path).Has(paletteId)) return null;
            return new DosImageBank(path, paletteId);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or KeyNotFoundException)
        {
            return null;
        }
    }
}
