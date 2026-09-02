using POPGame.Dos;

// Dumps every image resource of a POP DOS .DAT to PNG, plus a contact sheet.
//
//   dotnet run --project tools/DatDump -- <file.dat> <outDir> [paletteResId]
//
// paletteResId defaults to 200 (the dat_shpl resource that fronts most graphics DATs).

if (args.Length < 2)
{
    Console.Error.WriteLine("usage: DatDump <file.dat> <outDir> [paletteResId]");
    return 1;
}

string datPath = args[0];
string outDir = args[1];
int palId = args.Length > 2 ? int.Parse(args[2]) : 200;

Directory.CreateDirectory(outDir);
var dat = new DatFile(datPath);

DatPalette pal;
try
{
    pal = DatPalette.Parse(dat.Resource(palId));
    Console.WriteLine($"palette res {palId}: declares {pal.ImageCount} images");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"no palette at {palId} ({ex.Message}); falling back to greyscale");
    pal = Greyscale();
}

Console.WriteLine($"{Path.GetFileName(datPath)}: {dat.Entries.Count} resources");

var decoded = new List<(int Id, IndexedImage Img)>();
foreach (var e in dat.Entries)
{
    if (e.Id == palId || e.Size < 7) continue;

    var res = dat.Resource(e.Id);
    var peek = DatImage.Peek(res);
    if (peek is not { } pk) continue;
    if (pk.H <= 0 || pk.W <= 0 || pk.H > 512 || pk.W > 512) continue;
    if (pk.Method > 4) continue;

    IndexedImage? img;
    try { img = DatImage.Decode(res); }
    catch (Exception ex) { Console.WriteLine($"  id {e.Id}: decode failed: {ex.GetType().Name}"); continue; }
    if (img is null) continue;

    decoded.Add((e.Id, img));
    PngWriter.Write(Path.Combine(outDir, $"{e.Id:D4}.png"), img.Width, img.Height, ToRgba(img, pal));
}

Console.WriteLine($"decoded {decoded.Count} images -> {outDir}");

// Paged sheets: small enough that the per-sprite index labels stay legible.
const int PerPage = 48;
for (int page = 0, i = 0; i < decoded.Count; page++, i += PerPage)
{
    var slice = decoded.Skip(i).Take(PerPage).ToList();
    WriteContactSheet(Path.Combine(outDir, $"_sheet{page}.png"), slice, pal, palId);
}
WriteContactSheet(Path.Combine(outDir, "_sheet.png"), decoded, pal, palId);
Console.WriteLine($"contact sheets: _sheet.png + {(decoded.Count + PerPage - 1) / PerPage} pages");
return 0;

// ── helpers ──────────────────────────────────────────────────────────────────

static byte[] ToRgba(IndexedImage img, DatPalette pal)
{
    var rgba = new byte[img.Width * img.Height * 4];
    for (int i = 0; i < img.Pixels.Length; i++)
    {
        byte idx = img.Pixels[i];
        int o = i * 4;
        rgba[o + 0] = pal.R[idx & 15];
        rgba[o + 1] = pal.G[idx & 15];
        rgba[o + 2] = pal.B[idx & 15];
        rgba[o + 3] = idx == 0 ? (byte)0 : (byte)255;   // index 0 is transparent
    }
    return rgba;
}

static DatPalette Greyscale()
{
    var p = new DatPalette();
    for (int i = 0; i < 16; i++) { p.R[i] = p.G[i] = p.B[i] = (byte)(i * 17); }
    return p;
}

// Lays the sprites out on a grid with a checkerboard behind them, so transparency
// and sprite bounds are both obvious at a glance. Each cell is labelled with the
// sprite's 1-based index within the DAT, which is what the frame tables address.
static void WriteContactSheet(string path, List<(int Id, IndexedImage Img)> imgs, DatPalette pal, int paletteId)
{
    if (imgs.Count == 0) return;

    const int Cols = 16, Pad = 2;
    int label = DatDump.TinyFont.GlyphH + 2;
    int cellW = Math.Max(imgs.Max(t => t.Img.Width), 20) + Pad * 2;
    int cellH = imgs.Max(t => t.Img.Height) + Pad * 2 + label;
    int rows = (imgs.Count + Cols - 1) / Cols;
    int w = Cols * cellW, h = rows * cellH;

    var sheet = new byte[w * h * 4];
    for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            bool dark = ((x / 8) + (y / 8)) % 2 == 0;
            int o = (y * w + x) * 4;
            byte v = dark ? (byte)40 : (byte)70;
            sheet[o] = sheet[o + 1] = sheet[o + 2] = v;
            sheet[o + 3] = 255;
        }

    for (int i = 0; i < imgs.Count; i++)
    {
        var (_, img) = imgs[i];
        int ox = (i % Cols) * cellW + Pad;
        int oy = (i / Cols) * cellH + Pad + label;

        // 1-based sprite index within the DAT (what frame tables address).
        DatDump.TinyFont.Draw(sheet, w, h, ox, oy - label, (imgs[i].Id - paletteId).ToString(), 255, 230, 80);

        for (int y = 0; y < img.Height; y++)
            for (int x = 0; x < img.Width; x++)
            {
                byte idx = img.At(x, y);
                if (idx == 0) continue;
                int o = ((oy + y) * w + ox + x) * 4;
                sheet[o] = pal.R[idx & 15];
                sheet[o + 1] = pal.G[idx & 15];
                sheet[o + 2] = pal.B[idx & 15];
                sheet[o + 3] = 255;
            }
    }

    PngWriter.Write(path, w, h, sheet);
}
