# POPCS — Pending work

Known differences from the original and missing features, roughly in priority order.
Remove an item when it is done and log it in `CHANGELOG.md`.

## Movement / physics
- **Sprite clipping** (clip_char, seg006): the original clips the kid's sprite at walls
  and doortops (e.g. against a wall on his right, at the wall tile's left edge) and at
  the floor above when climbing. We rely on the room drawer's foreground layer instead.
  Compare facing right into a wall in DOSBox.
- **Exit door timing**: `leveldoor_open` should become true once the door has finished
  rising; ours is true as soon as the opener is pressed.
- Level 1's first crouch should last until the presentation music ends
  (need_level1_music); with no sound we stand up straight away.
- Level 0 (demo) room 1 draws a dotted floor piece at the bottom left (row 2) that ours
  doesn't. Seen in the demo comparison.
- Mirror run-jump-through (is_obstacle) and chompers as obstacles are ported only as far
  as the tile checks; mirrors never break.
- **JAMPP** (MOVER.S): a dead kid on a plate jams it (closer becomes dpressplate,
  opener becomes floor). `Hazards.CheckPress` doesn't press anything for a dead kid's weight yet.
- Level 12 phantom bridge (ONGROUND creates floor on the fly).

## Game features not in the DOS sim yet
- Guards and sword fighting (level 1 guard in room 3); sword pickup.
- Potions / drinking (`fx.DrankPotion` is raised but nothing uses it).
- Spikes impaling, slicers, chompers as hazards (drawn, not lethal).
- Torch flame phase differs from the original (cosmetic).

## Housekeeping
- `AGENTS.md` still describes the legacy Apple II engine.
- Check whether the Apple II-era files (`Engine/GameLoop.cs`, `Rendering/Raylib*`,
  `TileAtlas`, `CharAtlas`, ...) are still referenced; delete them if not.
