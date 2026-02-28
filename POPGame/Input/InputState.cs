namespace POPGame.Input;

/// <summary>
/// Joystick / keyboard state for one frame.
/// Fresh flags are true only on the first frame a key is down.
/// </summary>
public class InputState
{
    // Held-down flags
    public bool Left;
    public bool Right;
    public bool Up;
    public bool Down;
    public bool Action;  // Shift or Space – strike / grab
    public bool Pause;   // Esc

    // Fresh-press (rising-edge) flags
    public bool FreshLeft;
    public bool FreshRight;
    public bool FreshUp;
    public bool FreshDown;
    public bool FreshAction;

    // Any key just pressed (for "press any key" prompts)
    public bool AnyFresh;

    public void ClearFresh()
    {
        FreshLeft = FreshRight = FreshUp = FreshDown = FreshAction = false;
        AnyFresh  = false;
    }
}
