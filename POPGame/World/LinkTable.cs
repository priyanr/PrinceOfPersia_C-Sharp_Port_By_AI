using POPGame.Data;

namespace POPGame.World;

/// <summary>
/// Resolves LINKLOC / LINKMAP trigger chains.
/// LINKLOC[n] = target cell index on target screen.
/// LINKMAP[n] = target screen (1-indexed; 0 = end of chain).
/// The chain: starting from trigger cell, walk LINKLOC until LINKMAP entry is 0.
/// </summary>
public class LinkTable
{
    private readonly Level _level;

    public LinkTable(Level level) => _level = level;

    /// <summary>
    /// Enumerate all (targetScreen1, targetCell) pairs linked from (trigScrn1, trigCell).
    /// </summary>
    public IEnumerable<(int Screen1, int Cell)> Resolve(int trigScrn1, int trigCell)
    {
        // The trigger address in the link arrays is encoded as:
        //   key = (trigScrn1 - 1) * 30 + trigCell
        // We search LINKLOC for all entries where LINKMAP matches the trigger screen
        // and the cell matches. The Apple II code uses a flat index into the link arrays.
        // Here we do a simple scan: find indices i where LinkMap[i]==trigScrn1 is NOT how
        // it works — the real scheme stores the trigger position directly at each entry.
        //
        // Actual POP Apple II scheme (CTRLSUBS.S FINDLINK):
        //   The level file stores up to 256 (screen, cell) trigger→target pairs.
        //   LINKLOC[i] = target cell;  LINKMAP[i] = target screen (1-indexed).
        //   Index i corresponds to source cell (i%30) on source screen ((i/30)+1).
        //
        // So the lookup is direct: i = (trigScrn1-1)*30 + trigCell
        int idx = (trigScrn1 - 1) * 30 + trigCell;
        if (idx < 0 || idx >= 256) yield break;

        int targetCell   = _level.LinkLoc[idx];
        int targetScreen = _level.LinkMap[idx];
        if (targetScreen == 0) yield break;

        yield return (targetScreen, targetCell);
    }
}
