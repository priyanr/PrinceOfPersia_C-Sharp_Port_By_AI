# POPCS — Prince of Persia (C# remake, DOS-accurate)

## Project Overview
A C# .NET 10 remake of Prince of Persia that runs off the **original DOS install**
rather than hand-transcribed data. Nothing copyrighted lives in this repo.

- **POPGame** (`POPGame/`) — the game (Raylib window, or headless PNG dump)
- **LevelEditor** (`LevelEditor/`) — read-only level viewer (legacy, Apple II data)
- **tools/DatDump** — dumps any DOS `.DAT` to PNGs + labelled contact sheets
- Apple II 6502 reference source: `originalcode/Prince-of-Persia-Apple-II-master/01 POP Source/Source/`

Notable changes are logged in `CHANGELOG.md`, newest first. Add an entry when you change
behaviour or overturn something this file used to claim.

## DOS assets (required to run)
Located via `POP_DOS_DIR`, else `E:\DOS\games\Prince` (see `Dos/DosGame.cs`).
Must contain `PRINCE.EXE`, `LEVELS.DAT`, `VDUNGEON.DAT`, `KID.DAT`.

## Build & Run
```bash
dotnet build POPGame/POPGame.csproj
dotnet run --project POPGame -- 1               # play level 1 (needs OpenGL)
# headless: renders one PNG per tick, no GPU required
POPGame.exe --dump <outDir> <level> "<script>" [everyNth]
POPGame.exe --dump out 1 ".2 R24 RU3 R10" 1
```
Input script tokens are `<keys><count>`; keys are `L R U D S` (S = shift) or `.` for none.

## Where the authentic data comes from
`PRINCE.EXE` **is packed with Microsoft EXEPACK** (the stub at CS:0 has the "RB"
signature and "Packed file is corrupt"). EXEPACK only squeezes runs of repeated bytes,
so most of the image survives verbatim in the file — which is why the frame and
sequence tables below can be read in place at these *file* offsets. Anything holding a
long zero run (the tile drawing table has ten) must be read from the unpacked image:
`Dos/ExePack.Unpack`. Offsets were found by matching byte patterns from the Apple II
source and verified against two independent anchors each (`Dos/DosTables.cs`,
`Validate()` re-checks at startup):

| What | Offset | Shape |
|------|--------|-------|
| Frame table | `0x1B9AA` | 240 x 5 bytes: `image, sword, dx, dy, flags`; frame N at base+(N-1)*5 |
| Sequence bytecode | `0x1A8ED` | positive byte = frame no., negative = opcode |
| Sequence index | `0x1C175` | words, 1-indexed; `[1]`=startrun=`0x1973`, `[2]`=stand=`0x19A0` |

`dx`/`dy`/`flags` are byte-identical to Apple II `FRAMEDEF.S`; only `image`/`sword`
differ (DOS uses flat 0-based chtab indices). `image == 255` means a blank slot.
The sequence bytecode layout matches `SEQTABLE.S` exactly, so `SEQDATA.S` is a valid
key to the sequence ids (mirrored in `Sim/CharState.cs` as `Seq.*`).

## .DAT container format (`Dos/DatFile.cs`, `Dos/DatImage.cs`)
```
uint32 indexOffset; uint16 indexSize;      // indexOffset + indexSize == file length
... payloads, each preceded by ONE checksum byte ...
at indexOffset: uint16 count, then count x { uint16 id; uint32 offset; uint16 size; }
```
Image resource: `uint16 height, width, flags`, then payload.
- `depth = ((flags>>12)&7)+1` — POP1 uses 4bpp
- `cmeth = (flags>>8)&0x0F` — 0 raw, 1 RLE-lr, 2 RLE-ud, 3 LZG-lr, 4 LZG-ud
- `stride = (depth*width+7)/8`; pixels packed MSB-first; palette index 0 = transparent

Palette resource (`dat_shpl`): `byte nImages; uint16 rowBits; byte nColors; rgb[16]`
with 6-bit VGA components. **A DAT can hold more than one palette group** — a shpl
resource is recognisable because it parses as an image of width 4096.

| DAT | Palette groups | Notes |
|-----|----------------|-------|
| `VDUNGEON.DAT` | 200 (151 images), 360 (17) | 360 is the wall-brick bank; loose resources at 1030+ (pillars, spikes, debris) sit outside any declared run |
| `KID.DAT` | 400 (219) | `sprite id = 401 + frame.image` |
| `PRINCE.DAT` | **150 (23)**, 700 (34) | 150 is the flame/sword/potion bank with a *fire* palette — images 1-9 are the torch flame loop, 12-22 potions, 551+ swords |
| `PV.DAT` | 800, 850, 900, 950, 980 | princess / vizier / mouse |
| `GUARD.DAT` | **none** | guards borrow the level palette, which is why they recolour per level |

The dungeon palette is entirely blue-greys and teals — **no orange** — so anything
that looks like fire must come from the `PRINCE.DAT` 150 bank, not VDUNGEON.
`DosImageBank` takes several palette ids and matches each image to its group.

`LEVELS.DAT`: resource id `2000+N` is level N as the same 2304-byte blob the Apple II
`LEVELn` files use (verified 99-100% byte identical), parsed by `Level.Load(byte[])`.

## Coordinate system (from TABLES.S — `Sim/Coord.cs`)
- **X is in units, not pixels**: `ScrnWidth = 140`, 14 units per block, block 0's left
  edge at `ScrnLeft = 58`, so a room spans x 58..198. Render scale is `32/14`.
- **Y is in pixels**: `BlockHeight = 63`, `ScrnBot = 191`, `DHeight = 3` (floorpiece),
  `VertDist = 10` (block bottom to centre plane).
- A character standing in row R has `Y = FloorY(R) = 191 - (2-R)*63 - 10`
  (row 0 = 55, row 1 = 118, row 2 = 181).
- `Face`: **-1 = left** (sprites are drawn facing left natively), **0 = right**.
  `AddX(dx)` applies a delta in the facing direction (ADDCHARX, CTRLSUBS.S:353).

## POPGame architecture
```
POPGame/
  Program.cs                entry: locate DOS dir, then play or --dump
  Dos/DosGame.cs            finds the DOS install (POP_DOS_DIR or known paths)
  Dos/DatFile.cs            .DAT index reader
  Dos/DatImage.cs           4bpp image decoder + all five compression codecs
  Dos/DatPalette            16-colour 6-bit VGA palette
  Dos/DosImageBank.cs       a DAT decoded to CPU images, addressed by resource id
  Dos/DosTables.cs          frame table + sequence table out of PRINCE.EXE
  Dos/ExePack.cs            EXEPACK unpacker for PRINCE.EXE
  Dos/DosDrawTables.cs      the room-drawing tables (tile_table etc.) out of PRINCE.EXE
  Dos/DosLevels.cs          levels out of LEVELS.DAT
  Dos/PngWriter.cs          dependency-free RGBA PNG encoder
  Sim/Coord.cs              the original coordinate system
  Sim/CharState.cs          character state + CharAction + Seq ids
  Sim/SeqRunner.cs          ANIMCHAR: the sequence-table interpreter (COLL.S:994)
  Sim/RoomView.cs           tile queries with MAP neighbour resolution
  Sim/KidControl.cs         PLAYERCTRL: input -> which sequence to jump to
  Sim/Physics.cs            gravity, landing, walls, room transitions
  Sim/Simulation.cs         per-tick orchestration
  Rendering/Framebuffer.cs  320x200 RGBA buffer + indexed blitter with the original's blit modes
  Rendering/DosRoomDrawer.cs  port of the original's DRAW_ROOM / draw_tile_* -> list of placements
  Rendering/DosRenderer.cs  back layer, kid, front layer into the framebuffer
  Engine/DosGameLoop.cs     Raylib window; uploads the framebuffer as one texture
  Engine/HeadlessRun.cs     scripted input -> one PNG per tick, no GPU
  Data/Level.cs, Data/TileId.cs   level blob parsing (shared with the Apple II files)
```
All drawing goes through `Framebuffer`, so the live game and the headless dumps
produce identical pixels and neither needs a GPU.

## Per-tick order (matches the original)
1. `KidControl` reads input and may jump to a new sequence — it never moves anything.
2. `SeqRunner.Animate` executes opcodes until it hits a frame number; `chx/chy/up/down/
   act/setfall/goto` mutate the character on the way.
3. `Physics` applies gravity, resolves floors/walls, and handles room transitions.

The original disambiguates states by **action + frame number**, not action alone
(several sequences share `act,1`). `KidControl` does the same: standing is
`Action==Stand && Frame==15`, running is `Action==RunJump && Frame in 1..14`.
The crouch test also needs the sequence id — softland and stoop *park* on frame 109,
but medland passes straight through it.

## Sequences chain themselves
Much of the state machine is already in the bytecode's `goto`s, so the control layer
only has to pick the right entry point:
```
jumphangMed(8) -- frames 67..77, act,2, 78..80 --> hang(9) -- sway --> hangdrop(11) --> stand(2)
hang(9) + Up --> climbup(10) -- 135..140, chx,5, chy,-63, up, 141..149 --> stand(2)
jumpup(14)  -- touches the ceiling --> hangdrop(11) --> stand(2)
fallhang(15) -- act,3, frame 80 --> hang(9)            (the grab while falling)
climbdown(68) -- chy,+63, down --> hang(9)
```
So `Up` only has to choose **jumphangMed when there is a ledge overhead, jumpup when
there isn't** — everything after that follows from the data. `chy,193` in climbup is
`-63` read as a signed byte, i.e. exactly one block.

## Tile rendering
Rooms are drawn by `Rendering/DosRoomDrawer.cs`, a straight reimplementation of the
original's room-drawing routine (DRAW_ROOM and the `draw_tile_*` family, as
reconstructed in SDLPoP's `src/seg008.c`), driven by tables read out of PRINCE.EXE
(`Dos/DosDrawTables.cs`). **Nothing is hand-placed.** Checked against DOSBox captures,
the backgrounds of level 1 rooms 1 and 2 and the level 3 start room (exit door included)
match **pixel for pixel**. The only differences left are torch flame phase and
simulation state.

Before this, tiles were template-matched off screenshots one piece at a time. That got
the verified pieces right and everything else wrong (brick pattern, gate layers, any tile
not in a captured room). Don't go back to tuning offsets; if something is off, the port
or the sim state is wrong.

### The tables (all in one block of the unpacked data segment)
`tile_table` is 31 rows x 12 bytes: `base_id, floor_left, base_y, right_id, floor_right,
right_y, stripe_id, topright_id, bottom_id, fore_id, fore_x, fore_y`. It is located by its
first two rows (12 zeros, then `41,1,0,42,1,2,145,0,43,0,0,0`), and every small frame
array follows it at a fixed offset (loose, chomper, spikes, `door_fram_top/slice`,
`blueline_fram1/_y/3`, `wall_fram_bottom/main`, `potion_fram_bubb`, the wall-mark
`LPOS`/`RPOS` words). The offsets are in `DosDrawTables`, and `Validate()` checks a
second anchor in each.

Image ids: chtab 6 id N = VDUNGEON resource **200+N**; chtab 7 (walls) id N =
**360+N**; chtab 1 (flame/potion/sword) id N = PRINCE.DAT **150+N**.

### Geometry and order
- `xh = 4*col` (8px units), `x = xh*8 + xl`; `draw_bottom_y = 63*row + 65`,
  `draw_main_y = bottom - 3`. Every blit is **bottom-anchored**: top = `y - h + 1`.
  No extra offset is needed. (An earlier "+1 row" calibration was a bad DOSBox crop,
  see below.)
- Rows are drawn 2 -> 0, columns 0 -> 9. Each cell runs `floorright, anim_topright,
  right, anim_right, bottom, loose, base, anim, fore`. After that, the bottom row of the
  room above is drawn at `main_y=-1` and `bottom_y=2`, which is where the ceiling comes
  from. A room with nothing above gets a row of floors there.
- **A tile is drawn in two halves.** Its `base` goes in its own cell, but its
  `right_id` is drawn by the cell to its **right**, keyed on "what is my left
  neighbour". This is why the torch bracket, the gate lattice and the posts' flank all
  sit one cell right of their tile. Column 0 draws the left room's column 9 right half
  (the sliver on the left edge). A missing neighbour room counts as a wall.
- Wall bodies (`wall_fram_main`), wall seams/marks, pillar fronts, spike fronts, potion
  bubbles and a gate the kid is standing under go into the **front** layer and are drawn
  over the kid.

### Modifiers (DOS level data)
The per-tile modifier is **BLUESPEC**. The BLUETYPE top bits don't affect drawing. The
original rewrites some modifiers when a room loads (LOAD_ALTER_MOD), and the drawer does
the same:
- **Walls:** bits 0-1 record which neighbours are walls (SWS=0, SWW=1, WWS=2, WWW=3), and
  a spec of 1 sets bit 7 ("no blue"). `wall_fram_main[conn]` = 8/10/6/4, so the body is
  368/370/366/364. 364 is the interior wall, and 368 (a lone wall) is rare.
- **Gates:** in the level data, a spec of 1 means open and anything else means shut.
  `DosLevels.NormaliseGates` converts this to the sim's 0..47 height, and the drawer
  uses `height*4` (0..188).
- **Floors:** spec 1..3 draws the back-wall decoration `blueline_fram3` (244/245) at
  `main_y-20` in the cell to the right. These are the "crosses".
- **Empty tiles:** spec 1..3 draws `blueline_fram1` (324/325/326).

### Masonry is seeded, not random
`wall_pattern` reseeds the original's PRNG on every call with
`seed = room + row*10 + col`, then advances it once and discards the result.
`prandom(max)` is Microsoft C's LCG: `seed = seed*214013 + 2531011;
return (seed>>16) % (max+1)`. The seams (`371/372` = dividers, at `xh+1`+offset for the
middle course and `xh`+offset for the bottom), the random block `373`, and the marks
`374-377` all come from it. So the masonry is identical on every visit.

### Gate
The gate is drawn by the cell to its right:
- `gate_bottom_y = main_y - (height+1)`
- 8px slices `252` run upward from `gate_bottom_y-12` while above `bottom_y-62`
- `door_fram_slice[k]` is the partial top course
- when raised more than 12px, `250` is the bottom piece; otherwise the cell's own art is
  redrawn and `251` goes over it

`door_fram_slice` shares its first byte with the end of `door_fram_top` in the EXE.

**Level 1 starts with the gate open.** DO_STARTPOS presses the closer in room 5
(row 0, col 2), so the gate beside room 1 slams shut as the kid drops in. That is why
every capture shows it shut. `Simulation.StartEvents` does the same, and closer plates
(tile 6) now close gates instead of opening them.

### Palette indices 14 and 15 are real colours
They are the teal/green of the exit-door frame, confirmed against the level 3 start
room. An earlier theory that they were "stencil markers" came from drawing the wrong
pieces (`237` rows used as a floor slab). Don't mask them.

### Characters
The kid sprite is currently drawn **centred on the sim's x**, with y as the sprite's
bottom row (`y - h + 1`, checked against DOSBox).

The original anchors differently. LOAD_FRAME_TO_OBJ works in a 280-wide space:
`obj_x = 2*(x + dx) - 116`, plus 1 when `(sbyte)(flags ^ direction) >= 0`, then scaled by
320/280. Facing left, the sprite's **left** edge is at `obj_x`; facing right, its
**right** edge is. **Don't switch to this until the sim's x convention is ported too.**
Our x is not the original's x plus a constant. Trying `+7` (2026-09-24) matched the
original facing left but sank the kid ~7px into walls facing right, so it was reverted
on 2026-09-25.

Two things to port first:
- how COLL.S/CTRLSUBS position the kid against walls
- DO_STARTPOS's entry, which starts the kid facing the opposite way and plays the turn
  sequence; level 1 plays the fall instead

Then verify against DOSBox in **both** facing directions.

### Palette: the VGA DAC replicates, it does not scale
6-bit DAT colour -> 8-bit is `(v << 2) | (v >> 4)`, **not** `v * 255 / 63`. The old
truncating version was off by one on several entries, which silently broke every exact
template match against a DOSBox capture. Fixed in `DatImage.Scale6`.

### Identifying a piece
```bash
dotnet run --project tools/DatDump -- "E:/DOS/games/Prince/VDUNGEON.DAT" out 200
dotnet run --project tools/DatDump -- "E:/DOS/games/Prince/VDUNGEON.DAT" out360 360
```
Resources 361-377 belong to palette group **360** and come out miscoloured if dumped
with 200. Sheets are labelled with the image index (= resource id - palette id).
To check a room, capture it in DOSBox, crop the window to native 320x200 and pixel-diff
it against `POPGame --room <out.png> <level> <room>`. The client area of a default-size
window is 640x400 at **`(3,32)`-`(643,432)`**; halve it. The old recipe of `(3,30)` was
one game row too high: row 0 of the crop is window chrome (243,243,243). If the top row
of a crop isn't the ceiling colour, the crop is wrong.

## Level file format (2304 bytes; same for Apple II files and LEVELS.DAT resources)
| Offset | Size | Section  | Description |
|--------|------|----------|-------------|
| 0x000  | 720  | BLUETYPE | tile type for 24 screens x 30 cells |
| 0x2D0  | 720  | BLUESPEC | tile state (gate height, spike frame, etc.) |
| 0x5A0  | 256  | LINKLOC  | trigger -> linked-cell lookup |
| 0x6A0  | 256  | LINKMAP  | linked screen for each LINKLOC entry |
| 0x7A0  |  96  | MAP      | 24 screens x 4 bytes [left,right,above,below] |
| 0x800  | 256  | INFO     | player/guard start positions |

- 24 screens per level (1-indexed in game), 30 cells per screen = 3 rows x 10 cols
- `cell_index = row*10 + col`; screen N's blueprint starts at `(N-1)*30`
- BLUETYPE byte: lower 5 bits = tile id, upper 3 bits = editor flags
- MAP values are 1-indexed screen numbers; 0 = no neighbour
- INFO: `[0]`=screen count, `[64]`=KidStartScrn, `[65]`=KidStartBlock,
  `[66]`=KidStartFace (0xFF=left), `[68-69]`=SwordStart, `[71+]`=guard arrays (24 each)

## Tile IDs (BGDATA.S / MOVEDATA.S)
0=space, 1=floor, 2=spikes, 3=posts, 4=gate, 5=dpressplate, 6=pressplate,
7=panelwf, 8=pillarbot, 9=pillartop, 10=flask, 11=loose, 12=panelwof,
13=mirror, 14=rubble, 15=upressplate, 16=exit, 17=exit2, 18=slicer, 19=torch,
20=block, 21=bones, 22=sword, 23=window, 24=window2, 25=archbot, 26-29=archtop1-4

**Floor convention:** almost every tile has a walkable surface. A character stands at
the *same* row as the floor tile. The list of tiles *without* one is `CMPSPACE`
(CTRLSUBS.S:1495) — space (0), pillartop (9), panelwof (12), **block (20)** and
archtop1-4 (26-29).

**Solid block (20) has no floor.** CMPSPACE says so in as many words: it is a wall, not
a ledge — it blocks movement through its cell *and* you cannot stand on it. Leaving it
out of the exclusion list lets characters stand on top of walls and climb to places the
original never allows.

**Barriers** are `CMPBARR` (CTRLSUBS.S:1532), a different and smaller set: panelwf (7),
panelwof (12), gate (4) while it is low, mirror (13), slicer (18) while extended, and
block (20). A flask is explicitly "not really a barrier" (COLL.S:529).

The barrier check is **waived** while a character is hanging (`CharAction` 2 or 6),
turning (7), or part-way through the climbup frames 135..148 — COLLISIONS (COLL.S:409)
and CHECKBARR (COLL.S:80). Without that waiver a character that has just pulled itself
onto a ledge counts as standing inside the block it climbed, and the push-out shoves it
straight back off.

**Grabbing a ledge** is `CHECKLEDGE` (CTRLSUBS.S:1814), and its shape is easy to get
backwards. Pressing Up jumps for a ledge when:

* the cell **directly above** is clear — not a block, not a floorless panel while facing
  right, and CMPSPACE-clear (i.e. it has no floor), *and*
* the cell **above and one column in the facing direction** does have a floor — with
  `loose` disqualified once it has already been triggered, and `panelwf` grabbable only
  when facing right.

Testing for a floor *directly overhead* instead finds no ledge anywhere. `DoJumpup`
(CTRL.S:1633) tries above-in-front first, then above-and-behind (stepping back a block
before the jump), and only then falls through to a plain jumpup.

Because tile lookups resolve through the MAP links, "in front" reaches across a room
boundary. Standing at the **left edge of room 2 facing left**, the ledge caught is
column 9 of room 6, and climbing it carries the kid into room 6 — that is the
`1 → 2 → 6 → 8` branch of level 1, and it is unreachable if the ledge test is wrong.

## Level 1 route (verified against LEVELS.DAT)
Start room 1, cell 0 = row 0 col 0, which is **space**, so the kid drops one row onto
the torch tile. Then: walk right along row 1, fall through the col-4 gap to row 2,
walk right and **stand on the loose floor at row 2 col 6** so it gives way, dropping
into screen 2. Then right through screen 3 (guard at row 1 col 7) into screen 9, which
has the opener plate at row 0 col 0 and the exit at row 1 cols 3-4 — so the plate has
to be climbed to before the exit opens.

Tile 6 = "closer" button, tile 15 = "opener" button, tile 5 = stuck button.

## Level 1 reference data
- KidStartScrn=1, block=0 (row 0, col 0), face=0xFF (left) — that cell is **space**,
  so the kid drops one row onto the torch tile at the start.
- Screen 1 row 0: space space space floor floor floor floor floor block block
- Screen 1 row 1: torch torch floor posts space block block block block block
- Screen 1 row 2: block block block block rubble posts loose floor floor block
- Screen 1 neighbours: left=5, right=0, above=0, below=2
- Guards: screen 3 block 17, screen 21 block 6

## Comparing against the original
DOSBox automation runs through the `dosbox-control` skill, with two corrections:
the server listens on **port 5000** (not the documented 5164), and DOSBox only starts
when **`SDL_VIDEODRIVER=windib`** is set in the environment of whatever launches it —
with the default DirectX driver it exits silently with code 0.
Drive it from PowerShell (`ConvertTo-Json` + `Invoke-RestMethod`); bash heredocs and
`curl -d` mangle the doubled backslashes the JSON `exePath` needs.
Launch with `args = 'megahit'` to enable the cheat keys: **Shift+L** skips a level.
Press Enter to skip the intro first; a Shift+L sent during the intro still counts. Each
level's start room contains its entrance door, which is handy for checking door art.

## Common pitfalls
- X is in 14-per-block units, **not** pixels; only Y is in pixels.
- `Face` is -1/0, not -1/+1. Sprites face left natively; mirror when facing right.
- Frame table `image` is 0-based; `DosImageBank` is addressed by resource id, so the
  kid sprite is `401 + frame.image`.
- A DAT can declare several palette groups and also carry resources outside them —
  never assume ids are one contiguous run from the palette resource.
- `IsFloorTile` must use the exclusion list above, not an inclusion list.
- OpenGL is unavailable in some remote sessions; use `--dump` to verify rendering.
