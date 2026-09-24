using POPGame.Dos;

namespace POPGame.Rendering;

/// <summary>
/// A plain RGBA framebuffer at the DOS native resolution. All drawing goes through
/// here, so the live game and the offline frame dumps render byte-for-byte the same
/// image, and neither needs a GPU.
/// </summary>
public sealed class Framebuffer
{
    public int Width { get; }
    public int Height { get; }
    public byte[] Pixels { get; }        // RGBA8, row-major

    public Framebuffer(int width, int height)
    {
        Width = width;
        Height = height;
        Pixels = new byte[width * height * 4];
    }

    public void Clear(byte r, byte g, byte b)
    {
        for (int i = 0; i < Pixels.Length; i += 4)
        {
            Pixels[i] = r; Pixels[i + 1] = g; Pixels[i + 2] = b; Pixels[i + 3] = 255;
        }
    }

    public void FillRect(int x, int y, int w, int h, byte r, byte g, byte b)
    {
        int x0 = Math.Max(0, x), y0 = Math.Max(0, y);
        int x1 = Math.Min(Width, x + w), y1 = Math.Min(Height, y + h);
        for (int yy = y0; yy < y1; yy++)
        {
            int o = (yy * Width + x0) * 4;
            for (int xx = x0; xx < x1; xx++, o += 4)
            {
                Pixels[o] = r; Pixels[o + 1] = g; Pixels[o + 2] = b; Pixels[o + 3] = 255;
            }
        }
    }

    /// <summary>
    /// Blits an indexed sprite with its top-left at (x, y). Palette index 0 is
    /// transparent. <paramref name="mirror"/> flips it horizontally in place, which
    /// is how the original draws a character facing right.
    /// </summary>
    /// <param name="srcY">First source row to copy — lets a gate be drawn part-raised.</param>
    /// <param name="srcRows">Number of source rows, or -1 for the rest of the image.</param>
    /// <param name="skipMask">
    /// Bit i set means palette index i is transparent. Index 0 always is; the dungeon
    /// art also uses 14 and 15 as stencil markers rather than colours, which is why they
    /// are the only saturated greens in an otherwise entirely blue-grey palette.
    /// </param>
    /// <param name="mode">
    /// How the original's blitter combines the image with the screen; see <see cref="BlitMode"/>.
    /// </param>
    /// <param name="monoColor">Palette index every set pixel takes in <see cref="BlitMode.Mono"/>.</param>
    public void Blit(IndexedImage img, DatPalette pal, int x, int y, bool mirror,
                     int srcY = 0, int srcRows = -1, int skipMask = 1,
                     BlitMode mode = BlitMode.Trans, int monoColor = 0)
    {
        // An opaque blit draws index 0 as a colour; the stencil markers stay masked.
        if (mode == BlitMode.NoTrans) skipMask &= ~1;

        int last = srcRows < 0 ? img.Height : Math.Min(img.Height, srcY + srcRows);

        for (int sy = Math.Max(0, srcY); sy < last; sy++)
        {
            int dy = y + sy - srcY;
            if (dy < 0 || dy >= Height) continue;

            int rowIn = sy * img.Width;
            for (int sx = 0; sx < img.Width; sx++)
            {
                byte idx = (byte)(img.Pixels[rowIn + (mirror ? img.Width - 1 - sx : sx)] & 15);
                if ((skipMask >> idx & 1) != 0) continue;

                int dx = x + sx;
                if (dx < 0 || dx >= Width) continue;

                int o = (dy * Width + dx) * 4;
                if (mode == BlitMode.Black) { Pixels[o] = Pixels[o + 1] = Pixels[o + 2] = 0; Pixels[o + 3] = 255; continue; }
                if (mode == BlitMode.Mono) idx = (byte)monoColor;
                Pixels[o] = pal.R[idx];
                Pixels[o + 1] = pal.G[idx];
                Pixels[o + 2] = pal.B[idx];
                Pixels[o + 3] = 255;
            }
        }
    }

    public void SavePng(string path) => PngWriter.Write(path, Width, Height, Pixels);
}

/// <summary>
/// The blitter modes the original's tile drawing uses (SDLPoP's <c>blitters_*</c>).
/// </summary>
public enum BlitMode
{
    /// <summary>Index 0 is transparent (blitters_10h_transp).</summary>
    Trans,
    /// <summary>Every pixel is drawn, index 0 included (blitters_0_no_transp).</summary>
    NoTrans,
    /// <summary>
    /// ORs the image into the screen (blitters_2_or). On a true-colour buffer this is
    /// drawn as <see cref="Trans"/>, which is exact wherever it lands on black.
    /// </summary>
    Or,
    /// <summary>The image's shape in black (blitters_9_black, or mono colour 0).</summary>
    Black,
    /// <summary>The image's shape in one palette colour (blitters_40h_mono + colour).</summary>
    Mono,
}
