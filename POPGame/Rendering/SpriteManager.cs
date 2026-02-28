namespace POPGame.Rendering;

/// <summary>
/// Initializes and manages all sprite atlases (tile + character textures).
/// Call Init() after Raylib.InitWindow() and Unload() before Raylib.CloseWindow().
/// </summary>
public static class SpriteManager
{
    private static string _imagesDir = "";
    public static bool IsLoaded { get; private set; }

    /// <summary>
    /// Locate the Images directory relative to the levels directory.
    /// Levels dir is like .../01 POP Source/Levels, Images is at .../01 POP Source/Images.
    /// </summary>
    public static void Init(string levelsDir)
    {
        // Images dir is a sibling of Levels dir
        var parent = Directory.GetParent(levelsDir);
        if (parent == null) return;

        _imagesDir = Path.Combine(parent.FullName, "Images");
        if (!Directory.Exists(_imagesDir))
            return;

        TileAtlas.Load(_imagesDir);
        CharAtlas.Load(_imagesDir);
        IsLoaded = TileAtlas.IsLoaded || CharAtlas.IsLoaded;
    }

    public static void Unload()
    {
        TileAtlas.Unload();
        CharAtlas.Unload();
        IsLoaded = false;
    }
}
