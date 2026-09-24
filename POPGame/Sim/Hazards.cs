using POPGame.Data;
using POPGame.Engine;

namespace POPGame.Sim;

/// <summary>
/// Per-tick world state: gates opening and closing, pressure-plate timers, loose
/// floors giving way, and the exit door. Level 1 needs all of these to be finishable.
/// </summary>
public sealed class Hazards
{
    private readonly Level _level;
    private readonly Links _links;

    // Loose floors that have been stepped on: key -> ticks since triggered.
    private readonly Dictionary<(int Screen, int Cell), int> _looseShaking = new();

    /// <summary>Set once the kid reaches an opened exit.</summary>
    public bool LevelComplete { get; private set; }

    public Hazards(Level level)
    {
        _level = level;
        _links = new Links(level);
    }

    public void Tick(CharState kid, SeqEffects fx)
    {
        StandOnTile(kid);
        TickPlates();
        TickGates();
        TickLoose(kid);
    }

    // ── the tile the kid is standing on ───────────────────────────────────────

    private void StandOnTile(CharState kid)
    {
        int col = kid.BlockX;
        if (col is < 0 or >= Coord.Cols) return;
        if (kid.Row is < 0 or >= Coord.Rows) return;

        int s0 = kid.Room - 1;
        if (s0 < 0 || s0 >= Level.NumScreens) return;

        var tile = _level.GetTileId(s0, kid.Row, col);
        int cell = kid.Row * Coord.Cols + col;

        switch (tile)
        {
            case TileId.PressPlate:
                // Tile 6 is a CLOSER: it slams its gates shut rather than raising them.
                PushPlate(_level.GetSpec(s0, kid.Row, col), close: true);
                break;

            case TileId.UPressPlate:
            case TileId.DPressPlate:
                PushPlate(_level.GetSpec(s0, kid.Row, col), close: false);
                break;

            case TileId.Loose:
                _looseShaking.TryAdd((kid.Room, cell), 0);
                break;

            case TileId.Exit:
            case TileId.Exit2:
                if (_exitOpen) LevelComplete = true;
                break;
        }
    }

    /// <summary>
    /// PUSHPP (MOVER.S:425): hold the plate down for pptimer ticks, and trigger
    /// everything on its link chain. A timer of 31 means permanently held.
    /// </summary>
    private void PushPlate(int linkIndex, bool close)
    {
        if (linkIndex is < 0 or > 255) return;

        if (_links.Timer(linkIndex) != 31)
            _links.SetTimer(linkIndex, Constants.PPTimer);

        foreach (var t in _links.Chain(linkIndex))
            Trigger(t.Screen, t.Cell, close);

        _activePlates.Add(linkIndex);
    }

    private readonly HashSet<int> _activePlates = [];
    private bool _exitOpen;

    /// <summary>
    /// Presses the button at a cell as if the kid had stepped on it. Level 1 uses this
    /// at the start: the original presses the closer in room 5 as the kid drops in, so
    /// the gate by the first room — authored open — slams shut (DO_STARTPOS).
    /// </summary>
    public void PressButton(int room, int row, int col)
    {
        var tile = _level.GetTileId(room - 1, row, col);
        if (tile is not (TileId.PressPlate or TileId.UPressPlate or TileId.DPressPlate)) return;
        PushPlate(_level.GetSpec(room - 1, row, col), close: tile == TileId.PressPlate);
    }

    private void Trigger(int screen, int cell, bool close)
    {
        if (screen < 1 || screen > Level.NumScreens) return;
        if (cell is < 0 or >= Level.CellsPerScreen) return;

        int s0 = screen - 1;
        int row = cell / Coord.Cols, col = cell % Coord.Cols;

        switch (_level.GetTileId(s0, row, col))
        {
            case TileId.Gate:
                if (close)
                {
                    _gateHold.Remove((screen, cell));
                    _gateClosing.Remove((screen, cell));
                    _gateSlamming.Add((screen, cell));
                }
                else
                {
                    // A triggered gate is driven open and held while its plate is down.
                    _gateSlamming.Remove((screen, cell));
                    _gateHold[(screen, cell)] = Constants.GateTimer;
                }
                break;

            case TileId.Exit:
            case TileId.Exit2:
                _exitOpen = true;
                break;
        }
    }

    // ── gates ─────────────────────────────────────────────────────────────────

    private readonly Dictionary<(int Screen, int Cell), int> _gateHold = new();

    /// <summary>
    /// Gates that have been raised and are now coming back down. A gate that was never
    /// triggered is left at the height the level authored, open or shut — the original
    /// never winds an untouched gate, and doing so shortens a shut one by a pixel.
    /// </summary>
    private readonly HashSet<(int Screen, int Cell)> _gateClosing = [];

    /// <summary>Gates a closer has been pressed for: they drop fast, all the way.</summary>
    private readonly HashSet<(int Screen, int Cell)> _gateSlamming = [];

    /// <summary>How far a slammed gate drops per tick, in gate-height units.</summary>
    private const int GateSlamStep = 10;

    private void TickGates()
    {
        var done = new List<(int, int)>();

        foreach (var key in _gateHold.Keys.ToList())
        {
            var (screen, cell) = key;
            int s0 = screen - 1, row = cell / Coord.Cols, col = cell % Coord.Cols;

            // A triggered gate winds up fast; height counts up towards fully raised.
            int spec = _level.GetSpec(s0, row, col);
            _level.SetSpec(s0, row, col, (byte)Math.Min(RoomView.GateOpen, spec + 4));

            if (--_gateHold[key] <= 0) done.Add(key);
        }

        foreach (var key in done) { _gateHold.Remove(key); _gateClosing.Add(key); }

        foreach (var key in _gateSlamming.ToList())
        {
            var (screen, cell) = key;
            int s0 = screen - 1, row = cell / Coord.Cols, col = cell % Coord.Cols;

            int spec = _level.GetSpec(s0, row, col);
            if (spec > 0) _level.SetSpec(s0, row, col, (byte)Math.Max(0, spec - GateSlamStep));
            else _gateSlamming.Remove(key);
        }

        // A gate whose hold has run out drops shut again.
        foreach (var key in _gateClosing.ToList())
        {
            var (screen, cell) = key;
            int s0 = screen - 1, row = cell / Coord.Cols, col = cell % Coord.Cols;

            int spec = _level.GetSpec(s0, row, col);
            if (spec > 0) _level.SetSpec(s0, row, col, (byte)Math.Max(0, spec - 2));
            else _gateClosing.Remove(key);
        }
    }

    private void TickPlates()
    {
        foreach (int index in _activePlates.ToList())
        {
            int t = _links.Timer(index);
            if (t is 0 or 31) { _activePlates.Remove(index); continue; }
            _links.SetTimer(index, t - 1);
        }
    }

    // ── loose floors ──────────────────────────────────────────────────────────

    private void TickLoose(CharState kid)
    {
        foreach (var key in _looseShaking.Keys.ToList())
        {
            int ticks = ++_looseShaking[key];
            if (ticks < Constants.LooseTimer) continue;

            var (screen, cell) = key;
            int s0 = screen - 1, row = cell / Coord.Cols, col = cell % Coord.Cols;

            // The board drops out, leaving a hole; rubble lands on the floor below.
            _level.SetTileType(s0, row, col, TileId.Space);
            if (row + 1 < Coord.Rows && _level.GetTileId(s0, row + 1, col) == TileId.Floor)
                _level.SetTileType(s0, row + 1, col, TileId.Rubble);

            _looseShaking.Remove(key);
        }
    }
}
