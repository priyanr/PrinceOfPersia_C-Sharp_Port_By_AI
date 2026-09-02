using Raylib_cs;

namespace POPGame.Dos;

/// <summary>
/// A DAT graphics bank loaded as GPU textures: one "shape+palette" resource that
/// declares N images, followed by those N image resources.
///
/// Sprites are 1-indexed to match the frame tables. Palette index 0 is transparent.
/// Every POP sprite is drawn facing LEFT natively, so drawing to the right mirrors.
/// </summary>
public sealed class DosSpriteSheet : IDisposable
{
    public readonly record struct Sprite(Texture2D Tex, int W, int H)
    {
        public bool Valid => W > 0 && H > 0;
    }

    private readonly Sprite[] _sprites;

    public DatPalette Palette { get; }
    public int Count => _sprites.Length - 1;

    public DosSpriteSheet(string datPath, int paletteResId)
    {
        var dat = new DatFile(datPath);
        Palette = DatPalette.Parse(dat.Resource(paletteResId));

        int n = Palette.ImageCount;
        _sprites = new Sprite[n + 1];

        for (int i = 1; i <= n; i++)
        {
            var res = dat.ResourceOrEmpty(paletteResId + i);
            if (res.Length < 7) continue;

            var img = DatImage.Decode(res);
            if (img is null) continue;

            _sprites[i] = Upload(img, Palette);
        }
    }

    /// <summary>1-indexed lookup; out-of-range and blank slots come back invalid.</summary>
    public Sprite this[int index] =>
        index >= 1 && index < _sprites.Length ? _sprites[index] : default;

    private static Sprite Upload(IndexedImage img, DatPalette pal)
    {
        var rgba = new byte[img.Width * img.Height * 4];
        for (int i = 0; i < img.Pixels.Length; i++)
        {
            byte idx = (byte)(img.Pixels[i] & 15);
            int o = i * 4;
            rgba[o + 0] = pal.R[idx];
            rgba[o + 1] = pal.G[idx];
            rgba[o + 2] = pal.B[idx];
            rgba[o + 3] = idx == 0 ? (byte)0 : (byte)255;
        }

        unsafe
        {
            fixed (byte* p = rgba)
            {
                var raw = new Image
                {
                    Data = p,
                    Width = img.Width,
                    Height = img.Height,
                    Mipmaps = 1,
                    Format = PixelFormat.UncompressedR8G8B8A8,
                };
                var tex = Raylib.LoadTextureFromImage(raw);
                return new Sprite(tex, img.Width, img.Height);
            }
        }
    }

    /// <summary>
    /// Draws sprite <paramref name="index"/> with its top-left at (x, y).
    /// When <paramref name="faceRight"/> the sprite is mirrored about
    /// <paramref name="x"/>, matching how the original flips its blits.
    /// </summary>
    public void Draw(int index, int x, int y, bool faceRight, Color tint)
    {
        var s = this[index];
        if (!s.Valid) return;

        var src = new Rectangle(0, 0, faceRight ? -s.W : s.W, s.H);
        var dst = new Rectangle(x, y, s.W, s.H);
        Raylib.DrawTexturePro(s.Tex, src, dst, System.Numerics.Vector2.Zero, 0f, tint);
    }

    public void Draw(int index, int x, int y, bool faceRight) =>
        Draw(index, x, y, faceRight, Color.White);

    public void Dispose()
    {
        foreach (var s in _sprites)
            if (s.Valid) Raylib.UnloadTexture(s.Tex);
    }
}
