// Prince of Persia Apple II - Level Viewer (C# Console)
// Reads the original binary level files and renders them as ASCII art.
//
// Level file layout (2304 bytes = 0x900):
//   0x000 - 0x2CF  (720 bytes)  BLUETYPE  : tile type  for 24 screens × 30 cells
//   0x2D0 - 0x59F  (720 bytes)  BLUESPEC  : tile state for 24 screens × 30 cells
//   0x5A0 - 0x69F  (256 bytes)  LINKLOC   : trigger→linked-cell lookup table
//   0x6A0 - 0x79F  (256 bytes)  LINKMAP   : linked screen for each LINKLOC entry
//   0x7A0 - 0x7FF  ( 96 bytes)  MAP       : 24 screens × 4 adjacent-screen numbers
//                                           [left, right, above, below]  (1-indexed; 0 = none)
//   0x800 - 0x8FF  (256 bytes)  INFO      : player / guard starting data
//
// Screen cells: each screen has 30 cells = 3 rows × 10 columns
//   cell_index = row * 10 + col   (row 0 = top, row 2 = bottom)
//
// Tile ID is the lower 5 bits of the BLUETYPE byte (upper bits are editor flags).

using System;
using System.IO;

// ─────────────────────────────────────────────────────────────────────────────
// Tile definitions
// ─────────────────────────────────────────────────────────────────────────────

enum TileId : byte
{
    Space        = 0,
    Floor        = 1,
    Spikes       = 2,
    Posts        = 3,
    Gate         = 4,
    DPressPlate  = 5,   // pressure plate down
    PressPlate   = 6,   // pressure plate up
    PanelWF      = 7,   // panel with floor
    PillarBot    = 8,
    PillarTop    = 9,
    Flask        = 10,
    Loose        = 11,
    PanelWOF     = 12,  // panel without floor
    Mirror       = 13,
    Rubble       = 14,
    UPressPlate  = 15,  // pressure plate (up variant)
    Exit         = 16,
    Exit2        = 17,
    Slicer       = 18,
    Torch        = 19,
    Block        = 20,
    Bones        = 21,
    Sword        = 22,
    Window       = 23,
    Window2      = 24,
    ArchBot      = 25,
    ArchTop1     = 26,
    ArchTop2     = 27,
    ArchTop3     = 28,
    ArchTop4     = 29,
}

// ─────────────────────────────────────────────────────────────────────────────
// Level data model
// ─────────────────────────────────────────────────────────────────────────────

class Level
{
    public const int NumScreens      = 24;
    public const int CellsPerScreen  = 30;   // 3 rows × 10 cols
    public const int Rows            = 3;
    public const int Cols            = 10;

    public byte[] BlueType = new byte[NumScreens * CellsPerScreen];
    public byte[] BlueSpec = new byte[NumScreens * CellsPerScreen];
    public byte[] LinkLoc  = new byte[256];
    public byte[] LinkMap  = new byte[256];
    // MAP[screen0, dir]: dir 0=left 1=right 2=above 3=below; values 1-indexed; 0=none
    public byte[,] Map     = new byte[NumScreens, 4];
    public byte[] InfoRaw  = new byte[256];

    // Parsed INFO fields
    public int InfoScreenCount;
    public int KidStartScrn;        // 1-indexed; 0 = none
    public int KidStartBlock;       // 0-29
    public int KidStartFace;        // 0xFF = facing left
    public int SwordStartScrn;
    public int SwordStartBlock;
    public byte[] GdStartBlock = new byte[NumScreens];
    public byte[] GdStartFace  = new byte[NumScreens];
    public byte[] GdStartX     = new byte[NumScreens];
    public byte[] GdStartSeqL  = new byte[NumScreens];
    public byte[] GdStartProg  = new byte[NumScreens];
    public byte[] GdStartSeqH  = new byte[NumScreens];

    // 0-indexed screen helpers
    public TileId GetTileId(int screen0, int row, int col)
        => (TileId)(BlueType[screen0 * CellsPerScreen + row * Cols + col] & 0x1F);

    public byte GetSpec(int screen0, int row, int col)
        => BlueSpec[screen0 * CellsPerScreen + row * Cols + col];

    // Neighbours (1-indexed input/output; 0 = none)
    public int Left (int scrn1) => scrn1 >= 1 && scrn1 <= NumScreens ? Map[scrn1 - 1, 0] : 0;
    public int Right(int scrn1) => scrn1 >= 1 && scrn1 <= NumScreens ? Map[scrn1 - 1, 1] : 0;
    public int Above(int scrn1) => scrn1 >= 1 && scrn1 <= NumScreens ? Map[scrn1 - 1, 2] : 0;
    public int Below(int scrn1) => scrn1 >= 1 && scrn1 <= NumScreens ? Map[scrn1 - 1, 3] : 0;

    public static Level Load(string path)
    {
        byte[] data = File.ReadAllBytes(path);
        if (data.Length < 2304)
            throw new IOException($"File too short: {data.Length} (need 2304)");

        var lv = new Level();
        Buffer.BlockCopy(data,    0, lv.BlueType, 0, 720);
        Buffer.BlockCopy(data,  720, lv.BlueSpec, 0, 720);
        Buffer.BlockCopy(data, 1440, lv.LinkLoc,  0, 256);
        Buffer.BlockCopy(data, 1696, lv.LinkMap,  0, 256);

        // MAP at offset 0x7A0 = 1952
        for (int s = 0; s < NumScreens; s++)
        {
            int off = 1952 + s * 4;
            lv.Map[s, 0] = data[off];     // left
            lv.Map[s, 1] = data[off + 1]; // right
            lv.Map[s, 2] = data[off + 2]; // above
            lv.Map[s, 3] = data[off + 3]; // below
        }

        // INFO at offset 0x800 = 2048
        Buffer.BlockCopy(data, 2048, lv.InfoRaw, 0, 256);
        lv.InfoScreenCount = lv.InfoRaw[0];
        lv.KidStartScrn    = lv.InfoRaw[64];
        lv.KidStartBlock   = lv.InfoRaw[65];
        lv.KidStartFace    = lv.InfoRaw[66];
        lv.SwordStartScrn  = lv.InfoRaw[68];
        lv.SwordStartBlock = lv.InfoRaw[69];

        const int gdBase = 71;
        for (int i = 0; i < NumScreens; i++)
        {
            lv.GdStartBlock[i] = lv.InfoRaw[gdBase +   0 + i];
            lv.GdStartFace [i] = lv.InfoRaw[gdBase +  24 + i];
            lv.GdStartX    [i] = lv.InfoRaw[gdBase +  48 + i];
            lv.GdStartSeqL [i] = lv.InfoRaw[gdBase +  72 + i];
            lv.GdStartProg [i] = lv.InfoRaw[gdBase +  96 + i];
            lv.GdStartSeqH [i] = lv.InfoRaw[gdBase + 120 + i];
        }
        return lv;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Renderer
// ─────────────────────────────────────────────────────────────────────────────

static class Renderer
{
    record TileVis(string Glyph, ConsoleColor Fg, ConsoleColor Bg = ConsoleColor.Black);

    static readonly TileVis[] Visuals = new TileVis[]
    {
        new("   ", ConsoleColor.DarkGray),               //  0  space
        new("═══", ConsoleColor.Gray),                    //  1  floor
        new("^^^", ConsoleColor.Red),                     //  2  spikes
        new(" ║ ", ConsoleColor.DarkYellow),              //  3  posts
        new(" G ", ConsoleColor.Cyan),                    //  4  gate
        new(" v ", ConsoleColor.Magenta),                 //  5  dpressplate
        new(" u ", ConsoleColor.Magenta),                 //  6  pressplate
        new("═╪═", ConsoleColor.Gray),                    //  7  panel w/floor
        new("(B)", ConsoleColor.Yellow),                  //  8  pillar bottom
        new("(T)", ConsoleColor.Yellow),                  //  9  pillar top
        new("[f]", ConsoleColor.Green),                   // 10  flask
        new("-L-", ConsoleColor.Yellow),                  // 11  loose
        new("═╡═", ConsoleColor.Gray),                    // 12  panel w/o floor
        new("[M]", ConsoleColor.Cyan),                    // 13  mirror
        new(" r ", ConsoleColor.DarkGray),                // 14  rubble
        new(" U ", ConsoleColor.Magenta),                 // 15  upressplate
        new("[E]", ConsoleColor.Green),                   // 16  exit
        new("[e]", ConsoleColor.DarkGreen),               // 17  exit2
        new(" / ", ConsoleColor.Red),                     // 18  slicer
        new("[t]", ConsoleColor.DarkYellow),              // 19  torch
        new("███", ConsoleColor.DarkBlue, ConsoleColor.DarkBlue),  // 20  block
        new(" b ", ConsoleColor.DarkGray),                // 21  bones
        new(" S ", ConsoleColor.Yellow),                  // 22  sword
        new("[W]", ConsoleColor.DarkCyan),                // 23  window
        new("[w]", ConsoleColor.DarkCyan),                // 24  window2
        new(" A ", ConsoleColor.DarkYellow),              // 25  archbot
        new(" a ", ConsoleColor.DarkYellow),              // 26  archtop1
        new(" a ", ConsoleColor.DarkYellow),              // 27  archtop2
        new(" a ", ConsoleColor.DarkYellow),              // 28  archtop3
        new(" a ", ConsoleColor.DarkYellow),              // 29  archtop4
    };

    static TileVis GetVis(TileId id)
    {
        int i = (int)id;
        return i >= 0 && i < Visuals.Length ? Visuals[i] : new("?? ", ConsoleColor.White);
    }

    static (int row, int col) BlockIndex(int idx)
        => (Math.Min(idx / Level.Cols, Level.Rows - 1),
            Math.Min(idx % Level.Cols, Level.Cols - 1));

    public static void DrawScreen(Level lv, int scrn1, string levelName)
    {
        int s0 = scrn1 - 1; // 0-indexed

        // Overlays
        int kidRow = -1, kidCol = -1;
        if (lv.KidStartScrn == scrn1)
            (kidRow, kidCol) = BlockIndex(lv.KidStartBlock);

        int swdRow = -1, swdCol = -1;
        if (lv.SwordStartScrn == scrn1 && lv.SwordStartScrn != 0)
            (swdRow, swdCol) = BlockIndex(lv.SwordStartBlock);

        int gdRow = -1, gdCol = -1;
        byte gdBlk = lv.GdStartBlock[s0];
        bool hasGuard = gdBlk != 0xFF && gdBlk < Level.CellsPerScreen;
        if (hasGuard)
            (gdRow, gdCol) = BlockIndex(gdBlk);

        try { Console.Clear(); } catch { }

        // Header
        Cw(ConsoleColor.White, ConsoleColor.DarkBlue,
           $"  Prince of Persia (Apple II)  |  {levelName}  |  Screen {scrn1}/{lv.InfoScreenCount - 1}  ");
        Console.ResetColor();
        Console.WriteLine();

        // Top border
        DrawHLine();

        // Tile rows (row 0 = top of dungeon)
        for (int row = 0; row < Level.Rows; row++)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("│");

            for (int col = 0; col < Level.Cols; col++)
            {
                TileId tile = lv.GetTileId(s0, row, col);
                byte   spec = lv.GetSpec  (s0, row, col);

                if      (row == kidRow && col == kidCol)
                    Cw(ConsoleColor.White,  ConsoleColor.DarkBlue,  " P ");
                else if (row == gdRow  && col == gdCol && hasGuard)
                    Cw(ConsoleColor.White,  ConsoleColor.DarkRed,   " G ");
                else if (row == swdRow && col == swdCol)
                    Cw(ConsoleColor.Yellow, ConsoleColor.DarkGray,  " S ");
                else
                {
                    var v = GetVis(tile);
                    ConsoleColor fg = v.Fg;
                    // open gate = green, closed = cyan
                    if (tile == TileId.Gate)
                        fg = spec < 188 ? ConsoleColor.DarkGreen : ConsoleColor.Cyan;

                    Console.ForegroundColor = fg;
                    Console.BackgroundColor = v.Bg;
                    Console.Write(v.Glyph);
                    Console.ResetColor();
                }
            }

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("│");
            Console.ResetColor();

            // Row label
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  row {row}");
            Console.ResetColor();
        }

        DrawHLine();

        // Neighbours
        int L = lv.Left(scrn1), R = lv.Right(scrn1);
        int U = lv.Above(scrn1), D = lv.Below(scrn1);

        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.Write("\n  Neighbours  ");
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write($" ← {FmtScrn(L)}   → {FmtScrn(R)}   ↑ {FmtScrn(U)}   ↓ {FmtScrn(D)}");
        Console.ResetColor();
        Console.WriteLine();

        // Start info
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.Write("  Start info  ");
        Console.ForegroundColor = ConsoleColor.White;

        if (lv.KidStartScrn == scrn1)
        {
            string face = lv.KidStartFace == 0xFF ? "←" : "→";
            Console.Write($" [P]Kid row={kidRow} col={kidCol} face={face}");
        }
        if (lv.SwordStartScrn == scrn1 && lv.SwordStartScrn != 0)
            Console.Write($" [S]Sword row={swdRow} col={swdCol}");
        if (hasGuard)
        {
            string face = lv.GdStartFace[s0] == 0xFF ? "←" : "→";
            Console.Write($" [G]Guard row={gdRow} col={gdCol} face={face}");
        }

        Console.ResetColor();
        Console.WriteLine("\n");

        // Legend
        DrawLegend();

        // Controls
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine("  ← → ↑ ↓ navigate   L = load level   1-9 = jump to screen   Q = quit");
        Console.ResetColor();
    }

    static string FmtScrn(int s) => s > 0 ? s.ToString() : " -";

    static void DrawHLine()
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("┼");
        Console.Write(new string('─', Level.Cols * 3));
        Console.WriteLine("┼");
        Console.ResetColor();
    }

    static void Cw(ConsoleColor fg, ConsoleColor bg, string text)
    {
        Console.ForegroundColor = fg;
        Console.BackgroundColor = bg;
        Console.Write(text);
        Console.ResetColor();
    }

    static void DrawLegend()
    {
        var items = new (TileId id, string label)[]
        {
            (TileId.Space,       "space     "),
            (TileId.Floor,       "floor     "),
            (TileId.Block,       "block wall"),
            (TileId.Gate,        "gate      "),
            (TileId.Spikes,      "spikes    "),
            (TileId.Slicer,      "slicer    "),
            (TileId.Loose,       "loose flr "),
            (TileId.PressPlate,  "press plt↑"),
            (TileId.DPressPlate, "press plt↓"),
            (TileId.Flask,       "flask/pot "),
            (TileId.Exit,        "exit      "),
            (TileId.Torch,       "torch     "),
            (TileId.Mirror,      "mirror    "),
            (TileId.Posts,       "posts     "),
            (TileId.PillarBot,   "pillar bot"),
            (TileId.ArchBot,     "arch bot  "),
        };

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("  Legend: ");
        for (int i = 0; i < items.Length; i++)
        {
            if (i > 0 && i % 8 == 0) Console.Write("\n           ");
            var v = GetVis(items[i].id);
            Console.ForegroundColor = v.Fg;
            Console.BackgroundColor = v.Bg;
            Console.Write(v.Glyph);
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"={items[i].label} ");
        }
        Console.ResetColor();
        Console.WriteLine();
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Entry point
// ─────────────────────────────────────────────────────────────────────────────

static class Program
{
    // Resolve levels dir relative to the executable
    static string LevelsDir()
    {
        string exe = AppDomain.CurrentDomain.BaseDirectory;
        // Walk up until we find the "originalcode" sibling, or fall back to CWD
        DirectoryInfo? dir = new DirectoryInfo(exe);
        for (int i = 0; i < 8 && dir != null; i++)
        {
            string candidate = Path.Combine(dir.FullName,
                "originalcode",
                "Prince-of-Persia-Apple-II-master",
                "01 POP Source",
                "Levels");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        // Hard-coded fallback for this project
        return Path.Combine(
            "F:", "priyan", "Projects", "AI", "POPCS",
            "originalcode",
            "Prince-of-Persia-Apple-II-master",
            "01 POP Source",
            "Levels");
    }

    static readonly string[] LevelNames =
        ["LEVEL0","LEVEL1","LEVEL2","LEVEL3","LEVEL4","LEVEL5","LEVEL6","LEVEL7",
         "LEVEL8","LEVEL9","LEVEL10","LEVEL11","LEVEL12","LEVEL13","LEVEL14"];

    static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.Title = "Prince of Persia - Level Viewer";

        string levDir = LevelsDir();
        if (!Directory.Exists(levDir))
        {
            Console.WriteLine($"ERROR: Levels directory not found:\n  {levDir}");
            Console.WriteLine("Press any key...");
            Console.ReadKey(true);
            return;
        }

        int levelIdx = 1;
        int curScrn  = 1;
        Level? lv    = null;

        TryLoad(levDir, levelIdx, ref lv, ref curScrn);

        while (true)
        {
            if (lv is null)
            {
                Console.WriteLine("No level loaded. Press L to load or Q to quit.");
                var k2 = Console.ReadKey(true);
                if (k2.Key == ConsoleKey.Q || k2.Key == ConsoleKey.Escape) break;
                if (k2.Key == ConsoleKey.L) { levelIdx = PickLevel(levDir, levelIdx); TryLoad(levDir, levelIdx, ref lv, ref curScrn); }
                continue;
            }

            Renderer.DrawScreen(lv, curScrn, LevelNames[levelIdx]);

            var key = Console.ReadKey(true);
            switch (key.Key)
            {
                case ConsoleKey.LeftArrow:  { int n = lv.Left(curScrn);  if (n > 0) curScrn = n; break; }
                case ConsoleKey.RightArrow: { int n = lv.Right(curScrn); if (n > 0) curScrn = n; break; }
                case ConsoleKey.UpArrow:    { int n = lv.Above(curScrn); if (n > 0) curScrn = n; break; }
                case ConsoleKey.DownArrow:  { int n = lv.Below(curScrn); if (n > 0) curScrn = n; break; }

                case ConsoleKey.L:
                    levelIdx = PickLevel(levDir, levelIdx);
                    TryLoad(levDir, levelIdx, ref lv, ref curScrn);
                    break;

                case ConsoleKey.Q:
                case ConsoleKey.Escape:
                    try { Console.Clear(); } catch { }
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine("  Farewell, Prince!");
                    Console.ResetColor();
                    return;

                default:
                    // Digit 1-9: jump to screen
                    if (key.KeyChar >= '1' && key.KeyChar <= '9')
                    {
                        int n = key.KeyChar - '0';
                        if (n >= 1 && n <= Level.NumScreens) curScrn = n;
                    }
                    break;
            }
        }
    }

    static void TryLoad(string levDir, int idx, ref Level? lv, ref int curScrn)
    {
        string path = Path.Combine(levDir, LevelNames[idx]);
        try
        {
            lv = Level.Load(path);
            curScrn = lv.KidStartScrn > 0 ? lv.KidStartScrn : 1;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Failed to load {LevelNames[idx]}: {ex.Message}");
            Console.ResetColor();
            lv = null;
        }
    }

    static int PickLevel(string levDir, int current)
    {
        try { Console.Clear(); } catch { }
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("  Select level (0-14)\n");
        for (int i = 0; i < LevelNames.Length; i++)
        {
            bool exists = File.Exists(Path.Combine(levDir, LevelNames[i]));
            Console.ForegroundColor = exists ? ConsoleColor.Cyan : ConsoleColor.DarkGray;
            string mark = i == current ? " <" : "  ";
            Console.Write($"  [{i,2}] {LevelNames[i],-9}{mark}");
            if ((i + 1) % 5 == 0) Console.WriteLine();
        }
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write("  Enter number: ");
        Console.ResetColor();
        string? inp = Console.ReadLine();
        if (int.TryParse(inp, out int n) && n >= 0 && n < LevelNames.Length &&
            File.Exists(Path.Combine(levDir, LevelNames[n])))
            return n;
        return current;
    }
}
