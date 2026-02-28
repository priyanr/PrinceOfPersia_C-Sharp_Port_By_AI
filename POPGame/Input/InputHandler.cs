namespace POPGame.Input;

/// <summary>
/// Polls Console.KeyAvailable each frame and fills InputState with fresh-press flags.
/// </summary>
public class InputHandler
{
    private readonly InputState _state = new();

    // Track what was held last frame to compute rising-edge
    private bool _prevLeft, _prevRight, _prevUp, _prevDown, _prevAction;

    public InputState State => _state;

    /// <summary>Drain all pending key events and update state. Call once per tick.</summary>
    public void Poll()
    {
        // Reset held flags
        _state.Left   = false;
        _state.Right  = false;
        _state.Up     = false;
        _state.Down   = false;
        _state.Action = false;
        _state.Pause  = false;
        _state.AnyFresh = false;

        while (Console.KeyAvailable)
        {
            var ki = Console.ReadKey(intercept: true);
            switch (ki.Key)
            {
                case ConsoleKey.LeftArrow:  _state.Left   = true; break;
                case ConsoleKey.RightArrow: _state.Right  = true; break;
                case ConsoleKey.UpArrow:    _state.Up     = true; break;
                case ConsoleKey.DownArrow:  _state.Down   = true; break;
                case ConsoleKey.Spacebar:   _state.Action = true; break;
                case ConsoleKey.Escape:     _state.Pause  = true; break;
            }
            _state.AnyFresh = true;
        }

        // Compute fresh-press (rising edge)
        _state.FreshLeft   = _state.Left   && !_prevLeft;
        _state.FreshRight  = _state.Right  && !_prevRight;
        _state.FreshUp     = _state.Up     && !_prevUp;
        _state.FreshDown   = _state.Down   && !_prevDown;
        _state.FreshAction = _state.Action && !_prevAction;

        _prevLeft   = _state.Left;
        _prevRight  = _state.Right;
        _prevUp     = _state.Up;
        _prevDown   = _state.Down;
        _prevAction = _state.Action;
    }
}
