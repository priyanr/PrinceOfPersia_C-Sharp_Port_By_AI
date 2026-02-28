using Raylib_cs;
using POPGame.Data;

namespace POPGame.Rendering;

/// <summary>
/// Draws each TileId as pixel-art in a 32×72 virtual-pixel cell.
/// When authentic Apple II textures are loaded (via TileAtlas), draws those;
/// otherwise falls back to colored rectangles.
/// </summary>
public static class TileSprites
{
    public const int TileW = 32;
    public const int TileH = 72;

    public static void Draw(TileId tile, bool gateOpen, bool exitOpen, int px, int py)
    {
        int tileIdx = (int)tile;

        // Try authentic Apple II texture first
        if (TileAtlas.IsLoaded && tileIdx >= 0 && tileIdx < 30)
        {
            var tex = TileAtlas.GetTileTexture(tileIdx);
            if (tex.Id != 0)
            {
                // Special handling for gates — show floor when open
                if (tile == TileId.Gate && gateOpen)
                {
                    var floorTex = TileAtlas.GetTileTexture((int)TileId.Floor);
                    if (floorTex.Id != 0)
                    {
                        DrawScaledTexture(floorTex, px, py);
                        return;
                    }
                }

                // Special handling for exit — tint green when open
                DrawScaledTexture(tex, px, py);

                // Draw foreground overlay if present
                var front = TileAtlas.GetFrontTexture(tileIdx);
                if (front.Id != 0)
                {
                    var (fx, fy) = TileAtlas.GetFrontOffset(tileIdx);
                    // fx, fy are already in Apple II canvas coordinates
                    float scaleX = (float)TileW / TileAtlas.AppleTileWidthPx;
                    float scaleY = (float)TileH / TileAtlas.AppleTileHeight;
                    float drawFx = px + fx * scaleX;
                    float drawFy = py + fy * scaleY;
                    float drawFw = front.Width * scaleX;
                    float drawFh = front.Height * scaleY;
                    Raylib.DrawTexturePro(
                        front,
                        new Rectangle(0, 0, front.Width, front.Height),
                        new Rectangle(drawFx, drawFy, drawFw, drawFh),
                        System.Numerics.Vector2.Zero, 0f, Color.White);
                }
                return;
            }
        }

        // Fallback to rectangle rendering
        switch (tile)
        {
            case TileId.Space:       DrawSpace(px, py);                       break;
            case TileId.Floor:       DrawFloor(px, py);                       break;
            case TileId.Spikes:      DrawSpikes(px, py);                      break;
            case TileId.Posts:       DrawPosts(px, py);                       break;
            case TileId.Gate:        if (gateOpen) DrawFloor(px, py);
                                     else DrawGateClosed(px, py);             break;
            case TileId.DPressPlate:
            case TileId.PressPlate:
            case TileId.UPressPlate: DrawPressPlate(px, py);                  break;
            case TileId.PanelWF:     DrawFloor(px, py);                       break;
            case TileId.PillarBot:   DrawPillarBot(px, py);                   break;
            case TileId.PillarTop:   DrawPillarTop(px, py);                   break;
            case TileId.Flask:       DrawFlask(px, py);                       break;
            case TileId.Loose:       DrawLoose(px, py);                       break;
            case TileId.PanelWOF:    DrawPanelWOF(px, py);                   break;
            case TileId.Mirror:      DrawMirror(px, py);                      break;
            case TileId.Rubble:      DrawRubble(px, py);                      break;
            case TileId.Exit:
            case TileId.Exit2:       DrawExit(px, py, exitOpen);              break;
            case TileId.Slicer:      DrawSlicer(px, py);                      break;
            case TileId.Torch:       DrawTorch(px, py);                       break;
            case TileId.Block:       DrawBlock(px, py);                       break;
            case TileId.Bones:       DrawBones(px, py);                       break;
            case TileId.Sword:       DrawSword(px, py);                       break;
            case TileId.Window:
            case TileId.Window2:     DrawWindow(px, py);                      break;
            case TileId.ArchBot:     DrawArchBot(px, py);                     break;
            case TileId.ArchTop1:
            case TileId.ArchTop2:
            case TileId.ArchTop3:
            case TileId.ArchTop4:    DrawArchTop(px, py);                     break;
            default:                 DrawSpace(px, py);                       break;
        }
    }

    /// <summary>
    /// Draw a texture scaled to fit the tile cell (32×72).
    /// </summary>
    private static void DrawScaledTexture(Texture2D tex, int px, int py)
    {
        Raylib.DrawTexturePro(
            tex,
            new Rectangle(0, 0, tex.Width, tex.Height),
            new Rectangle(px, py, TileW, TileH),
            System.Numerics.Vector2.Zero, 0f, Color.White);
    }

    // Shorthand: draw a rect at tile-local coords
    private static void R(int lx, int ly, int lw, int lh, Color c, int px, int py)
        => Raylib.DrawRectangle(px + lx, py + ly, lw, lh, c);

    // ─── Tile implementations (fallback) ──────────────────────────────────────

    private static void DrawSpace(int px, int py)
        => R(0, 0, TileW, TileH, EgaPalette.Black, px, py);

    private static void DrawFloor(int px, int py)
    {
        R(0, 0,  TileW, 64, EgaPalette.Black, px, py);
        R(0, 64, TileW,  8, EgaPalette.Gray,  px, py);
    }

    private static void DrawBlock(int px, int py)
    {
        R(0, 0, TileW, TileH, EgaPalette.DarkGray, px, py);
        R(0,  0, TileW, 1, EgaPalette.Black, px, py);
        R(0, 18, TileW, 1, EgaPalette.Black, px, py);
        R(0, 36, TileW, 1, EgaPalette.Black, px, py);
        R(0, 54, TileW, 1, EgaPalette.Black, px, py);
        R(0, 71, TileW, 1, EgaPalette.Black, px, py);
        R(16, 0,  1, 18, EgaPalette.Black, px, py);
        R( 8, 18, 1, 18, EgaPalette.Black, px, py);
        R(24, 18, 1, 18, EgaPalette.Black, px, py);
        R(16, 36, 1, 18, EgaPalette.Black, px, py);
        R( 8, 54, 1, 18, EgaPalette.Black, px, py);
        R(24, 54, 1, 18, EgaPalette.Black, px, py);
    }

    private static void DrawGateClosed(int px, int py)
    {
        R(0, 0, TileW, TileH, EgaPalette.Black, px, py);
        R( 4, 0, 4, TileH, EgaPalette.DarkGray, px, py);
        R(12, 0, 4, TileH, EgaPalette.DarkGray, px, py);
        R(20, 0, 4, TileH, EgaPalette.DarkGray, px, py);
        R(28, 0, 4, TileH, EgaPalette.DarkGray, px, py);
    }

    private static void DrawSpikes(int px, int py)
    {
        R(0, 0,  TileW, TileH, EgaPalette.Black, px, py);
        R(0, 64, TileW,  8,    EgaPalette.Gray,  px, py);
        foreach (int cx in new[] { 4, 12, 20, 28 })
        {
            R(cx - 1, 50, 2, 4, EgaPalette.DarkRed, px, py);
            R(cx - 2, 54, 4, 4, EgaPalette.DarkRed, px, py);
            R(cx - 3, 58, 6, 6, EgaPalette.DarkRed, px, py);
        }
    }

    private static void DrawLoose(int px, int py)
    {
        R(0, 0, TileW, TileH, EgaPalette.Black, px, py);
        R(0, 64, TileW, 4, EgaPalette.Yellow, px, py);
        for (int x = 0; x < TileW; x += 8)
            R(x + 4, 64, 4, 4, EgaPalette.Black, px, py);
    }

    private static void DrawPosts(int px, int py)
    {
        DrawFloor(px, py);
        R( 5, 16, 4, 48, EgaPalette.Yellow, px, py);
        R(23, 16, 4, 48, EgaPalette.Yellow, px, py);
    }

    private static void DrawPressPlate(int px, int py)
    {
        DrawFloor(px, py);
        R( 4, 60, 6, 4, EgaPalette.Magenta, px, py);
        R(22, 60, 6, 4, EgaPalette.Magenta, px, py);
    }

    private static void DrawFlask(int px, int py)
    {
        DrawFloor(px, py);
        R(13, 44,  8, 12, EgaPalette.Green, px, py);
        R(15, 46,  4,  8, EgaPalette.Cyan,  px, py);
    }

    private static void DrawPillarBot(int px, int py)
    {
        DrawFloor(px, py);
        R(12, 0, 8, 64, EgaPalette.Yellow, px, py);
    }

    private static void DrawPillarTop(int px, int py)
    {
        DrawFloor(px, py);
        R( 8, 0, 16, 6, EgaPalette.Yellow, px, py);
        R(12, 6,  8, 26, EgaPalette.Yellow, px, py);
    }

    private static void DrawExit(int px, int py, bool open)
    {
        R(0, 0, TileW, TileH, EgaPalette.Black, px, py);
        Color archColor = open ? EgaPalette.Green : EgaPalette.DarkGray;
        R( 0, 0, TileW,  6, archColor, px, py);
        R( 0, 6,  4,   60, archColor, px, py);
        R(28, 6,  4,   60, archColor, px, py);
    }

    private static void DrawTorch(int px, int py)
    {
        DrawFloor(px, py);
        R(15, 36, 2, 24, EgaPalette.DarkGray, px, py);
        R(13, 28, 6,  8, EgaPalette.Yellow,   px, py);
        R(14, 24, 4,  4, EgaPalette.Yellow,   px, py);
    }

    private static void DrawMirror(int px, int py)
    {
        R(0, 0, TileW, TileH, EgaPalette.Cyan,    px, py);
        R(4, 4, TileW - 8, TileH - 8, EgaPalette.DarkCyan, px, py);
    }

    private static void DrawRubble(int px, int py)
    {
        DrawFloor(px, py);
        R( 2, 56, 6, 6, EgaPalette.DarkGray, px, py);
        R(10, 58, 8, 4, EgaPalette.DarkGray, px, py);
        R(20, 54, 5, 8, EgaPalette.DarkGray, px, py);
        R(26, 57, 4, 5, EgaPalette.DarkGray, px, py);
    }

    private static void DrawBones(int px, int py)
    {
        DrawFloor(px, py);
        R( 4, 60, 24, 2, EgaPalette.DarkGray, px, py);
        R( 8, 57,  4, 4, EgaPalette.DarkGray, px, py);
        R(18, 57,  4, 4, EgaPalette.DarkGray, px, py);
    }

    private static void DrawSword(int px, int py)
    {
        DrawFloor(px, py);
        R( 6, 43, 20, 2, EgaPalette.Yellow,   px, py);
        R(20, 41,  2, 6, EgaPalette.Yellow,   px, py);
        R(22, 43,  4, 2, EgaPalette.DarkGray, px, py);
    }

    private static void DrawWindow(int px, int py)
    {
        DrawFloor(px, py);
        for (int y = 0; y < 56; y += 8)
            R(0, y, TileW, 2, EgaPalette.DarkCyan, px, py);
        for (int x = 0; x < TileW; x += 8)
            R(x, 0, 2, 56, EgaPalette.DarkCyan, px, py);
    }

    private static void DrawPanelWOF(int px, int py)
    {
        R(0, 0, TileW, TileH, EgaPalette.Black, px, py);
        for (int y = 0; y < TileH; y += 12)
            R(0, y, TileW, 2, EgaPalette.Gray, px, py);
    }

    private static void DrawSlicer(int px, int py)
    {
        DrawFloor(px, py);
        R(13, 0, 6, 48, EgaPalette.DarkRed, px, py);
        R(14, 0, 4, 48, EgaPalette.Gray,    px, py);
    }

    private static void DrawArchBot(int px, int py)
    {
        DrawFloor(px, py);
        R( 0, 32, 8, 32, EgaPalette.Brown, px, py);
        R(24, 32, 8, 32, EgaPalette.Brown, px, py);
    }

    private static void DrawArchTop(int px, int py)
    {
        R(0, 0, TileW, TileH, EgaPalette.Black, px, py);
        R( 0, 0, 8, TileH, EgaPalette.Brown, px, py);
        R(24, 0, 8, TileH, EgaPalette.Brown, px, py);
    }
}
