namespace POPGame.Sim;

/// <summary>
/// The original coordinate system, from TABLES.S.
///
/// X is measured in "units": 14 per block, and block 0's left edge sits at
/// <see cref="ScrnLeft"/> = 58, so a room spans x 58..198. Y is measured in
/// pixels with 63 per block row and the room bottom at <see cref="ScrnBot"/> = 191.
///
/// A standing character's Y is the *centre plane* of its row: VertDist above the
/// bottom of the block, which is what gives POP its slight top-down perspective.
/// </summary>
public static class Coord
{
    public const int ScrnLeft = 58;
    public const int ScrnBot = 191;
    public const int ScrnWidth = 140;       // 10 blocks
    public const int BlockWidth = 14;
    public const int BlockHeight = 63;
    public const int VertDist = 10;         // block bottom -> centre plane
    public const int DHeight = 3;           // floorpiece thickness

    public const int Cols = 10;
    public const int Rows = 3;

    /// <summary>Left edge (x units) of block column <paramref name="col"/>; col may be off-screen.</summary>
    public static int BlockEdge(int col) => ScrnLeft + col * BlockWidth;

    /// <summary>Block column containing x. Negative / &gt;=10 means outside the room.</summary>
    public static int BlockX(int x) => FloorDiv(x - ScrnLeft, BlockWidth);

    /// <summary>Offset 0..13 of x within its block.</summary>
    public static int PixelX(int x) => Mod(x - ScrnLeft, BlockWidth);

    /// <summary>Y of the bottom of block row <paramref name="row"/> (row -1..3).</summary>
    public static int BlockBot(int row) => ScrnBot - (2 - row) * BlockHeight;

    /// <summary>Y of the top of block row <paramref name="row"/>.</summary>
    public static int BlockTop(int row) => ScrnBot + 1 - (3 - row) * BlockHeight;

    /// <summary>Y a character standing on the floor of row <paramref name="row"/> has.</summary>
    public static int FloorY(int row) => BlockBot(row) - VertDist;

    /// <summary>Block row whose centre plane contains y (the inverse of FloorY).</summary>
    public static int BlockY(int y) => FloorDiv(y + VertDist - (ScrnBot - 2 * BlockHeight), BlockHeight);

    private static int FloorDiv(int a, int b) => (a >= 0 ? a : a - (b - 1)) / b;
    private static int Mod(int a, int b) { int m = a % b; return m < 0 ? m + b : m; }
}
