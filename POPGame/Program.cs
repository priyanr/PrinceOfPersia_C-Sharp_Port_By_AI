using POPGame.Dos;
using POPGame.Engine;

// The game runs off the original DOS install: PRINCE.EXE supplies the frame and
// sequence tables, LEVELS.DAT the levels, and the graphics DATs the artwork.
// Point POP_DOS_DIR at it if it is not in one of the usual places.
//
//   POPGame [level]
//   POPGame --dump <outDir> <level> "<input script>" [everyNth]
//
// --dump renders without a window (no GPU needed) and writes one PNG per tick.
if (!DosGame.TryLocate(out string dosDir))
{
    Console.Error.WriteLine(
        "ERROR: could not find the DOS Prince of Persia install.\n" +
        "It must contain PRINCE.EXE, LEVELS.DAT, VDUNGEON.DAT and KID.DAT.\n" +
        "Set POP_DOS_DIR to its path and run again.");
    return 1;
}

Console.WriteLine($"Using DOS assets from: {dosDir}");

if (args.Length > 0 && args[0] == "--dump")
{
    if (args.Length < 4)
    {
        Console.Error.WriteLine("usage: POPGame --dump <outDir> <level> \"<script>\" [everyNth]");
        return 1;
    }
    int everyNth = args.Length > 4 ? int.Parse(args[4]) : 1;
    return HeadlessRun.Run(int.Parse(args[2]), args[1], args[3], everyNth);
}

if (args.Length > 3 && args[0] == "--room")
    return HeadlessRun.Room(args[1], int.Parse(args[2]), int.Parse(args[3]));

if (args.Length > 0 && args[0] == "--probe")
{
    if (args.Length < 4)
    {
        Console.Error.WriteLine("usage: POPGame --probe <outFile.png> <i1,i2,...> <pieceY>");
        return 1;
    }
    var ids = args[2].Split(',').Select(int.Parse).ToArray();
    return HeadlessRun.Probe(args[1], ids, int.Parse(args[3]));
}

int level = 1;
if (args.Length > 0 && int.TryParse(args[0], out int n) && n is >= 0 and <= 14)
    level = n;

new DosGameLoop(level).Run();
return 0;
