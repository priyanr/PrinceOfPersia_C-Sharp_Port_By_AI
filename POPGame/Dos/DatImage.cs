namespace POPGame.Dos;

/// <summary>A decoded DOS sprite: 8-bit palette indices, index 0 == transparent.</summary>
public sealed class IndexedImage
{
    public required int Width  { get; init; }
    public required int Height { get; init; }
    public required byte[] Pixels { get; init; }   // Width*Height palette indices

    public byte At(int x, int y) => Pixels[y * Width + x];
}

/// <summary>16-entry palette; components are 8-bit, scaled up from the DAT 6-bit VGA values.</summary>
public sealed class DatPalette
{
    public readonly byte[] R = new byte[16];
    public readonly byte[] G = new byte[16];
    public readonly byte[] B = new byte[16];

    /// <summary>Number of images the owning shape+palette resource declares.</summary>
    public int ImageCount { get; private set; }

    /// <summary>
    /// Parses a dat_shpl resource: byte nImages, uint16 rowBits, byte nColors, rgb[16] with 6-bit components.
    /// </summary>
    public static DatPalette Parse(ReadOnlySpan<byte> res)
    {
        var p = new DatPalette { ImageCount = res[0] };
        int at = 1 + 2 + 1;                 // nImages, rowBits(2), nColors
        for (int i = 0; i < 16; i++)
        {
            p.R[i] = Scale6(res[at++]);
            p.G[i] = Scale6(res[at++]);
            p.B[i] = Scale6(res[at++]);
        }
        return p;
    }

    // 6-bit VGA DAC value -> 8-bit.
    // The VGA DAC replicates the top bits rather than scaling, which is what
    // DOSBox reproduces: 6-bit v -> (v << 2) | (v >> 4).
    private static byte Scale6(byte v) => (byte)((v & 0x3F) << 2 | (v & 0x3F) >> 4);
}

/// <summary>
/// Decoder for image resources inside a POP DOS .DAT.
///
/// Image resource layout: uint16 height, uint16 width, uint16 flags, then payload.
///   depth  = ((flags >> 12) and 7) + 1     bits per pixel
///   cmeth  = (flags >> 8) and 0x0F         compression method 0..4
///   stride = (depth * width + 7) / 8       bytes per packed row
/// </summary>
public static class DatImage
{
    public static IndexedImage? Decode(ReadOnlySpan<byte> res)
    {
        if (res.Length < 6) return null;

        int height = res[0] | (res[1] << 8);
        int width  = res[2] | (res[3] << 8);
        int flags  = res[4] | (res[5] << 8);
        if (height <= 0 || width <= 0 || height > 4096 || width > 4096) return null;

        int depth  = ((flags >> 12) & 7) + 1;
        int cmeth  = (flags >> 8) & 0x0F;
        int stride = (depth * width + 7) / 8;
        int destSize = stride * height;

        var packed = new byte[destSize];
        var src = res[6..];

        switch (cmeth)
        {
            case 0: src[..Math.Min(destSize, src.Length)].CopyTo(packed); break;
            case 1: RleLr(packed, src, destSize); break;
            case 2: RleUd(packed, src, destSize, stride, height); break;
            case 3: LzgLr(packed, src, destSize); break;
            case 4: LzgUd(packed, src, destSize, stride, height); break;
            default: return null;
        }

        return new IndexedImage
        {
            Width = width,
            Height = height,
            Pixels = Unpack(packed, width, height, stride, depth)
        };
    }

    /// <summary>Header-only peek, for cataloguing a DAT without decoding it.</summary>
    public static (int H, int W, int Depth, int Method)? Peek(ReadOnlySpan<byte> res)
    {
        if (res.Length < 6) return null;
        int h = res[0] | (res[1] << 8);
        int w = res[2] | (res[3] << 8);
        int f = res[4] | (res[5] << 8);
        return (h, w, ((f >> 12) & 7) + 1, (f >> 8) & 0x0F);
    }

    // ── depth unpacking: pixels packed MSB-first inside each byte, rows stride-aligned ──
    private static byte[] Unpack(byte[] packed, int width, int height, int stride, int depth)
    {
        var outp = new byte[width * height];
        if (depth == 8)
        {
            for (int y = 0; y < height; y++)
                Array.Copy(packed, y * stride, outp, y * width, width);
            return outp;
        }

        int mask = (1 << depth) - 1;
        int perByte = 8 / depth;
        for (int y = 0; y < height; y++)
        {
            int rowIn = y * stride, rowOut = y * width;
            for (int x = 0; x < width; x++)
            {
                int b = packed[rowIn + x / perByte];
                int shift = 8 - depth - (x % perByte) * depth;
                outp[rowOut + x] = (byte)((b >> shift) & mask);
            }
        }
        return outp;
    }

    // ── codecs, ported from the original DOS decompressors ────────────────────

    private static void RleLr(byte[] dest, ReadOnlySpan<byte> src, int destLength)
    {
        int sp = 0, dp = 0, rem = destLength;
        while (rem > 0 && sp < src.Length)
        {
            int count = (sbyte)src[sp++];
            if (count >= 0)
            {
                count++;
                do { dest[dp++] = src[sp++]; rem--; count--; } while (count > 0 && rem > 0 && sp < src.Length);
            }
            else
            {
                byte v = src[sp++];
                count = -count;
                do { dest[dp++] = v; rem--; count--; } while (count > 0 && rem > 0);
            }
        }
    }

    private static void RleUd(byte[] dest, ReadOnlySpan<byte> src, int destLength, int stride, int height)
    {
        int remHeight = height, sp = 0, dp = 0, rem = destLength;
        int wrap = destLength - 1, step = stride - 1;

        while (rem > 0 && sp < src.Length)
        {
            int count = (sbyte)src[sp++];
            bool copy = count >= 0;
            byte v = 0;
            if (copy) count++;
            else { v = src[sp++]; count = -count; }

            do
            {
                dest[dp++] = copy ? src[sp++] : v;
                dp += step;
                if (--remHeight == 0) { dp -= wrap; remHeight = height; }
                rem--; count--;
            } while (count > 0 && rem > 0 && (!copy || sp < src.Length));
        }
    }

    private static void LzgLr(byte[] dest, ReadOnlySpan<byte> src, int destLength)
    {
        var window = new byte[0x400];
        int wp = 0x400 - 0x42, sp = 0, dp = 0, remaining = destLength, mask = 0;

        while (remaining > 0)
        {
            mask >>= 1;
            if ((mask & 0xFF00) == 0)
            {
                if (sp >= src.Length) break;
                mask = src[sp++] | 0xFF00;
            }

            if ((mask & 1) != 0)
            {
                if (sp >= src.Length) break;
                byte v = src[sp++];
                window[wp++] = v; dest[dp++] = v;
                if (wp >= 0x400) wp = 0;
                remaining--;
            }
            else
            {
                if (sp + 1 >= src.Length) break;
                int info = (src[sp] << 8) | src[sp + 1]; sp += 2;
                int cs = info & 0x3FF;
                int len = (info >> 10) + 3;
                do
                {
                    byte v = window[cs++];
                    window[wp++] = v; dest[dp++] = v;
                    if (cs >= 0x400) cs = 0;
                    if (wp >= 0x400) wp = 0;
                    remaining--; len--;
                } while (remaining > 0 && len > 0);
            }
        }
    }

    private static void LzgUd(byte[] dest, ReadOnlySpan<byte> src, int destLength, int stride, int height)
    {
        var window = new byte[0x400];
        int wp = 0x400 - 0x42, sp = 0, dp = 0, mask = 0;
        int remaining = height, left = destLength, wrap = destLength - 1;

        while (left > 0)
        {
            mask >>= 1;
            if ((mask & 0xFF00) == 0)
            {
                if (sp >= src.Length) break;
                mask = src[sp++] | 0xFF00;
            }

            if ((mask & 1) != 0)
            {
                if (sp >= src.Length) break;
                byte v = src[sp++];
                window[wp++] = v; dest[dp] = v; dp += stride;
                if (--remaining == 0) { dp -= wrap; remaining = height; }
                if (wp >= 0x400) wp = 0;
                left--;
            }
            else
            {
                if (sp + 1 >= src.Length) break;
                int info = (src[sp] << 8) | src[sp + 1]; sp += 2;
                int cs = info & 0x3FF;
                int len = (info >> 10) + 3;
                do
                {
                    byte v = window[cs++];
                    window[wp++] = v; dest[dp] = v; dp += stride;
                    if (--remaining == 0) { dp -= wrap; remaining = height; }
                    if (cs >= 0x400) cs = 0;
                    if (wp >= 0x400) wp = 0;
                    left--; len--;
                } while (left > 0 && len > 0);
            }
        }
    }
}
