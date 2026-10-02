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
/// The animation tables lifted out of the DOS PRINCE.EXE. The file is EXEPACK
/// compressed, so the tables are read from the unpacked load image
/// (<see cref="ExePack.Unpack"/>), not from the file: in the file, runs such as
/// medland's 29 x frame 109 are stored as fill/copy commands, and every sequence
/// after them is shifted. Offsets were located by matching byte patterns from the
/// Apple II FRAMEDEF.S / SEQTABLE.S sources.
///
/// The bytecode layout is identical to the Apple II source: a positive byte is a
/// frame number, a negative byte is an opcode (see <see cref="SeqOp"/>).
/// </summary>
public sealed class DosTables
{
    // Offsets into the unpacked load image (no MZ header).
    private const int FrameTableOffset = 0x1BCC5;   // frame N at +(N-1)*5
    private const int SeqBytecodeBase  = 0x1ACE0;   // offset 0 of the sequence bytecode
    private const int SeqIndexBase     = 0x1C568;   // words, 1-indexed by sequence id

    public const int FrameCount = 240;
    public const int SequenceCount = 114;

    public FrameDef[] Frames { get; }
    /// <summary>Raw sequence bytecode; goto targets index into this array.</summary>
    public byte[] Seq { get; }
    /// <summary>Entry offset into <see cref="Seq"/> for each sequence id (1-based).</summary>
    public ushort[] SeqStart { get; }

    public DosTables(string princeExePath)
    {
        byte[] exe = ExePack.Unpack(File.ReadAllBytes(princeExePath));

        Frames = new FrameDef[FrameCount + 1];      // 1-based
        Frames[0] = new FrameDef(0xFF, 0, 0, 0, 0);  // frame 0 is blank, as in the original
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

    // ── guards (SDLPoP custom options; the originals sit in PRINCE.EXE's data segment) ──

    public const int GuardSkills = 12;

    // SDLPoP's defaults, which are the DOS game's own values. They are not read from
    // PRINCE.EXE yet (SDLPoP lists the data-segment offsets per version; which one this
    // install is has to be established with the real file) -- see PENDING.md.
    private static readonly ushort[] DefStrikeProb   = [61, 100, 61, 61, 61, 40, 100, 220, 0, 48, 32, 48];
    private static readonly ushort[] DefRestrikeProb = [0, 0, 0, 5, 5, 175, 16, 8, 0, 255, 255, 150];
    private static readonly ushort[] DefBlockProb    = [0, 150, 150, 200, 200, 255, 200, 250, 0, 255, 255, 255];
    private static readonly ushort[] DefImpBlockProb = [0, 61, 61, 100, 100, 145, 100, 250, 0, 145, 255, 175];
    private static readonly ushort[] DefAdvProb      = [255, 200, 200, 200, 255, 255, 200, 0, 0, 255, 100, 100];
    private static readonly ushort[] DefRefracTimer  = [16, 16, 16, 16, 8, 8, 8, 8, 0, 8, 0, 0];
    private static readonly ushort[] DefExtraStrength = [0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0];
    private static readonly byte[] DefGuardHp = [4, 3, 3, 3, 3, 4, 5, 4, 4, 5, 5, 5, 4, 6, 0, 0];
    private static readonly short[] DefGuardType = [0, 0, 0, 2, 0, 0, 1, 0, 0, 0, 0, 0, 4, 3, -1, -1];

    public ushort[] StrikeProb { get; private set; } = DefStrikeProb;
    public ushort[] RestrikeProb { get; private set; } = DefRestrikeProb;
    public ushort[] BlockProb { get; private set; } = DefBlockProb;
    public ushort[] ImpBlockProb { get; private set; } = DefImpBlockProb;
    public ushort[] AdvProb { get; private set; } = DefAdvProb;
    public ushort[] RefracTimer { get; private set; } = DefRefracTimer;
    public ushort[] ExtraStrength { get; private set; } = DefExtraStrength;
    /// <summary>tbl_guard_hp per level 0..15.</summary>
    public byte[] GuardHp { get; private set; } = DefGuardHp;
    /// <summary>tbl_guard_type per level: -1 none, 0 guard, 1 fat, 2 skeleton, 3 vizier, 4 shadow.</summary>
    public short[] GuardType { get; private set; } = DefGuardType;

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

        // medland (sequence 20) holds frame 108 then 109 x 29 (SEQTABLE.S:1352). In the
        // packed file this run is a fill command, so this catches a packed read.
        p = SeqStart[20];
        int at108 = Array.IndexOf(Seq, (byte)108, p, 16);
        if (at108 < 0 || Enumerable.Range(at108 + 1, 29).Any(i => Seq[i] != 109))
            return $"sequence 20 (medland) does not hold frame 109 x 29 at 0x{p:x4}";

        // Frame 15 is the standing kid: chtab image 14, sword 9, no offset.
        if (Frames[15].Image != 14 || Frames[15].Sword != 9)
            return $"frame 15 is {Frames[15]}, expected image 14 / sword 9";

        return null;
    }
}
