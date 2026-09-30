# POPCS — Prince of Persia (C# remake, DOS-accurate)

C# .NET 10 remake that runs off the **original DOS install**; no game data lives in this
repo. GPL-3.0-or-later (`DosRoomDrawer.cs` ports SDLPoP).

- **POPGame** — the game (Raylib window, or headless PNG dump)
- **LevelEditor** — legacy Apple II level viewer
- **tools/DatDump** — dumps a DOS `.DAT` to PNGs + labelled contact sheets
- Apple II 6502 source: `originalcode/Prince-of-Persia-Apple-II-master/01 POP Source/Source/`

**Docs:** `HISTORY.md` (findings, verification, abandoned approaches),
`PENDING.md` (open work), `CHANGELOG.md` (newest first; add an entry when behaviour
changes), `SESSION_HISTORY.md` (per-session log).

## Golden rule
Port the original; don't tune. When something differs, find the routine (Apple II source
or SDLPoP), port it with the tables read from PRINCE.EXE, and verify against DOSBox. Hand-
placed offsets and guessed constants have been wrong every time (see HISTORY.md).

## Build & run
```bash
dotnet build POPGame/POPGame.csproj
dotnet run --project POPGame -- 1                     # play level 1 (needs OpenGL)
POPGame.exe --dump <outDir> <level> "<script>" [everyNth]   # headless, one PNG per tick
POPGame.exe --room <out.png> <level> <room>                 # render one room
POP_START=6,4,-1 POPGame.exe --dump out 1 ".2 LU2 .30"      # start at room,block,face
POP_DEMO=1 POPGame.exe --dump out 0 ".150"                  # replay the attract-mode demo
```
Script tokens are `<keys><count>`; keys `L R U D S` (S = shift), `.` = none.
DOS assets: `POP_DOS_DIR`, else `E:\DOS\games\Prince` (needs `PRINCE.EXE`, `LEVELS.DAT`,
`VDUNGEON.DAT`, `KID.DAT`).

## Data sources
| What | Where | Shape |
|------|-------|-------|
| Frame table | unpacked EXE `0x1BCC5` | 240 x `image, sword, dx, dy, flags`; frame N at base+(N-1)*5 |
| Sequence bytecode | `0x1ACE0` | positive = frame no., negative = opcode (layout = `SEQTABLE.S`) |
| Sequence index | `0x1C568` | words, 1-indexed; `[1]`=`0x1973`, `[2]`=`0x19A0` |
| Room-drawing tables | unpacked EXE | `Dos/DosDrawTables.cs` |
| Levels | `LEVELS.DAT` id `2000+N` | 2304-byte Apple II level blob (level 0 = demo) |

Offsets are into the **unpacked** load image (`ExePack.Unpack`, no MZ header). Never read
tables from the PRINCE.EXE file: EXEPACK stores byte runs as commands, which garbles them
and shifts everything after.

Frame flags: `0x40` = check floor (fcheckmark), `0x1F` = foot offset, `0x20` thin,
`0x80` odd. `image == 255` = blank. Kid sprite = KID.DAT `401 + image`.

**.DAT:** `uint32 indexOffset; uint16 indexSize`; index = `uint16 count` then
`{uint16 id; uint32 offset; uint16 size}`; each payload is preceded by one checksum byte.
Image: `uint16 height, width, flags`; `depth=((flags>>12)&7)+1`, `cmeth=(flags>>8)&0xF`
(0 raw, 1 RLE-lr, 2 RLE-ud, 3 LZG-lr, 4 LZG-ud); MSB-first; index 0 transparent.
A DAT can hold **several palette groups** and loose resources outside them (a palette
parses as an image of width 4096). Chtab ids: dungeon N = VDUNGEON `200+N`, walls N =
`360+N` (palette 360 — dump with 360 or it's miscoloured), flame/potion/sword N =
PRINCE.DAT `150+N`. Colour 6->8 bit is `(v<<2)|(v>>4)`.

## Coordinates (`Sim/Coord.cs`, TABLES.S)
- X in **units**: 14 per block, room spans 58..198 (`ScrnLeft=58`, `ScrnWidth=140`).
- Y in **pixels**: `BlockHeight=63`; standing Y = `191 - (2-R)*63 - 10` (55/118/181).
- `Face`: **-1 = left** (sprites face left natively), **0 = right**; `AddX` is facing-relative.
- `Col` (curr_col) is the column under the kid's **weight**: `ColM7(x + fwd(dx - (flags&0x1F)))`
  — characters stand 7 units in front of the tile plane. Walls collide 7 units into
  their tile. Level start x = block edge + 14 (DO_STARTPOS).
- Sprite: `obj_x = 2*(x + fwd(dx)) - 116` (+1 odd-pixel), screen px = `obj_x*320/280`;
  facing left the sprite starts there, facing right it ends there (`KidEngine.SpriteX`).

## Architecture
```
Dos/        DosGame, DatFile, DatImage, DosImageBank, DosTables, ExePack,
            DosDrawTables, DosLevels, PngWriter
Sim/        Coord, CharState (+CharAction, Seq ids), SeqRunner (play_seq / ANIMCHAR),
            RoomView, KidEngine (SDLPoP port: .cs frame order/tiles/room exit,
            .Physics.cs falls/landing/grab/wall collisions, .Control.cs controls),
            Hazards (check_press, plates, gates, loose floors), Simulation
Rendering/  Framebuffer (320x200), DosRoomDrawer (DRAW_ROOM port), DosRenderer
Engine/     DosGameLoop (Raylib), HeadlessRun
Data/       Level, TileId
```
All drawing goes through `Framebuffer`, so live game and dumps are pixel-identical.

**Per tick** (SDLPoP play_kid_frame, in `KidEngine.PlayKidFrame`): control picks a
sequence → play_seq → fall_accel/fall_speed → set_char_collision → check_collisions /
check_bumped / check_gate_push → check_action (floor, fall, land, grab) → `Hazards`
(check_press) → exit_room. `KidEngine` keeps SDLPoP's routine names and shared globals
(`curr_tile2`, `tile_col`, latched controls), so each method can be checked against
seg002/004/005/006. States are identified by **action + frame**, not action alone.
Sequences chain themselves via `goto`, so control only chooses entry points.

## Level format (2304 bytes)
| Offset | Size | Section |
|--------|------|---------|
| 0x000 | 720 | BLUETYPE: tile id (low 5 bits), 24 screens x 30 cells |
| 0x2D0 | 720 | BLUESPEC: modifier (gate height, etc.) |
| 0x5A0 | 256 | LINKLOC |
| 0x6A0 | 256 | LINKMAP |
| 0x7A0 | 96 | MAP: per screen `[left,right,above,below]`, 1-indexed, 0 = none |
| 0x800 | 256 | INFO: `[64]` KidStartScrn, `[65]` block, `[66]` face (0xFF=left), `[71+]` guards |
Screen N's cells start at `(N-1)*30`; `cell = row*10 + col`, row 0 = top.

**Tiles:** 0 space, 1 floor, 2 spikes, 3 posts, 4 gate, 5 stuck plate, 6 closer plate,
7 panelwf, 8 pillarbot, 9 pillartop, 10 flask, 11 loose, 12 panelwof, 13 mirror,
14 rubble, 15 opener plate, 16/17 exit, 18 slicer, 19 torch, 20 block, 21 bones,
22 sword, 23/24 window, 25 archbot, 26-29 archtop.
- **No floor** (CMPSPACE): 0, 9, 12, **20**, 26-29. Use this exclusion list.
- **Walls** (wall_type): gate/7/12 = wall at the block's right, 13 = at its left, 18 =
  chomper, 20 = both sides. Beyond the level's edge (room 0) is wall.

## Comparing against DOSBox
Use the `dosbox-control` skill, but: port **5000**, launch with `SDL_VIDEODRIVER=windib`,
drive it from PowerShell, `args='megahit'` (Shift+L skips a level). Crop the client area
at `(3,32)-(643,432)` and halve it to 320x200. For movement, record captures in a loop
while the user plays. Keypresses sent by the skill did not reach the game on
2026-09-30, but the attract-mode demo needs none: record it and compare with
`POP_DEMO=1 --dump out 0` frame by frame (see HISTORY.md).

## Pitfalls
- X is units, not pixels. Face is -1/0, not -1/+1.
- The kid engine is a line-by-line port. Don't adjust x, collision or drawing offsets
  in it; find the SDLPoP routine and compare. It matches the demo pixel for pixel.
- Don't mask palette indices 14/15; they're real colours.
- OpenGL is unavailable in some remote sessions; verify with `--dump`.
