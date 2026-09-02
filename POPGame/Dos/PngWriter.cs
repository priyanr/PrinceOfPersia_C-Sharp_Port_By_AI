using System.IO.Compression;

namespace POPGame.Dos;

/// <summary>
/// Minimal RGBA PNG encoder. Used by the asset dump tool so decoded DOS sprites
/// can be eyeballed without pulling in an imaging dependency.
/// </summary>
public static class PngWriter
{
    public static void Write(string path, int width, int height, byte[] rgba)
    {
        using var fs = File.Create(path);
        Span<byte> sig = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];
        fs.Write(sig);

        var ihdr = new byte[13];
        WriteBE(ihdr, 0, width);
        WriteBE(ihdr, 4, height);
        ihdr[8] = 8;    // bit depth
        ihdr[9] = 6;    // colour type: truecolour + alpha
        Chunk(fs, "IHDR", ihdr);

        // Raw scanlines, each prefixed with filter type 0.
        var raw = new byte[height * (1 + width * 4)];
        for (int y = 0; y < height; y++)
        {
            int dst = y * (1 + width * 4);
            raw[dst] = 0;
            Array.Copy(rgba, y * width * 4, raw, dst + 1, width * 4);
        }
        Chunk(fs, "IDAT", ZlibDeflate(raw));
        Chunk(fs, "IEND", []);
    }

    private static byte[] ZlibDeflate(byte[] data)
    {
        using var ms = new MemoryStream();
        ms.WriteByte(0x78);
        ms.WriteByte(0x9C);
        using (var ds = new DeflateStream(ms, CompressionLevel.Optimal, leaveOpen: true))
            ds.Write(data, 0, data.Length);

        uint a = 1, b = 0;
        foreach (byte v in data) { a = (a + v) % 65521; b = (b + a) % 65521; }
        uint adler = (b << 16) | a;
        ms.WriteByte((byte)(adler >> 24));
        ms.WriteByte((byte)(adler >> 16));
        ms.WriteByte((byte)(adler >> 8));
        ms.WriteByte((byte)adler);
        return ms.ToArray();
    }

    private static void Chunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4];
        WriteBE(len, 0, data.Length);
        s.Write(len);

        var body = new byte[4 + data.Length];
        for (int i = 0; i < 4; i++) body[i] = (byte)type[i];
        Array.Copy(data, 0, body, 4, data.Length);
        s.Write(body);

        var crc = new byte[4];
        WriteBE(crc, 0, (int)Crc32(body));
        s.Write(crc);
    }

    private static void WriteBE(byte[] b, int at, int v)
    {
        b[at] = (byte)(v >> 24); b[at + 1] = (byte)(v >> 16);
        b[at + 2] = (byte)(v >> 8); b[at + 3] = (byte)v;
    }

    private static readonly uint[] CrcTable = BuildCrcTable();

    private static uint[] BuildCrcTable()
    {
        var t = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            t[n] = c;
        }
        return t;
    }

    private static uint Crc32(byte[] buf)
    {
        uint c = 0xFFFFFFFFu;
        foreach (byte v in buf) c = CrcTable[(c ^ v) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFFu;
    }
}
