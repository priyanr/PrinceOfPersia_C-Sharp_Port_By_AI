# POPCS — Pending work

Known differences from the original and missing features, roughly in priority order.
Remove an item when it is done and log it in `CHANGELOG.md`.

## Verify (ported 2026-09-30 without the DOS files or DOSBox)
These compile and follow SDLPoP line by line, but have never run against the real data.
Check each in DOSBox (or at least with `--dump`) and log what you find in HISTORY.md.
- **Palace levels**: `--room out.png 4 <room>` against DOSBox. Check the brick colours
  (wipes in wall palette 0x61..0x69), the seams (mono colour 6 of the global VGA
  palette), wall stripes (image 84 / `stripe_id`), doortop arches and palace potions.
  `DoortopFramTop` / `DoortopFramBot` in `DosRoomDrawer` come from SDLPoP; find them in
  PRINCE.EXE and read them from there like the other tables.
- **Level 4 mirror**: drawn right now that VPALACE is loaded? A running jump to the
  left should go through it.
- **Mono blits** now use the global VGA palette: compare chomper blood and potion
  bubble colours (they used the image's palette before).
- **Spikes**: run onto springing spikes, land a jump on them, drop onto them (level 1/2).
- **Chompers**: timing of a row of them, the kill, blood (level 3+).
- **Sprite clipping**: facing right into a wall, climbing up under a floor, climbing the
  exit stairs. The demo must still match; the level-door wipe moved to layer 0.
- **Death**: the impale/halve/fall animation plays out and Shift restarts.
- Climbing onto a ledge now goes behind the floor edge (confirmed by the user in the
  live game, 2026-09-30). Still check hanging and jumping past ledge edges
  (draw_other_overlay), and that the demo still matches (falls now redraw tiles over him).

## Movement / physics
- Level 1's first crouch should last until the presentation music ends
  (need_level1_music); with no sound we stand up straight away.
- Jumping through the mirror should release the kid's shadow (jump_through_mirror,
  seg003); needs other characters first. `KidEngine.JumpedThroughMirror` is set.
- Level 12 phantom bridge (ONGROUND creates floor on the fly).
- **Grabbing after a short jump**: works headless (level 1 room 12, standing jump from
  col 7 with Shift pressed after take-off → hangs on col 3). The user saw it fail in the
  live game, probably because Right Shift wasn't mapped (fixed 2026-09-30). If it still
  fails, record where and compare in DOSBox.

## Drawing
- **Demo room 2, col 9 row 0** (a gate, tile byte 0x24, modifier 2): DOSBox draws a
  stone pillar with a dotted strip (images 92/93, the pillar tile) there; we draw the
  gate's white front post (image 49). It was already like this before the trobs port.
  ~1400 of the ~1500 pixels still differing in the demo's room 2.
- The pickup flash lasts the whole tick; the original shows it for 2/60 s of each tick
  (do_flash), so it flickers.

## Game features not in the DOS sim yet
- Guards and sword fighting (level 1 guard in room 3). The sword can be picked up
  (`HasSword`) but not drawn or used.
- Torch flames, potion bubbles and the sword glint animate from the tick; the original
  runs them as trobs with prandom (animate_torch / animate_potion / animate_sword).
- Sounds (none at all), so the feather fall lasts a fixed 225 ticks rather than until
  its sound ends.
- "Press Button to Continue" is drawn with Raylib's font over the window, not in the
  original's status line, and there is no death music (so no wait for it).
- Entering a room doesn't run anim_tile_modif / check_fall_flo (check_the_end); only
  the chompers start.

## Housekeeping
- `AGENTS.md` still describes the legacy Apple II engine.
- The Apple II-era files are no longer referenced: POPGame builds with all of these
  removed (checked 2026-09-30): `Characters/*`, `Data/Apple2Image.cs`,
  `Dos/DosSpriteSheet.cs`, `Engine/Constants.cs`, `Engine/GameLoop.cs`,
  `Engine/GameState.cs`, `Input/InputHandler.cs`, `Rendering/{CharAtlas, CharSprites,
  EgaPalette, RaylibHud, RaylibRenderer, SpriteManager, TileAtlas, TileSprites,
  TileVisuals}.cs`, `World/*`. They were kept pending the owner's go-ahead to delete.
