using Raylib_cs;
using POPGame.Characters;
using POPGame.Data;

namespace POPGame.Rendering;

/// <summary>
/// Loads character sprite frames from CHTAB binary files and maps
/// PlayerAction/GuardState to appropriate sprite images.
/// Frame data sourced from FRAMEDEF.S.
/// </summary>
public static class CharAtlas
{
    // Decoded sprite lists (1-indexed to match CHTAB image numbering)
    private static List<Apple2Image.DecodedSprite> _kidSprites = null!;
    private static List<Apple2Image.DecodedSprite> _guardSprites = null!;

    // Textures: one per CHTAB image index
    private static Texture2D[] _kidTextures = Array.Empty<Texture2D>();
    private static Texture2D[] _guardTextures = Array.Empty<Texture2D>();

    private static bool _loaded;
    public static bool IsLoaded => _loaded;

    // Key frame image indices from FRAMEDEF.S (Fimage field)
    // Kid frames (from CHTAB1):
    //   Stand: image $0F (15)
    //   Run cycle: images $01-$0E (1-14)
    //   Stand-jump: images $10-$21 (16-33)
    //   Run-jump: images $22-$2C (34-44)
    //   Turn: images $2D-$34 (45-52)
    //   Climb: images $0D-$1B with $80 flag (but in CHTAB1 these are 13-27)
    //   En-garde: image $8B-$8F from altset, but for kid we use same table
    //   Combat frames: various

    // Mapping of PlayerAction to representative CHTAB1 image index
    private static readonly Dictionary<PlayerAction, int> KidFrameMap = new()
    {
        { PlayerAction.OnGround,  0x0F }, // stand
        { PlayerAction.Running,   0x03 }, // mid-run
        { PlayerAction.Turning,   0x2D }, // turn start
        { PlayerAction.Jumping,   0x15 }, // mid-jump
        { PlayerAction.FreeFall,  0x32 }, // falling
        { PlayerAction.Hanging,   0x1F }, // hang
        { PlayerAction.ClimbUp,   0x11 }, // climb
        { PlayerAction.ClimbDown, 0x11 }, // climb (same visual)
        { PlayerAction.Crouching, 0x0C }, // crouch/landing
        { PlayerAction.EnGarde,   0x0F }, // stand (same for now)
        { PlayerAction.Strike,    0x0F }, // stand with sword
        { PlayerAction.Blocking,  0x0F }, // stand blocking
        { PlayerAction.Dead,      0x01 }, // collapsed
    };

    // Guard frames (from CHTAB2 or CHTAB4.GD)
    // ALTSET1 maps: guard images are $01-$20 in chtable4
    // Alert stand: image $10 (16)
    // Dead: image $20 (32)
    private static readonly Dictionary<GuardState, int> GuardFrameMap = new()
    {
        { GuardState.Patrol,   0x10 }, // alert stand
        { GuardState.Alert,    0x10 }, // alert stand
        { GuardState.EnGarde,  0x01 }, // en garde
        { GuardState.Strike,   0x0E }, // strike
        { GuardState.Blocking, 0x03 }, // block
        { GuardState.Stunned,  0x0B }, // missed block
        { GuardState.Dead,     0x20 }, // dead
    };

    public static void Load(string imagesDir)
    {
        string chtab1Path = Path.Combine(imagesDir, "IMG.CHTAB1");
        string chtab4Path = Path.Combine(imagesDir, "IMG.CHTAB4.GD");
        // Fallback to CHTAB2 if CHTAB4.GD missing
        string guardPath = File.Exists(chtab4Path) ? chtab4Path :
            Path.Combine(imagesDir, "IMG.CHTAB2");

        if (!File.Exists(chtab1Path) || !File.Exists(guardPath))
            return;

        _kidSprites = Apple2Image.LoadTable(chtab1Path, 0x6000);
        // CHTAB4.GD loads at $9600, CHTAB2 loads at $8400
        ushort guardLoadAddr = File.Exists(chtab4Path) ? (ushort)0x9600 : (ushort)0x8400;
        _guardSprites = Apple2Image.LoadTable(guardPath, guardLoadAddr);

        // Build textures
        _kidTextures = new Texture2D[_kidSprites.Count];
        for (int i = 1; i < _kidSprites.Count; i++)
        {
            var s = _kidSprites[i];
            if (s.Pixels != null)
                _kidTextures[i] = Apple2Image.ToTexture(s, transparentBlack: true);
        }

        _guardTextures = new Texture2D[_guardSprites.Count];
        for (int i = 1; i < _guardSprites.Count; i++)
        {
            var s = _guardSprites[i];
            if (s.Pixels != null)
                _guardTextures[i] = Apple2Image.ToTexture(s, transparentBlack: true);
        }

        _loaded = true;
    }

    /// <summary>
    /// Get the texture for a kid pose based on PlayerAction.
    /// </summary>
    public static Texture2D GetKidTexture(PlayerAction action)
    {
        if (!_loaded) return default;
        if (!KidFrameMap.TryGetValue(action, out int imgIdx)) return default;
        if (imgIdx <= 0 || imgIdx >= _kidTextures.Length) return default;
        return _kidTextures[imgIdx];
    }

    /// <summary>
    /// Get the texture for a guard pose based on GuardState.
    /// </summary>
    public static Texture2D GetGuardTexture(GuardState action)
    {
        if (!_loaded) return default;
        if (!GuardFrameMap.TryGetValue(action, out int imgIdx)) return default;
        if (imgIdx <= 0 || imgIdx >= _guardTextures.Length) return default;
        return _guardTextures[imgIdx];
    }

    /// <summary>
    /// Get raw sprite dimensions for a kid frame.
    /// </summary>
    public static (int w, int h) GetKidSize(PlayerAction action)
    {
        if (!_loaded || !KidFrameMap.TryGetValue(action, out int imgIdx))
            return (0, 0);
        if (imgIdx <= 0 || imgIdx >= _kidSprites.Count) return (0, 0);
        var s = _kidSprites[imgIdx];
        return (s.WidthPx, s.Height);
    }

    /// <summary>
    /// Get raw sprite dimensions for a guard frame.
    /// </summary>
    public static (int w, int h) GetGuardSize(GuardState action)
    {
        if (!_loaded || !GuardFrameMap.TryGetValue(action, out int imgIdx))
            return (0, 0);
        if (imgIdx <= 0 || imgIdx >= _guardSprites.Count) return (0, 0);
        var s = _guardSprites[imgIdx];
        return (s.WidthPx, s.Height);
    }

    public static void Unload()
    {
        if (!_loaded) return;
        for (int i = 1; i < _kidTextures.Length; i++)
            if (_kidTextures[i].Id != 0) Raylib.UnloadTexture(_kidTextures[i]);
        for (int i = 1; i < _guardTextures.Length; i++)
            if (_guardTextures[i].Id != 0) Raylib.UnloadTexture(_guardTextures[i]);
        _kidTextures = Array.Empty<Texture2D>();
        _guardTextures = Array.Empty<Texture2D>();
        _loaded = false;
    }
}
