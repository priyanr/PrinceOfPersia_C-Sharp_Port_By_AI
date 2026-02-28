using System.Numerics;
using Raylib_cs;
using POPGame.Characters;
using POPGame.Data;
using POPGame.Input;
using POPGame.Rendering;
using POPGame.World;

namespace POPGame.Engine;

/// <summary>
/// Main game loop — Raylib window at 640×480, rendering into a 320×240 virtual canvas
/// scaled ×2. Game logic ticks at 10 fps (SetTargetFPS(10) matches 100 ms/frame).
/// </summary>
public class GameLoop
{
    private readonly GameState      _state;
    private readonly RaylibInput    _raylibInput;
    private readonly RaylibRenderer _raylibRenderer;
    private readonly InputState     _inputState;
    private readonly string         _levelsDir;

    // Subsystems — rebuilt on each LoadLevel
    private HazardSystem     _hazards  = null!;
    private PlayerController _player   = null!;
    private GuardController  _guard    = null!;
    private CombatSystem     _combat   = null!;
    private PhysicsEngine    _physics  = null!;

    private int _nextLevelTimer;

    private static readonly string[] LevelNames =
        ["LEVEL0","LEVEL1","LEVEL2","LEVEL3","LEVEL4","LEVEL5","LEVEL6","LEVEL7",
         "LEVEL8","LEVEL9","LEVEL10","LEVEL11","LEVEL12","LEVEL13","LEVEL14"];

    public GameLoop(string levelsDir)
    {
        _levelsDir      = levelsDir;
        _state          = new GameState { Level = new Level() };
        _inputState     = new InputState();
        _raylibInput    = new RaylibInput();
        _raylibRenderer = new RaylibRenderer();
        _combat         = new CombatSystem();
    }

    // ─────────────────────────────────────────────────────────────────────────

    public void Run()
    {
        Raylib.InitWindow(640, 480, "Prince of Persia");
        Raylib.SetTargetFPS(10);   // 10 ticks/sec = original 6502 game speed

        SpriteManager.Init(_levelsDir);

        var virtualTarget = Raylib.LoadRenderTexture(320, 240);
        Raylib.SetTextureFilter(virtualTarget.Texture, TextureFilter.Point);

        while (!Raylib.WindowShouldClose() && _state.Phase != GamePhase.Quit)
        {
            _raylibInput.Poll(_inputState);

            switch (_state.Phase)
            {
                case GamePhase.Title:
                    if (_inputState.AnyFresh) StartPlaying();
                    break;

                case GamePhase.Playing:
                    Tick();
                    break;

                case GamePhase.Paused:
                    if (_inputState.AnyFresh) _state.Phase = GamePhase.Playing;
                    break;

                case GamePhase.Dead:
                    if (_inputState.AnyFresh)
                    {
                        LoadLevel(_state.LevelIndex);
                        _state.Phase = GamePhase.Playing;
                    }
                    break;

                case GamePhase.NextLevel:
                    _nextLevelTimer++;
                    if (_nextLevelTimer > 15)
                    {
                        _nextLevelTimer = 0;
                        _state.LevelIndex++;
                        LoadLevel(_state.LevelIndex);
                        _state.Phase = GamePhase.Playing;
                    }
                    break;

                case GamePhase.YouWin:
                    if (_inputState.AnyFresh) _state.Phase = GamePhase.Quit;
                    break;
            }

            // ── Render virtual canvas → scale to window ───────────────────
            Raylib.BeginTextureMode(virtualTarget);
            _raylibRenderer.Draw(_state);
            Raylib.EndTextureMode();

            Raylib.BeginDrawing();
            Raylib.ClearBackground(EgaPalette.Black);
            Raylib.DrawTexturePro(
                virtualTarget.Texture,
                // negative height flips Y (render texture is stored bottom-up)
                new Rectangle { X = 0, Y = 0,
                                Width  =  virtualTarget.Texture.Width,
                                Height = -virtualTarget.Texture.Height },
                new Rectangle { X = 0, Y = 0, Width = 640, Height = 480 },
                Vector2.Zero, 0f, EgaPalette.White);
            Raylib.EndDrawing();
        }

        Raylib.UnloadRenderTexture(virtualTarget);
        SpriteManager.Unload();
        Raylib.CloseWindow();
    }

    // ─────────────────────────────────────────────────────────────────────────

    private void StartPlaying()
    {
        _state.LevelIndex = 1;
        LoadLevel(_state.LevelIndex);
        _state.Phase = GamePhase.Playing;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Main tick — game logic only, no rendering
    // ─────────────────────────────────────────────────────────────────────────

    private void Tick()
    {
        var inp = _inputState;

        if (inp.Pause)
        {
            _state.Phase = GamePhase.Paused;
            return;
        }

        _state.FrameCount++;
        _state.TimerFrames++;
        _state.TickStatus();

        // Time limit
        if (_state.TimerFrames >= Constants.TimeLimitMinutes * Constants.FramesPerMinute)
        {
            _state.SetStatus("Time's up!", 30);
            _state.Player.TakeDamage(_state.Player.Life);
        }

        _hazards.Update(_state);
        _player.Update(_state, inp);
        _guard.Update(_state);
        _combat.Check(_state, inp);

        if (!_state.Player.IsAlive)
            _state.Phase = GamePhase.Dead;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Level loader
    // ─────────────────────────────────────────────────────────────────────────

    private void LoadLevel(int idx)
    {
        string name = LevelNames[Math.Clamp(idx, 0, LevelNames.Length - 1)];
        string path = Path.Combine(_levelsDir, name);

        Level lv;
        try
        {
            lv = Level.Load(path);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to load {name}: {ex.Message}");
            _state.Phase = GamePhase.Quit;
            return;
        }

        _state.Level     = lv;
        _state.LevelName = name;
        _state.FrameCount    = 0;
        _state.TimerFrames   = 0;
        _state.ExitOpen      = false;
        _state.StatusMessage = "";
        _state.StatusTimer   = 0;

        // Rebuild subsystems for the new level
        var links  = new LinkTable(lv);
        _hazards   = new HazardSystem(lv, links);
        _physics   = new PhysicsEngine(lv, _hazards);
        _player    = new PlayerController(_physics, _hazards, lv);
        _guard     = new GuardController(_physics);
        _combat    = new CombatSystem();

        // Place player
        var kid = _state.Player;
        kid.Screen  = lv.KidStartScrn > 0 ? lv.KidStartScrn : 1;
        kid.BlockX  = lv.KidStartBlock % Level.Cols;
        kid.BlockY  = lv.KidStartBlock / Level.Cols;
        kid.FaceDir = lv.KidStartFace == 0xFF ? -1 : 1;
        kid.Action  = PlayerAction.OnGround;
        kid.OnFloor = true;
        kid.YVel    = 0;
        kid.StunFrames  = 0;
        kid.GraceFrames = 0;
        kid.InFight = false;

        // Reset HP on level 1 only; carry HP across later levels
        if (idx == 1)
        {
            kid.Life     = 3;
            kid.MaxLife  = 3;
            kid.HasSword = false;
        }

        // Place guards
        var guards = new List<CharState>();
        for (int i = 0; i < Level.NumScreens; i++)
        {
            byte blk = lv.GdStartBlock[i];
            if (blk == 0xFF || blk >= Level.CellsPerScreen) continue;

            int prog = i < lv.GdStartProg.Length ? lv.GdStartProg[i] : 0;
            int str  = idx < Constants.BasicStrength.Length ? Constants.BasicStrength[idx] : 3;

            guards.Add(new CharState
            {
                GuardIndex = i,
                Screen     = i + 1,
                BlockX     = blk % Level.Cols,
                BlockY     = blk / Level.Cols,
                FaceDir    = lv.GdStartFace[i] == 0xFF ? -1 : 1,
                Life       = str,
                MaxLife    = str,
                GuardProg  = Math.Clamp(prog, 0, 11),
                Action     = PlayerAction.OnGround,
                OnFloor    = true,
            });
        }
        _state.Guards = guards.ToArray();
    }
}
