# POPCS — Pending work

Known differences from the original and missing features, roughly in priority order.
Remove an item when it is done and log it in `CHANGELOG.md`.

## Movement / physics
- **Sprite clipping** (clip_char, seg006): the original clips the kid's sprite at walls
  and doortops (e.g. against a wall on his right, at the wall tile's left edge) and at
  the floor above when climbing. We rely on the room drawer's foreground layer instead.
  Compare facing right into a wall in DOSBox.
- Level 1's first crouch should last until the presentation music ends
  (need_level1_music); with no sound we stand up straight away.
- Mirror run-jump-through (is_obstacle) and chompers as obstacles are ported only as far
  as the tile checks; mirrors never break.
- Level 12 phantom bridge (ONGROUND creates floor on the fly).
- **Grabbing after a short jump**: works headless (level 1 room 12, standing jump from
  col 7 with Shift pressed after take-off → hangs on col 3). The user saw it fail in the
  live game, probably because Right Shift wasn't mapped (fixed 2026-09-30). If it still
  fails, record where and compare in DOSBox.

## Drawing
- **Palace levels (4-6, 10-11, 14) use the dungeon graphics.** They should load
  VPALACE.DAT (tbl_level_type) and the palace branches of the room drawer. Reported by
  the user on level 4.
- **Level 4 mirror**: drawn black, and the kid can't run-jump through it (is_obstacle /
  jumped_through_mirror, seg004). The user got stuck there.
- **Demo room 2, col 9 row 0** (a gate, tile byte 0x24, modifier 2): DOSBox draws a
  stone pillar with a dotted strip (images 92/93, the pillar tile) there; we draw the
  gate's white front post (image 49). It was already like this before the trobs port.
  ~1400 of the ~1500 pixels still differing in the demo's room 2.
- The pickup flash lasts the whole tick; the original shows it for 2/60 s of each tick
  (do_flash), so it flickers.

## Game features not in the DOS sim yet
- Guards and sword fighting (level 1 guard in room 3). The sword can be picked up
  (`HasSword`) but not drawn or used.
- Spikes impaling (check_spiked / spiked) and landing on spikes; slicers and chompers as
  hazards. Spikes spring out (check_spike_below) but don't hurt.
- Torch flames, potion bubbles and the sword glint animate from the tick; the original
  runs them as trobs with prandom (animate_torch / animate_potion / animate_sword).
- Sounds (none at all), so the feather fall lasts a fixed 225 ticks rather than until
  its sound ends.
- Dying restarts the level at once (`DosGameLoop`); the original plays the death and
  waits for a key ("Press Button to Continue").

## Housekeeping
- `AGENTS.md` still describes the legacy Apple II engine.
- Check whether the Apple II-era files (`Engine/GameLoop.cs`, `Rendering/Raylib*`,
  `TileAtlas`, `CharAtlas`, `World/HazardSystem.cs`, ...) are still referenced; delete
  them if not.
