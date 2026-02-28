using POPGame.Characters;
using POPGame.Data;

namespace POPGame.Engine;

public enum GamePhase
{
    Title,
    Playing,
    Paused,
    Dead,
    NextLevel,
    YouWin,
    Quit,
}

/// <summary>
/// All mutable runtime state for a single game session.
/// </summary>
public class GameState
{
    public GamePhase Phase = GamePhase.Title;

    // Level
    public int    LevelIndex = 1;      // 1-indexed, matches LEVEL1 file
    public Level  Level      = null!;
    public string LevelName  = "";

    // Characters
    public CharState Player = new();
    public CharState[] Guards = Array.Empty<CharState>();

    // Time
    public int  FrameCount     = 0;    // total ticks since level start
    public int  TimerFrames    = 0;    // game-minute timer
    public bool ExitOpen       = false; // whether the exit is unlocked

    // Status messages (shown briefly on HUD)
    public string StatusMessage = "";
    public int    StatusTimer   = 0;   // frames remaining to show message

    public void SetStatus(string msg, int frames = 30)
    {
        StatusMessage = msg;
        StatusTimer   = frames;
    }

    public void TickStatus()
    {
        if (StatusTimer > 0) StatusTimer--;
        if (StatusTimer == 0) StatusMessage = "";
    }
}
