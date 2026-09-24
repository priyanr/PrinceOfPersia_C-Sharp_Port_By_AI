namespace POPGame.Dos;

/// <summary>
/// Undoes Microsoft EXEPACK, which PRINCE.EXE is compressed with.
///
/// EXEPACK only squeezes runs of repeated bytes, so most of the image survives
/// verbatim in the file — which is why the frame and sequence tables can be read in
/// place. But any table holding a long run of zeros (the tile drawing table has
/// ten in a row) is stored as a fill command and has to be expanded first.
///
/// The format: an 18-byte header sits at CS:0 of the packed program (signature "RB"),
/// and the packed image is everything before it. Decompression runs backwards from
/// the end of the packed image to the end of the destination buffer, one command at
/// a time: <c>0xB0</c> fills a run with one byte, <c>0xB2</c> copies a run verbatim,
/// and bit 0 of a command marks the last one.
/// </summary>
public static class ExePack
{
    /// <summary>Returns the unpacked load image (the program without its MZ header).</summary>
    public static byte[] Unpack(byte[] exe)
    {
        int headerBytes = Word(exe, 0x08) * 16;
        int cs = Word(exe, 0x16);
        int ep = headerBytes + cs * 16;

        if (exe.Length < ep + 18 || exe[ep + 16] != 'R' || exe[ep + 17] != 'B')
            throw new InvalidDataException("not an EXEPACK executable");

        int destLen = Word(exe, ep + 12) * 16;
        int skipLen = Word(exe, ep + 14);
        int packedLen = cs * 16 - (skipLen - 1) * 16;

        var buf = new byte[Math.Max(destLen, packedLen)];
        Array.Copy(exe, headerBytes, buf, 0, packedLen);

        int src = packedLen, dst = destLen;

        // Up to 15 bytes of 0xFF padding separate the image from the first command.
        for (int i = 0; i < 16 && buf[src - 1] == 0xFF; i++) src--;

        while (true)
        {
            byte cmd = buf[--src];
            int len = buf[--src] << 8;
            len |= buf[--src];

            switch (cmd & 0xFE)
            {
                case 0xB0:
                    byte fill = buf[--src];
                    for (int i = 0; i < len; i++) buf[--dst] = fill;
                    break;
                case 0xB2:
                    for (int i = 0; i < len; i++) buf[--dst] = buf[--src];
                    break;
                default:
                    throw new InvalidDataException($"bad EXEPACK command 0x{cmd:x2} at {src}");
            }

            if ((cmd & 1) != 0) break;
        }

        return buf[..destLen];
    }

    private static int Word(byte[] b, int o) => b[o] | b[o + 1] << 8;
}
