using POPGame.Dos;

namespace POPGame.Sim;

/// <summary>Side effects a sequence can raise that the caller has to act on.</summary>
public sealed class SeqEffects
{
    public bool Footstep, SmackWall, AlertGuard;
    public bool Died, NextLevel, DrankPotion;
    public int JarFloor;      // +1 = jar the floorboards above, -1 = below

    public void Clear()
    {
        Footstep = SmackWall = AlertGuard = Died = NextLevel = DrankPotion = false;
        JarFloor = 0;
    }
}

/// <summary>
/// The sequence-table interpreter — a direct port of ANIMCHAR (COLL.S:994).
///
/// Each call consumes opcodes from the character's sequence cursor until it hits a
/// positive byte, which is the frame number to display this tick. Opcodes mutate
/// the character in place: chx/chy move it, up/down change its row, act sets the
/// action, setfall seeds the fall velocities, goto jumps.
/// </summary>
public sealed class SeqRunner
{
    private readonly DosTables _tables;

    public SeqRunner(DosTables tables) => _tables = tables;

    /// <summary>Points a character at the start of sequence <paramref name="seqId"/>.</summary>
    public void Start(CharState ch, int seqId)
    {
        ch.CurrentSeq = seqId;
        ch.SeqPtr = _tables.SeqStart[seqId];
    }

    /// <summary>
    /// Advances one animation frame. Returns false if the bytecode ran away, which
    /// would mean a bad jump rather than anything the game does on purpose.
    /// </summary>
    public bool Animate(CharState ch, SeqEffects fx, bool weightless = false)
    {
        byte[] seq = _tables.Seq;

        for (int guard = 0; guard < 256; guard++)
        {
            if (ch.SeqPtr < 0 || ch.SeqPtr >= seq.Length) return false;
            byte b = seq[ch.SeqPtr++];

            switch ((SeqOp)b)
            {
                case SeqOp.ChX:
                    ch.AddX((sbyte)seq[ch.SeqPtr++]);
                    continue;

                case SeqOp.ChY:
                    ch.Y += (sbyte)seq[ch.SeqPtr++];
                    continue;

                case SeqOp.AboutFace:
                    ch.Flip();
                    continue;

                case SeqOp.Goto:
                    ch.SeqPtr = seq[ch.SeqPtr] | (seq[ch.SeqPtr + 1] << 8);
                    continue;

                case SeqOp.Up:
                    ch.Row--;
                    continue;

                case SeqOp.Down:
                    ch.Row++;
                    continue;

                case SeqOp.Act:
                    ch.Action = (CharAction)seq[ch.SeqPtr++];
                    continue;

                case SeqOp.SetFall:
                    ch.XVel = (sbyte)seq[ch.SeqPtr++];
                    ch.YVel = (sbyte)seq[ch.SeqPtr++];
                    continue;

                case SeqOp.IfWtLess:
                    // Jump when weightless (feather fall), otherwise step over the word.
                    if (weightless) ch.SeqPtr = seq[ch.SeqPtr] | (seq[ch.SeqPtr + 1] << 8);
                    else ch.SeqPtr += 2;
                    continue;

                case SeqOp.Die:
                    fx.Died = true;
                    continue;

                case SeqOp.JarU:
                    fx.JarFloor = 1;
                    continue;

                case SeqOp.JarD:
                    fx.JarFloor = -1;
                    continue;

                case SeqOp.Tap:
                    switch (seq[ch.SeqPtr++])
                    {
                        case 0: fx.AlertGuard = true; break;
                        case 1: fx.Footstep = true; fx.AlertGuard = true; break;
                        case 2: fx.SmackWall = true; fx.AlertGuard = true; break;
                    }
                    continue;

                case SeqOp.Effect:
                    if (seq[ch.SeqPtr++] == 1) fx.DrankPotion = true;
                    continue;

                case SeqOp.NextLevel:
                    fx.NextLevel = true;
                    continue;

                default:
                    // Any other value is the frame number to display.
                    ch.Frame = b;
                    return true;
            }
        }

        return false;
    }

    public FrameDef Frame(CharState ch) => _tables.Frames[ch.Frame];
}
