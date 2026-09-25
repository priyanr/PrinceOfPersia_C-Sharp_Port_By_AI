# Changelog

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
