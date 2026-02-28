using POPGame.Data;

namespace POPGame.Rendering;

/// <summary>
/// Maps TileId → 2-line (Top/Bot) console art + foreground colours.
/// Each string is exactly 6 characters wide to fill one tile cell.
/// </summary>
public static class TileVisuals
{
    public record TileVis(
        string Top, string Bot,
        ConsoleColor TopFg, ConsoleColor BotFg,
        ConsoleColor Bg = ConsoleColor.Black);

    // Gate-open visual — rendered when spec < GMaxVal
    public static readonly TileVis GateOpen =
        new("      ", "▓▓▓▓▓▓", ConsoleColor.DarkGray, ConsoleColor.Gray);

    private static readonly TileVis[] Visuals =
    [
        new("      ", "      ", ConsoleColor.DarkGray,   ConsoleColor.DarkGray),   //  0  space
        new("      ", "▓▓▓▓▓▓", ConsoleColor.DarkGray,   ConsoleColor.Gray),       //  1  floor
        new(@"/\/\/\", "▓▓▓▓▓▓", ConsoleColor.DarkRed,    ConsoleColor.Gray),       //  2  spikes
        new(" ╫ ╫  ", "▓╫▓╫▓▓", ConsoleColor.DarkYellow, ConsoleColor.DarkYellow), //  3  posts
        new("╫╫╫╫╫╫", "╫╫╫╫╫╫", ConsoleColor.DarkGray,   ConsoleColor.DarkGray),   //  4  gate (closed)
        new("      ", "▓═▓═▓▓", ConsoleColor.Magenta,    ConsoleColor.Magenta),    //  5  dpressplate
        new("      ", "▓╤▓╤▓▓", ConsoleColor.Magenta,    ConsoleColor.Magenta),    //  6  pressplate
        new("──────", "▓▓▓▓▓▓", ConsoleColor.Gray,       ConsoleColor.Gray),       //  7  panelwif
        new("  ██  ", "▓▓██▓▓", ConsoleColor.Yellow,     ConsoleColor.Yellow),     //  8  pillarbottom
        new("╤════╤", "  ██  ", ConsoleColor.Yellow,     ConsoleColor.Yellow),     //  9  pillartop
        new(" (●)  ", "▓▓▓▓▓▓", ConsoleColor.Green,      ConsoleColor.Gray),       // 10  flask
        new("      ", "╌╌╌╌╌╌", ConsoleColor.DarkGray,   ConsoleColor.Yellow),     // 11  loose
        new("──────", "──────", ConsoleColor.Gray,       ConsoleColor.Gray),       // 12  panelwof
        new("╔════╗", "╚════╝", ConsoleColor.Cyan,       ConsoleColor.Cyan),       // 13  mirror
        new("  ▄▄  ", "▓▓██▓▓", ConsoleColor.DarkGray,   ConsoleColor.DarkGray),   // 14  rubble
        new("      ", "▓╪▓╪▓▓", ConsoleColor.Magenta,    ConsoleColor.Magenta),    // 15  upressplate
        new("╔════╗", "╨▓▓▓▓╨", ConsoleColor.Green,      ConsoleColor.Green),      // 16  exit
        new("╔════╗", "╨▓▓▓▓╨", ConsoleColor.DarkGreen,  ConsoleColor.DarkGreen),  // 17  exit2
        new("  ╱╲  ", "▓▓▓▓▓▓", ConsoleColor.DarkRed,    ConsoleColor.Gray),       // 18  slicer
        new("  *   ", "▓▓│▓▓▓", ConsoleColor.DarkYellow, ConsoleColor.DarkYellow), // 19  torch
        new("▐█▌▐█▌", "▐█▌▐█▌", ConsoleColor.DarkGray,   ConsoleColor.DarkGray),   // 20  block
        new("      ", "▓╴╴╴╴▓", ConsoleColor.DarkGray,   ConsoleColor.DarkGray),   // 21  bones
        new(" ──══─", "▓▓▓▓▓▓", ConsoleColor.Yellow,     ConsoleColor.Gray),       // 22  sword
        new("░┼░┼░┼", "▓▓▓▓▓▓", ConsoleColor.DarkCyan,   ConsoleColor.Gray),       // 23  window
        new("▌┼▌┼▌┼", "▓▓▓▓▓▓", ConsoleColor.DarkCyan,   ConsoleColor.Gray),       // 24  window2
        new("█▌  ▐█", "▓▓▓▓▓▓", ConsoleColor.DarkYellow, ConsoleColor.Gray),       // 25  archbot
        new("▌╔══╗▐", "▌║  ║▐", ConsoleColor.DarkYellow, ConsoleColor.DarkYellow), // 26  archtop1
        new("▌╔══╗▐", "▌║  ║▐", ConsoleColor.DarkYellow, ConsoleColor.DarkYellow), // 27  archtop2
        new("▌╔══╗▐", "▌║  ║▐", ConsoleColor.DarkYellow, ConsoleColor.DarkYellow), // 28  archtop3
        new("▌╔══╗▐", "▌║  ║▐", ConsoleColor.DarkYellow, ConsoleColor.DarkYellow), // 29  archtop4
    ];

    public static TileVis Get(TileId id)
    {
        int i = (int)id;
        return i >= 0 && i < Visuals.Length
            ? Visuals[i]
            : new("??????", "??????", ConsoleColor.White, ConsoleColor.White);
    }
}
