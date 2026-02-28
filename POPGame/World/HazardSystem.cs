using POPGame.Characters;
using POPGame.Data;
using POPGame.Engine;

namespace POPGame.World;

/// <summary>
/// Updates all transitional hazard objects (gates, spikes, slicer, loose floors,
/// pressure plates) each game tick based on constants from the 6502 source.
/// </summary>
public class HazardSystem
{
    private readonly Level      _level;
    private readonly LinkTable  _links;

    // Pressure-plate timers: key = (screen1, cell), value = remaining frames
    private readonly Dictionary<(int, int), int> _ppTimers = new();

    // Gate timers: key = (screen1, cell), value = remaining open frames
    private readonly Dictionary<(int, int), int> _gateTimers = new();

    // Loose floor state: key = (screen1, cell), value = frames since triggered (0=intact)
    private readonly Dictionary<(int, int), int> _looseFrames = new();

    // Slicer / spike per-cell frame counter (resets each period)
    private readonly Dictionary<(int, int), int> _hazardFrames = new();

    public HazardSystem(Level level, LinkTable links)
    {
        _level = level;
        _links = links;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Public interface
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Called by the game loop each tick before character updates.</summary>
    public void Update(GameState state)
    {
        int scrn1 = state.Player.Screen;
        int s0    = scrn1 - 1;

        for (int cell = 0; cell < Level.CellsPerScreen; cell++)
        {
            int row = cell / Level.Cols;
            int col = cell % Level.Cols;
            TileId tile = _level.GetTileId(s0, row, col);

            switch (tile)
            {
                case TileId.Gate:        TickGate(scrn1, cell, row, col, s0); break;
                case TileId.Spikes:      TickSpikes(scrn1, cell, row, col, s0, state); break;
                case TileId.Slicer:      TickSlicer(scrn1, cell, row, col, s0, state); break;
                case TileId.Loose:       TickLoose(scrn1, cell, row, col, s0, state); break;
                case TileId.PressPlate:
                case TileId.UPressPlate: TickPressPlate(scrn1, cell, row, col, s0, state); break;
            }
        }

        TickPpTimers();
    }

    /// <summary>Trigger a pressure plate manually (e.g. when kid steps on it).</summary>
    public void TriggerPressPlate(int scrn1, int cell)
    {
        var key = (scrn1, cell);
        _ppTimers[key] = Constants.PPTimer;

        // Resolve linked targets and open their gates
        foreach (var (tScrn, tCell) in _links.Resolve(scrn1, cell))
            TriggerGate(tScrn, tCell);
    }

    /// <summary>Trigger a loose floor (e.g. when kid stands on it).</summary>
    public void TriggerLoose(int scrn1, int cell)
    {
        var key = (scrn1, cell);
        if (!_looseFrames.ContainsKey(key))
            _looseFrames[key] = 1; // start countdown
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Internal tick helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void TickGate(int scrn1, int cell, int row, int col, int s0)
    {
        byte spec = _level.GetSpecFlat(s0, cell);
        var key   = (scrn1, cell);

        if (_gateTimers.TryGetValue(key, out int timeLeft))
        {
            // Gate is opening/open
            if (spec > 0)
            {
                int next = Math.Max(0, spec - Constants.GateStep);
                _level.SetSpecFlat(s0, cell, (byte)next);
            }

            if (timeLeft <= 0)
            {
                // Auto-close
                _gateTimers.Remove(key);
            }
            else
            {
                _gateTimers[key] = timeLeft - 1;
            }
        }
        else
        {
            // Gate is closing — restore spec toward GMaxVal slowly
            if (spec < Constants.GMaxVal)
            {
                int next = Math.Min(Constants.GMaxVal, spec + 4);
                _level.SetSpecFlat(s0, cell, (byte)next);
            }
        }
    }

    private void TickSpikes(int scrn1, int cell, int row, int col, int s0, GameState state)
    {
        var key = (scrn1, cell);
        if (!_hazardFrames.ContainsKey(key)) _hazardFrames[key] = 0;
        int f = _hazardFrames[key];

        bool dangerous = f < Constants.SpikeExt;
        _level.SetSpecFlat(s0, cell, (byte)(dangerous ? 1 : 0));

        // Check if player is on this cell while spikes are extended
        if (dangerous && IsPlayerOnCell(state.Player, scrn1, row, col))
            state.Player.TakeDamage(state.Player.Life); // instant kill

        _hazardFrames[key] = (f + 1) % Constants.SpikeTimer;
    }

    private void TickSlicer(int scrn1, int cell, int row, int col, int s0, GameState state)
    {
        var key = (scrn1, cell);
        if (!_hazardFrames.ContainsKey(key)) _hazardFrames[key] = 0;
        int f = _hazardFrames[key];

        bool lethal = f < Constants.SlicerExt;
        _level.SetSpecFlat(s0, cell, (byte)(lethal ? 1 : 0));

        if (lethal && IsPlayerOnCell(state.Player, scrn1, row, col))
            state.Player.TakeDamage(state.Player.Life); // instant kill

        _hazardFrames[key] = (f + 1) % Constants.SliceTimer;
    }

    private void TickLoose(int scrn1, int cell, int row, int col, int s0, GameState state)
    {
        var key = (scrn1, cell);

        // Check if player standing on this cell to trigger it
        if (IsPlayerOnCell(state.Player, scrn1, row, col) && !_looseFrames.ContainsKey(key))
            _looseFrames[key] = 1;

        if (!_looseFrames.TryGetValue(key, out int f)) return;

        _looseFrames[key] = f + 1;
        byte spec = (byte)Math.Min(f, 255);
        _level.SetSpecFlat(s0, cell, spec);

        if (f >= Constants.LooseTimer)
        {
            // Floor has fallen — replace with space
            _level.SetTileType(s0, row, col, TileId.Space);
            _looseFrames.Remove(key);
        }
    }

    private void TickPressPlate(int scrn1, int cell, int row, int col, int s0, GameState state)
    {
        // Activation is handled externally in PhysicsEngine when kid lands
        // Here we just keep the spec visual updated
        var key = (scrn1, cell);
        bool active = _ppTimers.ContainsKey(key) && _ppTimers[key] > 0;
        _level.SetSpecFlat(s0, cell, (byte)(active ? 1 : 0));
    }

    private void TickPpTimers()
    {
        var keys = new List<(int, int)>(_ppTimers.Keys);
        foreach (var k in keys)
        {
            _ppTimers[k]--;
            if (_ppTimers[k] <= 0) _ppTimers.Remove(k);
        }
    }

    private void TriggerGate(int scrn1, int cell)
    {
        _gateTimers[(scrn1, cell)] = Constants.GateTimer;
    }

    private static bool IsPlayerOnCell(CharState player, int scrn1, int row, int col)
        => player.Screen == scrn1 && player.BlockY == row && player.BlockX == col;
}
