namespace POPGame.Dos;

/// <summary>
/// A DAT graphics file decoded to CPU-side indexed images, addressed by resource id.
///
/// A graphics DAT can declare more than one "shape+palette" group: VDUNGEON.DAT has
/// one at resource 200 (151 images) and another at 360 (17 images — the wall bricks),
/// each with its own 16 colours. It also carries loose resources outside any declared
/// run (pillars, spikes and debris up at 1030+), so every decodable resource is kept
/// and each is matched to the palette of the group it falls in.
///
/// Palette index 0 is transparent, and sprites are drawn facing LEFT natively.
/// </summary>
public sealed class DosImageBank
{
    private readonly record struct Group(int First, int Last, DatPalette Palette);

    private readonly Dictionary<int, IndexedImage> _byId = new();
    private readonly List<Group> _groups = [];

    /// <summary>Palette of the first declared group; the file's primary colours.</summary>
    public DatPalette Palette => _groups[0].Palette;

    /// <summary>Resource id of the primary palette, which indices are relative to.</summary>
    public int PaletteId { get; }

    public DosImageBank(string datPath, params int[] paletteResIds)
    {
        if (paletteResIds.Length == 0)
            throw new ArgumentException("at least one palette resource id is required");

        PaletteId = paletteResIds[0];
        var dat = new DatFile(datPath);

        foreach (int p in paletteResIds)
        {
            if (!dat.Has(p)) continue;
            var pal = DatPalette.Parse(dat.Resource(p));
            _groups.Add(new Group(p + 1, p + pal.ImageCount, pal));
        }

        foreach (var e in dat.Entries)
        {
            if (Array.IndexOf(paletteResIds, (int)e.Id) >= 0 || e.Size < 7) continue;

            var res = dat.Resource(e.Id);
            var peek = DatImage.Peek(res);
            if (peek is not { } pk) continue;
            if (pk.H is <= 0 or > 512 || pk.W is <= 0 or > 512 || pk.Method > 4) continue;

            var img = DatImage.Decode(res);
            if (img is not null) _byId[e.Id] = img;
        }
    }

    /// <summary>Look up by raw resource id.</summary>
    public IndexedImage? ById(int id) => _byId.GetValueOrDefault(id);

    /// <summary>Look up by index relative to the primary palette, as the tables address them.</summary>
    public IndexedImage? this[int index] => ById(PaletteId + index);

    /// <summary>The palette an image should be drawn with, by index.</summary>
    public DatPalette PaletteForIndex(int index) => PaletteForId(PaletteId + index);

    public DatPalette PaletteForId(int id)
    {
        foreach (var g in _groups)
            if (id >= g.First && id <= g.Last) return g.Palette;
        return _groups[0].Palette;
    }
}
