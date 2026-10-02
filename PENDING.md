# POPCS — Pending work

Known differences from the original and missing features, roughly in priority order.
Remove an item when it is done and log it in `CHANGELOG.md`.

## Verify (ported 2026-10-02 without the DOS files or DOSBox): sword fighting
Everything in the 2026-10-02 CHANGELOG entry compiles and follows SDLPoP but has never run.
- **First, the demo**: `POP_DEMO=1 --dump out 0 ".400"` against a DOSBox recording of the
  attract mode. The kid should draw his sword at the guard, fight at skill 10 and, when the
  guard dies, run off. The demo used to stop before the fight, so any difference here is
  new; nothing before the guard may change.
- Level 1 room 3 (guard at row 1, col 7; `POP_START=3,...`): the guard's sprites (does the
  GUARD2.DAT-then-GUARD.DAT lookup in `CharSheets` find the right sheet? image indices are
  `frame.image + 1` from resource 750), his sword (sword number of the frame, `sword_tbl`
  offsets) and the hit-point triangles at the bottom (images 216/217 of KID.DAT, image 0 of
  the guard sheet).
- Fight feel: strike (Shift), parry (Up), advance/retreat, sheathe (Down); guard blocks and
  strikes at their skill; both hurt splashes; death by sword (pushed off a ledge, stabbed
  against a gate); the guard following through a room edge, and staying behind when the kid
  leaves with his sword away; `bump_into_opponent` for an unarmed kid.
- The probability tables (`DosTables.StrikeProb` ... `GuardHp`, `GuardType`) and `sword_tbl`
  (`DosRenderer.SwordTable`) are SDLPoP's values. Find them in PRINCE.EXE (SDLPoP's
  `options.c` lists the data-segment offsets per version) and read them from there.

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
- **Torches / potions / sword glint** (trobs since 2026-10-01): flame frames random,
  bubbles 1..7, glint every 40..103 ticks. **Level 13** rooms 23/16: the floor above falls.
- **Chompers**: confirmed open/shut the right way round by the user (2026-10-01). The
  start-up log's `draw tables:` lines say whether the EXE held the chomper tables at the
  old offsets.
- Climbing onto a ledge now goes behind the floor edge (confirmed by the user in the
  live game, 2026-09-30). Still check hanging and jumping past ledge edges
  (draw_other_overlay), and that the demo still matches (falls now redraw tiles over him).

## Movement / physics
- Level 1's first crouch should last until the presentation music ends
  (need_level1_music); with no sound we stand up straight away.
- Jumping through the mirror should release the kid's shadow (jump_through_mirror,
  seg003); needs other characters first. `KidEngine.JumpedThroughMirror` is set.
- Level 12: the bridge is ported (2026-10-01) but uniting with the shadow is a debug key
  (F8 / `POP_UNITED=1`) until the shadow and sword fighting exist (check_shadow, seg002).
  The kid's blink while united (draw_objtable_item) isn't drawn. Verify the bridge in
  rooms 2 and 13 against DOSBox.
- **Grabbing after a short jump**: works headless (level 1 room 12, standing jump from
  col 7 with Shift pressed after take-off → hangs on col 3). The user saw it fail in the
  live game, probably because Right Shift wasn't mapped (fixed 2026-09-30). If it still
  fails, record where and compare in DOSBox.

## Drawing
- **Demo room 2, col 9 row 0** (a gate, tile byte 0x24, modifier 2): DOSBox draws a
  stone pillar with a dotted strip (images 92/93, the pillar tile) there; we draw the
  gate's white front post (image 49). It was already like this before the trobs port.
  ~1400 of the ~1500 pixels still differing in the demo's room 2.

## Game features not in the DOS sim yet
- The rest of the characters: the shadow (level 4 mirror, 5 theft, 6 step, 12 final fight;
  `autocontrol_shadow_*`, `do_init_shad`, check_killed_shadow), the mouse, Jaffar's level 13
  events (`meet_Jaffar`, the exit opening on his death), the skeleton waking (`check_skel`),
  guard colours (`guard_palettes`, `curr_guard_color`), loose floors falling on guards,
  and the princess/vizier cutscenes. Sword fighting and plain guards are in (see Verify).
- Sounds (none at all), so the feather fall lasts a fixed 225 ticks rather than until
  its sound ends.
- "Press Button to Continue" is drawn with Raylib's font over the window, not in the
  original's status line, and there is no death music (so no wait for it).

## Housekeeping
- `AGENTS.md` still describes the legacy Apple II engine.
- The Apple II-era files are no longer referenced: POPGame builds with all of these
  removed (checked 2026-09-30): `Characters/*`, `Data/Apple2Image.cs`,
  `Dos/DosSpriteSheet.cs`, `Engine/Constants.cs`, `Engine/GameLoop.cs`,
  `Engine/GameState.cs`, `Input/InputHandler.cs`, `Rendering/{CharAtlas, CharSprites,
  EgaPalette, RaylibHud, RaylibRenderer, SpriteManager, TileAtlas, TileSprites,
  TileVisuals}.cs`, `World/*`. They were kept pending the owner's go-ahead to delete.
