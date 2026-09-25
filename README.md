# POPCS — Prince of Persia (1989) in C#, rebuilt by AI models

A C# / .NET 10 remake of Jordan Mechner's **Prince of Persia**. It plays from the
original DOS game's own data and is built to match the DOS version, down to the pixel
where it's been checked.

It's also an experiment. I didn't write or edit the code myself. Every line was written by
AI coding models (Claude Opus 4.6, OpenAI Codex, Claude Opus 5 and Claude Opus 5.5),
working from Mechner's published Apple II source and, later, from comparisons against the
real DOS game running in DOSBox. The git history keeps one commit per stage, and
[`SESSION_HISTORY.md`](SESSION_HISTORY.md) records every session: the model, my prompts
word for word, and what changed.

> **No game data is included.** This repository contains only source code. To run it you
> need your own copy of the original DOS Prince of Persia; the program reads the
> animation tables, levels and artwork from those files at runtime.

## Status

| Works | Not yet |
|-------|---------|
| The prince's animation, driven by the original frame and sequence tables | Guards and sword fighting |
| Running, jumping, climbing, hanging, crouching, falling | Palace levels (4, 5, 6, 10, 11, 14) use dungeon art |
| Level 1 route: loose floors, gates, pressure plates, the exit | Sound |
| Room drawing ported from the original, **pixel-exact** on the rooms checked against DOSBox | Exact landing positions of the prince |

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/)
- The DOS version of Prince of Persia. The folder must contain `PRINCE.EXE`,
  `LEVELS.DAT`, `VDUNGEON.DAT`, `KID.DAT` and `PRINCE.DAT`.
  - Point `POP_DOS_DIR` at it, or put it at `E:\DOS\games\Prince`.
  - At startup the program checks the tables it reads from `PRINCE.EXE`. Only one copy of
    the game has been tested, so another release may fail that check with an error.
- OpenGL for the game window. Without a GPU, use the headless mode below.

## Running

```bash
dotnet run --project POPGame -- 1        # play level 1 (levels 0-14)
```

In Visual Studio, open `POPCS.slnx` and set **POPGame** as the startup project. The
solution lists the legacy `LevelEditor` first.

**Controls:**
- Arrow keys: run, jump, climb, crouch
- Shift or Space: careful step / action
- R: restart the level
- Esc: pause

**Headless** (no window or GPU; writes one PNG per tick):

```bash
dotnet run --project POPGame -- --dump out 1 ".20 R40 .10 R25" 1
dotnet run --project POPGame -- --room room.png 1 5      # render one room
```

Script tokens are `<keys><count>`, with keys `L R U D S` or `.` for none.

## Layout

| Path | What |
|------|------|
| `POPGame/Dos/` | Readers for the DOS files: `.DAT` images and palettes, levels, the EXEPACK unpacker, and the tables in `PRINCE.EXE` |
| `POPGame/Sim/` | The simulation: the original's sequence interpreter, player control, physics, gates, plates |
| `POPGame/Rendering/` | Room drawing (port of the original's routine) and the framebuffer |
| `tools/DatDump/` | Dumps any DOS `.DAT` file to PNG contact sheets |
| `LevelEditor/`, `POPCS/` | The first versions (Feb 2026): text level viewers for the Apple II level files |
| `CLAUDE.md` | Technical notes: file formats, offsets, coordinate system, what was verified and how |
| `CHANGELOG.md`, `SESSION_HISTORY.md` | What changed, and how the project got here |

The Apple II source isn't included. To use the references in `CLAUDE.md`, clone
[jmechner/Prince-of-Persia-Apple-II](https://github.com/jmechner/Prince-of-Persia-Apple-II)
into `originalcode/`. That folder is gitignored.

## Credits

- **Jordan Mechner** created Prince of Persia and published the original Apple II source:
  <https://github.com/jmechner/Prince-of-Persia-Apple-II>
- **David Nagy and the princed.org community** made **SDLPoP**, the open-source port of
  the DOS game reconstructed from its disassembly: <https://github.com/NagyD/SDLPoP>.
  `POPGame/Rendering/DosRoomDrawer.cs` is a C# port of SDLPoP's room-drawing code
  (`src/seg008.c`), and that is why this project is licensed under the GPL.
- **Fabien Sanglard**'s code review of the original source:
  <https://fabiensanglard.net/prince_of_persia/>

## License

Copyright (C) 2026 Priyan Rajeevan

This program is free software: you can redistribute it and/or modify it under the terms of
the **GNU General Public License** as published by the Free Software Foundation, either
version 3 of the License, or (at your option) any later version. See [`LICENSE`](LICENSE).

## Disclaimer

This is a non-commercial fan project, made for study and fun. It is not affiliated with,
endorsed by, or connected to Ubisoft or Jordan Mechner. *Prince of Persia* is a trademark of
Ubisoft Entertainment. Jordan Mechner's source release explicitly grants no rights in the
Prince of Persia franchise, and neither does this project. No original game data or
artwork, and none of the original source files, are distributed here. You need your own
legally obtained copy of the DOS game to run it.
