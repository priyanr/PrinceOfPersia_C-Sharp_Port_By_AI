# POPCS — Prince of Persia (C# remake, DOS-accurate)

## Project Overview
A C# .NET 10 remake of Prince of Persia that runs off the **original DOS install**
rather than hand-transcribed data. Nothing copyrighted lives in this repo.

- **POPGame** (`POPGame/`) — the game (Raylib window, or headless PNG dump)
- **LevelEditor** (`LevelEditor/`) — read-only level viewer (legacy, Apple II data)
- **tools/DatDump** — dumps any DOS `.DAT` to PNGs + labelled contact sheets
- Apple II 6502 reference source: `originalcode/Prince-of-Persia-Apple-II-master/01 POP Source/Source/`

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
`PRINCE.EXE` is **not packed**, so the tables are readable in place. Offsets were
found by matching byte patterns from the Apple II source and verified against two
independent anchors each (`Dos/DosTables.cs`, `Validate()` re-checks at startup):

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
  Dos/DosLevels.cs          levels out of LEVELS.DAT
  Dos/PngWriter.cs          dependency-free RGBA PNG encoder
  Sim/Coord.cs              the original coordinate system
  Sim/CharState.cs          character state + CharAction + Seq ids
  Sim/SeqRunner.cs          ANIMCHAR: the sequence-table interpreter (COLL.S:994)
  Sim/RoomView.cs           tile queries with MAP neighbour resolution
  Sim/KidControl.cs         PLAYERCTRL: input -> which sequence to jump to
  Sim/Physics.cs            gravity, landing, walls, room transitions
  Sim/Simulation.cs         per-tick orchestration
  Rendering/Framebuffer.cs  320x200 RGBA buffer + indexed blitter
  Rendering/DosTileArt.cs   which DOS images compose each tile  <-- main tuning knob
  Rendering/DosRenderer.cs  room + character into the framebuffer
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
`DosTileArt.cs` places pieces by **raw VDUNGEON.DAT resource id**, relative to the
cell's top-left corner (`cellX = 32*col`, `cellY = Coord.BlockTop(row)`).
Every offset below was **read back off the real game**, not guessed: a DOSBox frame is
captured at native 320x200 and each decoded resource is template-matched against it, so
a hit is an exact pixel-for-pixel placement. `DosRenderer` splits pieces into a back
layer and a front layer (pillar tops, torch flames) that draws over the characters.

| Tile | Pieces (id @ x,y within the cell) |
|------|-----------------------------------|
| solid wall (`Block`) | body `364/366/368/370` @ 0,+1 · a seam `371`/`372` on the lower two courses (@ +22, +43) · cap `369` @ 0,+61 |
| any floor-ish tile | face @ 0,+48 · cap `243` @ 0,+61 |
| `Posts` | `292` @ 0,+1 · `295` @ +8,+1 · flank `293` @ **+32**,+1 · cap `243` @ 0,+61 |
| `Gate` | threshold `362` @ +7,+52 · lattice `260-h`/`252`/`251` @ +7, hung by BLUESPEC (see below) |
| `Rubble` | floor + `300` @ 0,+52 |
| `Torch` | floor + sconce `346` @ **+32**,+21 + flame (PRINCE.DAT 150 bank) @ **+40**,+3 |
| back wall | cross `244` @ 0,+14, on open cells only |
| ceiling | cap `243` @ 32*col, `BlockTop(-1)+61` (== y 1) across every column |
| `Spikes` | floor + `1305`/`1304`/`1303`/`1301` by BLUESPEC, foot on the surface |
| `Flask` / `Sword` | floor + PRINCE.DAT 150-bank index 12 (4-frame loop) / 11 |
| `Exit` | floor + stairs `344` (41x45), spanning the Exit/Exit2 pair |

Everything above the `Spikes` row is verified against a capture. The last three rows are
reasoned placements — those tiles only occur in rooms of level 1 that cannot be reached
from the start (room 5 is behind a shut gate, room 9 past the guard), so nothing has been
matched against the original yet. Pressure plates still draw as bare floor.

Four things are not what you would guess, and cost most of the time to find:

1. **A floor has no body.** Only the perspective top face at +48 and the 3px cap at
   +61; the rest of the cell is open and shows the black backdrop. Only a *wall* fills
   its whole cell (60px body at +1 plus the cap), which lands exactly on the next row's
   cell top.
2. **The floor top face is flat for a continuing run and a wedge at the run's left end.**
   The wedge is `348` (32x14, sloping up to the right). There is no standalone flat
   slab resource — it is rows **47..59 of image 237**, whose lower half *is* the floor
   slab. Drawing the wedge on every cell gives a sawtooth (an easy wrong turn: the wedge
   template-matches on flat cells too, because it is a subset of the slab).
3. **Wall-mounted decoration is drawn one cell to the RIGHT of the tile that owns it.**
   A torch at col 0 puts its bracket at x=32. Confirmed on both torches of room 1 *and*
   both of room 2, against level data that is otherwise column-exact.
4. **Palette indices 14 and 15 are stencil markers, not colours.** They are the only
   saturated entries (a green and a teal) in an otherwise entirely blue-grey palette,
   and several images — the gate rail in 237, the exit stairs 344, the arch pieces
   238/240 — carry a mask strip painted in them. Blit them and you get bright green bars
   across the art. `Framebuffer.Blit` takes a `skipMask`; `DosRenderer.EnvSkipMask`
   passes 0, 14 and 15 for all background art.

The room also draws the **left neighbour's last column at x = -7**, which is the wall
sliver visible in the leftmost 25 pixels.

### Masonry: what is rule and what is the original's RNG
Solved cell by cell against captures of rooms 1, 2 and 3:

* **BLUETYPE modifier 0 always draws body 364** (verified on 40+ cells, no exceptions).
  A non-zero modifier draws 364, 366 or 370; 368 never turned up. *Which* of the three
  is the original's own randomness and is not reproduced.
* **Only the lower two courses carry a vertical seam** (`371` 9px / `372` 8px); the top
  course never does. The middle course breaks 8..12 px into the cell, the bottom one
  0..4 px in.
* Those seams sit on **one continuous grid a shade under 32px apart**, so their offset
  inside the cell walks down about half a pixel per column and wraps: reading room 3's
  bottom wall left to right gives 4 4 3 3 2 1 0 0 0. `DosTileArt.SeamX` reproduces the
  drift; the per-row starting phase is hashed, not derived.
* The back-wall cross `244` is rare and its placement is unexplained — room 1 has three,
  room 2 none, with no correlation to modifier or BLUESPEC that fits both.

### Gate
The portcullis is built, not clipped: threshold `362` @ +7,+52, then the lattice hung
from the top of the doorway — a short course of the leftover height (`260-h` for h=1..7),
then 8px courses (`252`), then the weighted end piece (`251`). The drop is
`GateOpen + 1 - spec`, so a shut gate (spec 0) hangs 48px. Verified byte-exact against
the shut gate on the left edge of room 1: a 7px course at +1, five full courses, end
piece at +48.

Note this only lines up if the sim leaves an untriggered gate at its authored height.
`Sim/Hazards.TickGates` used to wind *every* gate down towards 0 each tick; it now only
closes gates that were actually opened.

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
To place a piece, capture the room in DOSBox, crop the window to native 320x200
(client area is 640x400: crop `(3,30)`-`(643,430)` of a default-size window, then halve)
and template-match every dumped PNG against it.

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

## Common pitfalls
- X is in 14-per-block units, **not** pixels; only Y is in pixels.
- `Face` is -1/0, not -1/+1. Sprites face left natively; mirror when facing right.
- Frame table `image` is 0-based; `DosImageBank` is addressed by resource id, so the
  kid sprite is `401 + frame.image`.
- A DAT can declare several palette groups and also carry resources outside them —
  never assume ids are one contiguous run from the palette resource.
- `IsFloorTile` must use the exclusion list above, not an inclusion list.
- OpenGL is unavailable in some remote sessions; use `--dump` to verify rendering.
