using POPGame.Data;

namespace POPGame.Sim;

/// <summary>
/// LINKLOC / LINKMAP decoding, from the getloc / getscrn / gettimer helpers in
/// MOVER.S:1543. A pressure plate's BLUESPEC byte is an index into these arrays;
/// entries are walked from there until one carries the last-entry flag.
///
///   LINKLOC[i] &amp; 0x1F  target cell (0-29)
///   LINKLOC[i] &amp; 0x60  low 2 bits of the target screen
///   LINKLOC[i] &amp; 0x80  last-entry flag
///   LINKMAP[i] &amp; 0xE0  high 3 bits of the target screen
///   LINKMAP[i] &amp; 0x1F  pressure-plate timer (0-31); 31 means permanently held down
/// </summary>
public sealed class Links
{
    public readonly record struct Target(int Screen, int Cell);

    private readonly Level _level;

    public Links(Level level) => _level = level;

    public int Timer(int index) => _level.LinkMap[index] & 0x1F;

    public void SetTimer(int index, int value) =>
        _level.LinkMap[index] = (byte)((_level.LinkMap[index] & 0xE0) | (value & 0x1F));

    public bool IsLast(int index) => (_level.LinkLoc[index] & 0x80) != 0;

    public bool LinksToNothing(int index) => _level.LinkLoc[index] == 0xFF;

    public int Cell(int index) => _level.LinkLoc[index] & 0x1F;

    public int Screen(int index)
    {
        int low = (_level.LinkLoc[index] & 0x60) >> 2;   // -> bits 3-4
        int high = _level.LinkMap[index] & 0xE0;         // bits 5-7
        return (high + low) >> 3;
    }

    /// <summary>Walks the chain of gadgets a plate triggers, starting at its link index.</summary>
    public IEnumerable<Target> Chain(int startIndex)
    {
        for (int i = startIndex; i >= 0 && i < 256; i++)
        {
            if (!LinksToNothing(i))
                yield return new Target(Screen(i), Cell(i));

            if (IsLast(i)) yield break;
        }
    }
}
