namespace POPGame.Dos;

/// <summary>One entry of the 240-slot frame table.</summary>
public readonly record struct FrameDef(byte Image, byte Sword, sbyte Dx, sbyte Dy, byte Flags)
{
    /// <summary>Frames with image 255 are blank placeholders in the table.</summary>
    public bool IsBlank => Image == 0xFF;

    // Flags, from SEQDATA.S: fcheckmark = %01000000, fcentermark = %00011111.
    public bool Check => (Flags & 0x40) != 0;
    public int  Center => Flags & 0x1F;
    public bool Thin => (Flags & 0x20) != 0;
    public bool Odd => (Flags & 0x80) != 0;
}

/// <summary>
/// Sequence-table opcodes. Stored as negative bytes in the bytecode; the values
/// here are the raw byte values (two's complement of the SEQDATA.S constants).
/// </summary>
public enum SeqOp : byte
{
    Goto      = 0xFF, // -1,  followed by a word: absolute offset into the bytecode
    AboutFace = 0xFE, // -2
    Up        = 0xFD, // -3
    Down      = 0xFC, // -4
    ChX       = 0xFB, // -5,  followed by a signed delta applied in the facing direction
    ChY       = 0xFA, // -6,  followed by a signed delta
    Act       = 0xF9, // -7,  followed by the new action id
    SetFall   = 0xF8, // -8,  followed by xvel, yvel
    IfWtLess  = 0xF7, // -9,  goto if weightless, else skip the word
    Die       = 0xF6, // -10
    JarU      = 0xF5, // -11
    JarD      = 0xF4, // -12
    Effect    = 0xF3, // -13, followed by an effect id
    Tap       = 0xF2, // -14, followed by a sound id
    NextLevel = 0xF1, // -15
}

/// <summary>
/// The animation tables lifted straight out of the DOS PRINCE.EXE, which is not
/// packed. Offsets were located by matching byte patterns from the Apple II
/// FRAMEDEF.S / SEQTABLE.S sources and cross-checked against two anchors each.
///
/// The bytecode layout is identical to the Apple II source: a positive byte is a
/// frame number, a negative byte is an opcode (see <see cref="SeqOp"/>).
/// </summary>
public sealed class DosTables
{
    private const int FrameTableOffset = 0x1B9AA;   // frame N at +(N-1)*5
    private const int SeqBytecodeBase  = 0x1A8ED;   // offset 0 of the sequence bytecode
    private const int SeqIndexBase     = 0x1C175;   // words, 1-indexed by sequence id

    public const int FrameCount = 240;
    public const int SequenceCount = 114;

    public FrameDef[] Frames { get; }
    /// <summary>Raw sequence bytecode; goto targets index into this array.</summary>
    public byte[] Seq { get; }
    /// <summary>Entry offset into <see cref="Seq"/> for each sequence id (1-based).</summary>
    public ushort[] SeqStart { get; }

    public DosTables(string princeExePath)
    {
        byte[] exe = File.ReadAllBytes(princeExePath);

        Frames = new FrameDef[FrameCount + 1];      // 1-based
        for (int n = 1; n <= FrameCount; n++)
        {
            int o = FrameTableOffset + (n - 1) * 5;
            Frames[n] = new FrameDef(exe[o], exe[o + 1], (sbyte)exe[o + 2], (sbyte)exe[o + 3], exe[o + 4]);
        }

        SeqStart = new ushort[SequenceCount + 1];
        int maxEnd = 0;
        for (int s = 1; s <= SequenceCount; s++)
        {
            int o = SeqIndexBase + s * 2;
            SeqStart[s] = (ushort)(exe[o] | (exe[o + 1] << 8));
            if (SeqStart[s] > maxEnd) maxEnd = SeqStart[s];
        }

        // The bytecode runs from the base past the last entry point; copy generously
        // and let goto targets bound themselves.
        int seqLen = maxEnd + 512;
        Seq = new byte[seqLen];
        Array.Copy(exe, SeqBytecodeBase, Seq, 0, Math.Min(seqLen, exe.Length - SeqBytecodeBase));
    }

    public static DosTables Load() => new(DosGame.File("PRINCE.EXE"));

    /// <summary>
    /// Sanity check that the offsets still line up with the expected executable.
    /// Returns null when everything matches, otherwise a description of the mismatch.
    /// </summary>
    public string? Validate()
    {
        // startrun (sequence 1) begins with act,1 then frames 1,2,3,4.
        int p = SeqStart[1];
        if (Seq[p] != (byte)SeqOp.Act || Seq[p + 1] != 1 ||
            Seq[p + 2] != 1 || Seq[p + 3] != 2 || Seq[p + 4] != 3)
            return $"sequence 1 (startrun) does not start with act,1,1,2,3 at 0x{p:x4}";

        // stand (sequence 2) is act,0 then frame 15 then goto.
        p = SeqStart[2];
        if (Seq[p] != (byte)SeqOp.Act || Seq[p + 1] != 0 || Seq[p + 2] != 15)
            return $"sequence 2 (stand) does not start with act,0,15 at 0x{p:x4}";

        // Frame 15 is the standing kid: chtab image 14, sword 9, no offset.
        if (Frames[15].Image != 14 || Frames[15].Sword != 9)
            return $"frame 15 is {Frames[15]}, expected image 14 / sword 9";

        return null;
    }
}
