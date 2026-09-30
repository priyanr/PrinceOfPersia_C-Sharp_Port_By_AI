# Changelog

## 2026-09-30 (later still) — Tiles redrawn over the kid: climbing goes behind the floor edge

Reported by the user: climbing up onto a ledge, the kid was drawn over the floor slab
instead of behind its front edge. The original redraws parts of the tiles the kid
overlaps on top of him (seg008 redraw_needed_tiles), and we didn't.

- **draw_floor_overlay**: while climbing (frames 137..144) the floor he climbs onto is
  redrawn over him (`floor_left_overlay` + the floor's bottom piece), when the tile to
  its left is open.
- **draw_other_overlay**: while hanging, jumping or falling (and for falling floor
  pieces), a tile with an open space on its left is drawn again over him.
- Objects are filed under a tile (set_objtile_at_char; draw_mob) and drawn when the
  redraw pass reaches that tile, bottom row first, so the tiles marked by
  redraw_at_char2 after it cover him. The marks and the clip come from
  `KidEngine.KidDraw` (add_kid_to_objtable).
- `floor_left_overlay` is SDLPoP's table; not yet located in PRINCE.EXE.
- Not compared with DOSBox (no DOS files in this session).

## 2026-09-30 (later) — Spikes, chompers, palace graphics, sprite clipping, death wait

Ported in a cloud session with no DOS install and no DOSBox, so **nothing here has been
compared against the original yet**. Each routine is a line-by-line port of the SDLPoP
routine it names, and the build is clean. The checks to do are listed under "Verify" in
PENDING.md.

- **Spikes impale** (check_spiked / spiked, seg006/seg005): running or starting a
  running jump onto springing spikes, or landing a jump on any that are out. Landing on
  them from a fall does too (land's spike branch, including the original's jump into the
  alive branch). The spikes that got someone stay out for good (0xFF).
- **Chompers** chomp (start_chompers / animate_chomper, seg007). They start when the
  kid enters their room or row, lands, or starts a fall, each a few frames behind the
  last. A shut chomper (frame 2) in a column the kid overlaps halves him and gets blood
  on it (check_chomped_kid / chomped, seg004).
- **Mirror**: a running jump from right to left goes through it (is_obstacle's mirror
  branch). The shadow it releases is not ported (no other characters yet).
- **Palace levels (4-6, 10, 11, 14)** load VPALACE.DAT (load_lev_spr, by
  tbl_level_type, now `DosLevels.LevelType`) and use the room drawer's palace branches:
  solid-colour bricks per room (gen_palace_wall_colors) with divider decals, wall
  stripes, doortop arches, palace potions, the wider level-door wipe, no dungeon wall
  bodies. The level 4 mirror was drawn with dungeon art, which is probably why it looked
  black.
- **Wipes are layered as in draw_tables**: layer 0 before the back table, layer 1 after
  the characters. The start room's level-door wipe moved from inside the back list to
  layer 0.
- **Mono blits use the global VGA palette** (method_3_blit_mono paints `palette[color]`),
  not the image's own palette: chomper blood, potion bubbles, palace wall seams.
- **Sprite clipping** (clip_char, seg006): walls and doortops to the kid's right, the
  floor above when he climbs or jumps up, and the exit door when he climbs the stairs
  now cut his sprite off. The play area also clips at y 192 (reset_obj_clip). It runs at
  draw time on a copy of the kid, as add_kid_to_objtable does, and doesn't change the
  simulation.
- **Dying**: the level no longer restarts at once. The death plays out, and after the
  death frame has shown for 7 ticks (Kid.alive > 6; the original also waits for the
  death music) Shift or Enter restarts. "Press Button to Continue" is shown, except on
  level 0 and level 15. R still restarts at any time.

## 2026-09-30 — Animated tiles and falling floors ported (seg007); sword, potions, big pillars

`Sim/Hazards.cs` was a hand-written approximation (its own gate speeds, loose-floor timer
and a gate height stored divided by 4). It is replaced by a port of SDLPoP seg007:
animated tiles ("trobs") and falling floor pieces ("mobs"), with modifiers in the
original units.

- **Loose floors** shake when something lands or knocks nearby (check_knock / do_knock),
  and one being stood on comes away after 11 ticks and **falls** as a piece, room by room.
  It shatters into rubble, knocks loose floors it hits on the way down, presses plates it
  lands on, and hurts or knocks down a kid it lands on (fell_on_your_head). The demo's
  loose floors fall exactly where they do in DOSBox.
- **Exit door**: an opener raises it one step a tick (animate_leveldoor). The kid can
  only go up the stairs once it is fully open (was: as soon as the plate was pressed,
  with the door never drawn open). The start room's door starts open and slams shut.
- **Gates** open, hold and close as in animate_door; closers slam them in six speeds.
  Plates stay down 5 ticks after the last press, and a dead kid on a plate jams it
  (died_on_button).
- **Sword and potions**: Shift with the item in front (standing or crouched) crouches
  and picks it up (check_get_item / get_item / proc_get_object). The sword sets
  `HasSword`. Potions heal, add a hit point, hurt, slow the fall (feather) or flip the
  screen. Pickups flash the background, and so does losing a hit point.
- **Potions are drawn** (their type is now shifted into bits 3-7 at level load, as
  load_alter_mod does; before, none showed).
- **Big pillars, spikes, chompers, and the dotted floor piece in the demo** are drawn.
  The original loads those images from VDUNGEON resource `1200 + index`
  (load_more_opt_graf), not `200 + index`, so they were missing. Level 2's first room
  was the visible case.
- **Spikes spring out** when the kid is over them or above them (check_spike_below).
  They don't hurt yet.
- **Right Shift** now works as the action key (only Left Shift and Space did).
- `Level.Reset` also restores the plate timers.
- Tick order is now play_frame's: falling pieces, animated tiles, then the kid (whose
  frame ends with check_press, check_spike_below, check_knock), then the room exit.
- Demo check: the kid's trace is unchanged over 150 ticks. Pixels differing from the
  DOSBox recording dropped from ~1200 to ~110 per frame in room 1 and from ~2400 to
  ~1500 in room 2 (mostly one tile, see PENDING.md).

## 2026-09-30 — Kid control, physics and collision ported from SDLPoP; demo-verified

The kid's x position, anchoring, foot column and wall contact were each wrong in small
ways, because `KidControl`/`Physics` were hand-written approximations. They are replaced
by `Sim/KidEngine*.cs`, a routine-by-routine port of SDLPoP (seg002/004/005/006) using the
tables from PRINCE.EXE:

- **Position and drawing:** x is the character's front edge. The column under his weight
  comes from the frame's foot offset (determine_col). The sprite is placed with
  load_frame_to_obj (`2*(x+dx)-116`, scaled 320/280). The level start is block edge + 14,
  with the kid turning into the start direction (level 1 falls in, facing right, and lands
  under the first torch).
- **Walls:** collision edges from the sprite's width (set_char_collision), wall edges per
  tile type, bumps detected as edges crossed since last frame (check_collisions /
  check_bumped), soft and hard bumps, bump-falls, gates pushing the kid, and pushing a
  kid out of a wall (in_wall). Beyond the level's edge is wall.
- **Moves:** careful steps now walk exactly up to the edge or wall (stepfwd 1..14, then
  test-foot, then off the edge). Starting a run next to a wall takes a step instead. Jumping
  up aligns with the ledge (jumphang Med/Long, jump back under a ledge above). Running
  jumps align the take-off with the edge. Down steps off an edge in front or climbs down
  one behind. Climbing up is blocked by mirrors, chompers and low gates.
- **Falls:** grabbing a ledge while falling with Shift held (check_grab, FALLON).
  Start-fall picks the sequence and bumps out of walls. Landing picks soft, medium or
  crushed from the fall speed.
- **Controls:** keys latch as in the original, so a held key doesn't repeat a move.
- **Rooms:** the room changes when the sprite's edge leaves the screen (leave_room), not
  when x crosses it. Dropping out of the level's bottom kills.
- **Exit:** press Up at the open exit door to climb the stairs (was: standing on it).
- `check_press` and loose floors look across room edges.
- Headless: `POP_DEMO=1 POPGame --dump out 0 ".150"` replays the attract-mode demo. It
  matches a DOSBox recording of the original pixel for pixel on the kid.

## 2026-09-29 — Sequences read from the unpacked EXE; plates only pressed underfoot

**Garbled sequences.** `DosTables` read the frame and sequence tables straight out of the
PRINCE.EXE file, but the file is EXEPACK-compressed. Medland's 29 x frame 109 came out as
the garbage frames 218, 7, 178, 0, 176, and every sequence after it (hardland, bump,
bumpfall, the fighting sequences) was read 22 bytes off. The tables now come from
`ExePack.Unpack` (frames `0x1BCC5`, bytecode `0x1ACE0`, index `0x1C568`). `Validate()`
checks medland's 109 run. The level 1 start now plays 108, holds 109, then 110-119.

**Door opening while the kid falls into the pit** (reported playing level 1 room 6). A
standing jump that came up short dropped the kid into the gap. While falling, the kid drifted over the
pressure plate beside it, and `Hazards` pressed the tile under the kid on every tick, so the
door opened mid-fall. The press is now a port of CHECKPRESS (CTRL.S:1939):
- only stand, runjump, turn or bumped, and only on frames with `fcheckmark`;
- hanging and climbing frames press the tile above;
- frame 79 (jumping up to touch the ceiling) breaks a loose floor above;
- only opener (15) and closer (6) plates count, not the already-down plate (5).

Whether that short jump should fall at all still depends on the foot column (PENDING).

## 2026-09-29 — Jumps clear gaps: the floor check follows the frame's `fcheckmark`

Every standing or running jump over a hole used to drop the kid into it. `Physics`
checked for floor under the kid on every tick while standing, running or jumping, so the
first airborne frame over the gap started a fall. The original's ONGROUND (CTRL.S:309)
tests only frames whose flags carry `fcheckmark` (0x40). The airborne frames of standjump
(20-25) and runjump (39-43) don't have it, so the kid sails over the gap. Found by
recording DOSBox while the user jumped the hole in level 1 room 6.

- `Physics.CheckFloor` now follows CHECKFLOOR's per-action branching: hanging never
  checks, and a bumped character checks only on frame 109 (crouched) or 185 (dead).
- `StartFall` picks the fall sequence from the frame the kid dropped out of, as STARTFALL
  does: stepfall, stepfall2, jumpfall, rjumpfall, hangdrop (+5 x) and fightfall.
  Previously every fall was stepfall.
- Headless test hook: `POP_START=room,block,face` starts `--dump` with the kid standing
  anywhere, e.g. `POP_START=6,4,-1 POPGame --dump out 1 ".2 LU2 .30"`.
- Not ported yet: FALLON (grabbing a ledge while falling, InMidair frames 102-105), and
  STARTFALL's bump-out checks after the first frame of a fall.

## 2026-09-25 — Correction: the Codex model was not GPT-5.6

The March 2026 Codex session was recorded as "GPT 5.6 terra". GPT-5.6 was only released
on 2026-07-09 (limited preview from 2026-06-26). The most likely model is GPT-5.4,
released for Codex on 2026-03-05; the exact model was not recorded.
`SESSION_HISTORY.md` and the Codex commit message now say so. The commit message was
reworded by rewriting history, so the published history was replaced (force push).

## 2026-09-25 — Prepared for publishing: GPL-3.0, README, notices

- **License: GPL-3.0-or-later** (`LICENSE`, the official text from gnu.org).
  `Rendering/DosRoomDrawer.cs` is a C# port of SDLPoP's room-drawing code, which is
  GPL-3.0-or-later, so the project takes the same license. That file now carries a notice
  saying where it comes from and what was changed.
- **`README.md`:** what the project is, its status, requirements ("bring your own DOS
  copy"), how to run it, credits (Jordan Mechner, SDLPoP / David Nagy, Fabien Sanglard),
  and a non-affiliation disclaimer (Prince of Persia is a Ubisoft trademark).
- **Wording:** "nothing copyrighted lives in this repo" was inaccurate, since code under the
  GPL is still copyrighted. CLAUDE.md and `DosGame.cs` now say no *game data* is included.
- **History rewritten before publishing:** every commit now uses a GitHub no-reply email,
  and a local user path in `SESSION_HISTORY.md` became `~`.

## 2026-09-25 — Kid sprite anchoring reverted; line endings restored

Compared against the backup taken before the 2026-09-24 session
(`D:\temp\popsepbeforeop5`), with identical input scripts run through both builds:

- **Movement is unchanged.** 8 scripts, 713 ticks: route, running jump, jump-up, careful
  steps, crouch, walking left, and level 2. Room, x, y, row, frame, action and sequence
  are identical on every tick.
- **The regression was drawing only.** The 2026-09-24 kid anchoring (front edge + `x+7`)
  matched the original facing left but pushed the kid ~7px into walls facing right,
  where the wall's front layer hid him. Horizontal placement is back to centred on the
  sim's x. The verified vertical fix (`y - h + 1`) stays. See CLAUDE.md "Characters" for
  what has to be ported before the original's anchoring can be used.
- **Line endings:** the 2026-09-24 edits had converted `Sim/Hazards.cs`,
  `Sim/Simulation.cs` and `Dos/DosLevels.cs` from LF to CRLF. They are LF again.
- Recovered the Feb 2026 history into `SESSION_HISTORY.md` from `~/.claude/history.jsonl`
  and the backup at `D:\temp\popjuly\POPCS`. That backup still holds the original
  `POPCS/` console project, which is missing from this tree.

## 2026-09-24 — Rooms drawn by the original's own routine

The room renderer was rewritten. Tile placement is no longer tuned by hand from screenshots. It is now a
port of the DOS game's room-drawing routine (DRAW_ROOM / `draw_tile_*`, as reconstructed in
SDLPoP's `src/seg008.c`), driven by tables read out of the player's own `PRINCE.EXE`.

### Result
Checked by diffing full frames against DOSBox captures at native 320x200:

| Room | Differing pixels before | After |
|------|------------------------:|------:|
| Level 1, room 1 (start) | 8,429 | 2 (torch flame edge) |
| Level 1, room 2 | — | 0 outside state differences* |
| Level 3, start room (exit door) | — | 0 outside state differences* |

\* The remaining differences are torch flame animation frames, the kid's position, and a
loose floor that had already fallen in the capture.

### Added
- **`POPGame/Dos/ExePack.cs`**: an unpacker for Microsoft EXEPACK. `PRINCE.EXE` *is*
  packed, contrary to earlier notes. The frame and sequence tables only read correctly
  because they happen to sit in a stretch the packer left verbatim.
- **`POPGame/Dos/DosDrawTables.cs`**: reads the room-drawing tables from the unpacked
  EXE. These are `tile_table` (31 x 12), the loose/spike/chomper/gate frame arrays,
  `blueline_fram*`, `wall_fram_main/bottom`, `potion_fram_bubb`, and the wall-mark
  positions. One anchor locates the whole block, and `Validate()` checks a second value
  in each array.
- **`POPGame/Rendering/DosRoomDrawer.cs`**: the port of the drawing routine. It outputs
  a back layer (drawn behind the characters) and a front layer (drawn over them). It covers:
  - the tile halves: base, right, top-right, bottom, fore
  - the neighbour-dependent wall bodies
  - the seeded brick pattern (Microsoft C's random-number generator, seeded with
    `room + row*10 + col`)
  - two-layer gates, the level door, spikes, loose floors, chomper, potions, sword
    and torches
  - the bottom row of the room above, which forms the ceiling
- **`BlitMode`** in `Framebuffer`: `Trans`, `NoTrans`, `Or`, `Black` and `Mono`, matching
  the blitters the original uses.
- **`CHANGELOG.md`**: this file.

### Changed
- **`DosRenderer`** now only turns the drawer's output into pixels, in the original's
  order: back layer, kid, front layer.
- **Kid sprite placement** follows the original's LOAD_FRAME_TO_OBJ:
  - x is computed in the original's 280-wide space and scaled by 320/280
  - the sprite's left edge is at x when facing left, and its right edge when facing right
  - y is the sprite's bottom row (`y - h + 1`)

  Before, the sprite was centred and drawn 1px too high, leaving the kid about 27px left
  of where the original draws him.
- **DOS gates** (`DosLevels.NormaliseGates`): in the level data, a gate spec of `1` means
  open and anything else means shut. It is converted to the sim's 0..47 height on load.
  Before, the raw value was read as a height.
- **Closer plates** (tile 6) now slam their gates shut. Before, they opened them.
- **Level 1 start event** (`Simulation.StartEvents`): the closer in room 5 (row 0, col 2)
  is pressed as the level begins, so the open gate beside room 1 slams shut, as the
  original's DO_STARTPOS does.

### Removed
- **`POPGame/Rendering/DosTileArt.cs`**: the hand-placed piece table.
- **The palette 14/15 "stencil" mask.** Those entries are real colours: the teal and
  green of the exit-door frame.
- **`Level.GetTileModifier`**: the BLUETYPE top bits don't affect drawing. The drawing
  modifier is BLUESPEC.

### Documentation corrections (CLAUDE.md)
- `PRINCE.EXE` is EXEPACK-packed; the existing offsets are file offsets.
- The DOSBox crop is `(3,32)-(643,432)`, not `(3,30)-(643,430)`. The old crop was one
  game row too high, and it was the source of a spurious "+1 row" offset.
- The "Tile rendering" section is rewritten: tables, geometry and draw order, how the
  original rewrites modifiers on load, seeded masonry, gates, and character anchoring.
  Earlier "unexplained" observations are now explained:
  - wall bodies 364/366/368/370 depend on which neighbours are walls
  - the crosses come from a floor's modifier
  - decoration sits one cell right because each tile draws its right half there
- Added `megahit` / Shift+L for reaching other levels in DOSBox.

### Known gaps
- Palace levels (4, 5, 6, 10, 11, 14) still render with dungeon art. The palace drawing
  path and its graphics file are not implemented.
- Spikes, slicers, mirrors, potions and an opening exit door come straight from the
  tables but have not yet been diffed against a capture.
- The kid lands about 4px left of the original after the level 1 drop, in a slightly
  different pose. This is simulation logic, not drawing.
- Torches cycle their flame by tick. The original picks flame frames with its random
  generator.
