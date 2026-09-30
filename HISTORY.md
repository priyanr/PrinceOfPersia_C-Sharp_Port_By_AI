# POPCS — Findings and decisions

The detailed "how we know" behind the rules in `CLAUDE.md`: what was found in the
original, how it was verified, and what was tried and abandoned. Per-session logs
(models, prompts, files) are in `SESSION_HISTORY.md`; dated changes are in `CHANGELOG.md`.
Open work is in `PENDING.md`.

## Where the authentic data comes from
`PRINCE.EXE` is packed with Microsoft EXEPACK (the stub at CS:0 has the "RB" signature
and "Packed file is corrupt"). EXEPACK only squeezes runs of repeated bytes, so most of
the image survives verbatim in the file, and that is a trap: the tables *look* right when
read in place, but every run is replaced by a fill/copy command and everything after it
shifts. The sequence tables were read from the file until 2026-09-29. Medland's 29 x frame
109 appeared as frames `218,7,178` (copy 0x7DA, cmd B2) and `109,29,0,176` (fill 109 x 29,
cmd B0), and every sequence after 0x205A (hardland, bump, bumpfall, all fighting) was
read 22 bytes off. **All tables are read from the unpacked image** (`Dos/ExePack.Unpack`):
frames `0x1BCC5`, sequence bytecode `0x1ACE0`, sequence index `0x1C568`. The old file
offsets were `0x1B9AA`, `0x1A8ED` and `0x1C175`. The frame table has no runs, so it was
byte-identical either way. Offsets were
found by matching byte patterns from the Apple II source and verified against two
independent anchors each (`Dos/DosTables.cs`; `Validate()` re-checks at startup).

- Frame table `dx`/`dy`/`flags` are byte-identical to Apple II `FRAMEDEF.S`; only
  `image`/`sword` differ (DOS uses flat 0-based chtab indices). `image == 255` = blank.
- The sequence bytecode layout matches `SEQTABLE.S` exactly, so `SEQDATA.S` is a valid key
  to the sequence ids (mirrored in `Sim/CharState.cs` as `Seq.*`).
- `LEVELS.DAT` resource `2000+N` is level N as the same 2304-byte blob the Apple II
  `LEVELn` files use (verified 99-100% byte identical).

## DAT palettes
| DAT | Palette groups | Notes |
|-----|----------------|-------|
| `VDUNGEON.DAT` | 200 (151 images), 360 (17) | 360 is the wall-brick bank; loose resources at 1030+ (pillars, spikes, debris) sit outside any declared run |
| `KID.DAT` | 400 (219) | `sprite id = 401 + frame.image` |
| `PRINCE.DAT` | 150 (23), 700 (34) | 150 is the flame/sword/potion bank with a *fire* palette: images 1-9 torch flame loop, 12-22 potions, 551+ swords |
| `PV.DAT` | 800, 850, 900, 950, 980 | princess / vizier / mouse |
| `GUARD.DAT` | none | guards borrow the level palette, which is why they recolour per level |

The dungeon palette is entirely blue-greys and teals, with no orange, so anything that
looks like fire comes from the `PRINCE.DAT` 150 bank.

**The VGA DAC replicates, it does not scale.** 6-bit -> 8-bit is `(v << 2) | (v >> 4)`,
not `v * 255 / 63`. The old truncating version was off by one on several entries, which
silently broke every exact template match against DOSBox captures (`DatImage.Scale6`).

**Palette indices 14 and 15 are real colours**, the teal/green of the exit-door frame
(confirmed against the level 3 start room). An earlier theory that they were "stencil
markers" came from drawing the wrong pieces (`237` rows used as a floor slab).

## Room rendering: port, don't tune
Rooms were first built by template-matching tiles off DOSBox screenshots one piece at a
time. That got the verified pieces right and everything else wrong (brick pattern, gate
layers, any tile not in a captured room). It was replaced by `Rendering/DosRoomDrawer.cs`,
a straight port of DRAW_ROOM and the `draw_tile_*` family (as reconstructed in SDLPoP's
`src/seg008.c`), driven by tables read out of PRINCE.EXE (`Dos/DosDrawTables.cs`).
Against DOSBox captures, level 1 rooms 1 and 2 and the level 3 start room (exit door
included) match pixel for pixel; later also rooms 6 and 8. Only torch flame phase and
simulation state differ.

### The tables
`tile_table` is 31 rows x 12 bytes: `base_id, floor_left, base_y, right_id, floor_right,
right_y, stripe_id, topright_id, bottom_id, fore_id, fore_x, fore_y`. Located by its first
two rows (12 zeros, then `41,1,0,42,1,2,145,0,43,0,0,0`); every small frame array follows
at a fixed offset (loose, chomper, spikes, `door_fram_top/slice`, `blueline_fram1/_y/3`,
`wall_fram_bottom/main`, `potion_fram_bubb`, the wall-mark `LPOS`/`RPOS` words).
`door_fram_slice` shares its first byte with the end of `door_fram_top` in the EXE.

### Geometry and draw order
- `xh = 4*col` (8px units), `x = xh*8 + xl`; `draw_bottom_y = 63*row + 65`,
  `draw_main_y = bottom - 3`. Every blit is bottom-anchored: top = `y - h + 1`. An
  earlier "+1 row" calibration came from a bad DOSBox crop and was removed.
- Rows 2 -> 0, columns 0 -> 9. Each cell runs `floorright, anim_topright, right,
  anim_right, bottom, loose, base, anim, fore`. Then the bottom row of the room above is
  drawn at `main_y=-1`, `bottom_y=2` (the ceiling); no room above = a row of floors.
- A tile is drawn in two halves: `base` in its own cell, `right_id` by the cell to its
  right, keyed on "what is my left neighbour". That is why the torch bracket, gate lattice
  and posts' flank sit one cell right. Column 0 draws the left room's column 9 right half.
  A missing neighbour room counts as a wall.
- Front layer (over the kid): wall bodies, seams/marks, pillar fronts, spike fronts,
  potion bubbles, and a gate the kid is standing under.

### Modifiers (BLUESPEC) and LOAD_ALTER_MOD
- Walls: bits 0-1 record which neighbours are walls (SWS=0, SWW=1, WWS=2, WWW=3); spec 1
  sets bit 7 ("no blue"). `wall_fram_main[conn]` = 8/10/6/4 -> bodies 368/370/366/364.
- Gates: level data spec 1 = open, anything else = shut. `DosLevels.NormaliseGates`
  converts to the sim's 0..47 height; the drawer uses `height*4`.
- Floors: spec 1..3 draws `blueline_fram3` (244/245) at `main_y-20` in the cell to the
  right (the "crosses"). Empty tiles: spec 1..3 draws `blueline_fram1` (324/325/326).

### Masonry is seeded
`wall_pattern` reseeds the PRNG each call with `seed = room + row*10 + col`, advances once
and discards. `prandom(max)` is MSC's LCG: `seed = seed*214013 + 2531011; return
(seed>>16) % (max+1)`. Seams `371/372` (at `xh+1`+offset middle course, `xh`+offset
bottom), random block `373` and marks `374-377` come from it, so masonry is identical on
every visit.

### Gate
Drawn by the cell to its right: `gate_bottom_y = main_y - (height+1)`; 8px slices `252`
run upward from `gate_bottom_y-12` while above `bottom_y-62`; `door_fram_slice[k]` is the
partial top course; raised more than 12px -> `250` is the bottom piece, otherwise the
cell's own art is redrawn and `251` goes over it.

**Level 1 starts with the gate open.** DO_STARTPOS presses the closer in room 5 (row 0,
col 2), so the gate beside room 1 slams shut as the kid drops in. `Simulation.StartEvents`
does the same, and closer plates (tile 6) close gates instead of opening them.

## The kid engine is a port of SDLPoP (2026-09-30)
`Sim/KidEngine*.cs` ports SDLPoP's kid routines (seg002 exits and demo moves, seg004
collisions, seg005 control and landing, seg006 physics) with the tables from PRINCE.EXE.
The earlier hand-written control and physics fixed symptoms one at a time. The x
anchoring question could not be answered inside them, because three things only make sense
together:
- **x is the character's front edge.** Facing left the collision box is `[x, x+w/2]`,
  facing right `[x-w/2, x]` (set_char_collision, w = sprite width). The sprite is drawn
  from `obj_x = 2*(x+dx)-116`, scaled 320/280, starting there facing left and ending
  there facing right.
- **Characters stand 7 units in front of the tile plane** (`angle`, TILE_MIDX). Columns
  come from `x - 7` (get_tile_div_mod_m7) of the weight point `x + fwd(dx - foot)`, and
  wall edges sit 7 units into their tile (x_bump + 7).
- **Starts turn:** DO_STARTPOS puts the kid at block edge + 14 facing the *other* way
  and plays the turn; level 1 plays the fall instead (so the kid faces right there).
The 2026-09-24 `+7` attempt added one of these without the other two.

**Verified against the original:** the attract-mode demo (level 0, a fixed move table)
recorded in DOSBox matches `POP_DEMO=1 --dump out 0` pixel for pixel on the kid:
standing facing left and right, running, run-stop, the aligned running jump, and the
room change. Only torch flames and the (unported) spikes differ. The frame table in
SDLPoP's source is byte-identical to the one read from PRINCE.EXE.

Findings from the port:
- **Controls latch** (read_user_control): a press stays "held" until a move consumes it,
  which sets it to "ignore" until the key is released. Latching happens before the
  facing flip, so "forward" means left until user_control swaps it.
- **Unsigned compares matter:** do_fall, check_grab and bumped_floor compare y as
  unsigned words (Char.y is a byte), so a kid below the floor line counts as far above it.
- **Beyond the level's edge is wall** (get_tile on room 0), not space.
- **The exit needs Up** at the open door (up_pressed → stairs sequence 70, whose
  `nextlevel` opcode ends the level), and never in the level's start room.

## Animated tiles and falling floors are a port of seg007 (2026-09-30)
`Sim/Hazards.cs` ports SDLPoP's trobs (process_trobs / animate_*: buttons, gates, exit
door, loose floors, spikes) and mobs (do_mobs / move_loose / loose_land). It replaces a
hand-written version with its own gate speed, loose-floor timer and gate height / 4.
Findings:
- **Modifiers need load_alter_mod** at level load (`DosLevels.AlterModsAllrm`): gate
  1 -> 188 else 0, loose -> 0, potion `<<= 3`. Without the shift no potion was drawn
  (the drawer reads the type from bits 3-7).
- **The exit door opens gradually** (+1 a tick to 43) and `leveldoor_open` is set only
  at the top; the start room's door is set to 43 and slammed shut (find_start_level_door).
- **A loose floor falls as a mob** after counting 1..11 under the kid's weight
  (make_loose_fall(1) from check_press). Its modifier's bit 7 means "shaking" (a knock
  from a landing or a bump, 0x80..0x83, via the sequences' jarU/jarD). Tile bit 0x20
  marks a loose floor that never falls. The demo's two loose floors in room 2 fall in
  DOSBox exactly as ours do.
- **Optional graphics** (load_more_opt_graf, seg000): after loading the environment
  chtab, the original loads eight index ranges again from resource 1200 + index. In
  VDUNGEON those are the only copies of the big pillars (86-91), spikes (127-143),
  chompers (101-123), debris (30-31), etc., so ours drew nothing for them. This was the
  "black floor" in level 2's first room and the demo's missing dotted piece.
- **Right Shift** is an action key in the DOS game; the live loop only read Left Shift.

## Movement rules found in the source
- **Sequences chain themselves** through `goto`s in the bytecode, so control only picks an
  entry point: jumphangMed(8) -> hang(9) -> hangdrop(11)/climbup(10) -> stand(2);
  jumpup(14) -> hangdrop; fallhang(15) -> hang; climbdown(68) -> hang. `chy,193` in
  climbup is `-63` as a signed byte, exactly one block.
- **States are action + frame**, not action alone: standing is `Stand && Frame==15`,
  running is `RunJump && Frame in 1..14`. Softland and stoop park on frame 109; medland
  passes through it, so the crouch test also needs the sequence id.
- **Solid block (20) has no floor** (CMPSPACE, CTRLSUBS.S:1495). Leaving it out let
  characters stand on walls and climb where the original never allows.
- **Barrier waiver** (COLL.S:409, COLL.S:80): no barrier check while hanging (action 2/6),
  turning (7) or in climbup frames 135..148. Without it a kid who just climbed onto a
  ledge counts as inside the block and gets pushed back off.
- **CHECKLEDGE** (CTRLSUBS.S:1814): Up jumps for a ledge when the cell directly above has
  no floor and isn't a block (or a floorless panel facing right), and the cell above and
  one column ahead does have a floor (triggered loose disqualified; panelwf only facing
  right). Testing for floor directly overhead finds no ledge anywhere. DoJumpup
  (CTRL.S:1633) tries above-in-front, then above-behind, then plain jumpup. Tile lookups
  cross room edges via MAP, which is how `1 -> 2 -> 6 -> 8` on level 1 works.
- **Floor check follows `fcheckmark`** (2026-09-29, CTRL.S:309 ONGROUND): only frames
  with flag 0x40 test for floor underfoot. Before this, every jump over a hole fell in.
  Found by recording DOSBox (5 captures/s) while the user jumped the level 1 room 6 hole.

## Level 1 reference (verified against LEVELS.DAT)
- Start: room 1, block 0 (row 0 col 0), facing left. That cell is space, so the kid drops
  one row onto the torch tile.
- Room 1: row 0 `space space space floor floor floor floor floor block block`;
  row 1 `torch torch floor posts space block block block block block`;
  row 2 `block block block block rubble posts loose floor floor block`;
  neighbours left=5, right=0, above=0, below=2.
- Route: walk right along row 1, fall through the col-4 gap to row 2, stand on the loose
  floor at row 2 col 6 to drop into room 2, then right through room 3 (guard at row 1
  col 7) into room 9: opener plate at row 0 col 0, exit at row 1 cols 3-4.
- Branch: room 2 left edge facing left -> climb into room 6 -> jump the hole at row 0
  col 3 (spikes below) -> room 8.
- Guards: room 3 block 17, room 21 block 6.

## DOSBox comparison recipe
- The dosbox-control server listens on port 5000 (not 5164), and DOSBox only starts with
  `SDL_VIDEODRIVER=windib` in the launcher's environment (DirectX exits silently, code 0).
- Drive it from PowerShell (`ConvertTo-Json` + `Invoke-RestMethod`); bash/curl mangle the
  doubled backslashes `exePath` needs. `args = 'megahit'` enables Shift+L (skip level).
- The client area of a default window is 640x400 at `(3,32)-(643,432)`; halve it. The old
  `(3,30)` crop was one row too high (row 0 was window chrome 243,243,243).
- For movement, record continuously while the user plays rather than taking single shots;
  then identify rooms by diffing each capture against `POPGame --room` renders.
