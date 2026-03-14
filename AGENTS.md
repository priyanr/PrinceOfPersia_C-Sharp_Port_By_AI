# POPCS — Prince of Persia Apple II (C# Remake)

## Project Overview
Prince of Persia Apple II remake — two C# .NET 10 projects:
- **POPGame** (`POPGame/`) — playable Raylib-based game
- **LevelEditor** (`LevelEditor/`) — read-only level viewer
- Original 6502 ASM source: `originalcode/Prince-of-Persia-Apple-II-master/01 POP Source/`
- Level files: `originalcode/.../01 POP Source/Levels/LEVEL0` through `LEVEL14`

## Build & Run
```bash
dotnet build POPGame/POPGame.csproj
dotnet run --project POPGame
```

## POPGame Architecture
```
POPGame/
  Program.cs                    entry: FindLevelsDir() → GameLoop.Run()
  Data/TileId.cs                enum TileId 0-29
  Data/Level.cs                 binary level loader + LiveBlueType/LiveBlueSpec mutable copies
  Engine/Constants.cs           physics/combat/hazard constants from 6502 source
  Engine/GameState.cs           GamePhase enum + all runtime state
  Engine/GameLoop.cs            Raylib window 640x480, 10fps tick, rebuilds subsystems on LoadLevel
  World/LinkTable.cs            LINKLOC/LINKMAP trigger resolution
  World/HazardSystem.cs         gate/spike/slicer/loose-floor tick updates
  Characters/CharState.cs       struct: pos, face, action, YVel, life, combat state
  Characters/PhysicsEngine.cs   gravity, floor check, ledge grab, screen transitions
  Characters/PlayerController.cs  PLAYERCTRL → state machine (OnGround→Running→Jumping etc.)
  Characters/GuardController.cs   patrol/alert/engarde/strike AI with prob tables
  Characters/CombatSystem.cs      strike/block resolution per tick
  Input/InputState.cs           held + fresh-press flags
  Input/InputHandler.cs         legacy Console.KeyAvailable polling (unused)
  Input/RaylibInput.cs          Raylib keyboard polling (active)
  Rendering/TileAtlas.cs        Apple II tile sprite atlas loader (AppleTileHeight=63)
  Rendering/TileSprites.cs      tile rendering with Apple II textures
  Rendering/CharAtlas.cs        Apple II character sprite atlas loader
  Rendering/CharSprites.cs      character rendering (texture or stick-figure fallback)
  Rendering/RaylibRenderer.cs   full screen repaint: tiles then characters per cell
  Rendering/RaylibHud.cs        HP bar, level, timer overlay
  Rendering/EgaPalette.cs       color constants
  Rendering/SpriteManager.cs    Init/Unload lifecycle for all atlases
```

## Rendering Pipeline
- Raylib window: 640x480, renders to 320x240 virtual canvas scaled 2x
- Render texture is flipped vertically (Raylib stores bottom-up)
- Tile scale: Apple II 28x63 → screen 32x72 (scale = TileH/AppleTileHeight ≈ 1.143)
- Character sprites use the SAME scale factor as tiles for consistent proportions
- Apple II character sprites (CHTAB) natively face LEFT; flip when FaceDir > 0

## Level File Format (2304 bytes each)
| Offset | Size | Section  | Description |
|--------|------|----------|-------------|
| 0x000  | 720  | BLUETYPE | tile type for 24 screens x 30 cells |
| 0x2D0  | 720  | BLUESPEC | tile state (gate height, spike frame, etc.) |
| 0x5A0  | 256  | LINKLOC  | trigger→linked-cell lookup |
| 0x6A0  | 256  | LINKMAP  | linked screen for each LINKLOC entry |
| 0x7A0  |  96  | MAP      | 24 screens x 4 bytes [left,right,above,below] |
| 0x800  | 256  | INFO     | player/guard start positions |

## Screen & Cell Layout
- 24 screens per level (1-indexed in game; 0-indexed in arrays)
- 30 cells per screen = 3 rows x 10 cols
- cell_index = row*10 + col (row 0 = top, row 2 = bottom)
- BLUETYPE byte: lower 5 bits = tile ID, upper 3 bits = editor flags
- Screen N blueprint starts at (N-1)*30

## Tile IDs (from BGDATA.S)
0=space, 1=floor, 2=spikes, 3=posts, 4=gate, 5=dpressplate, 6=pressplate,
7=panelwif, 8=pillarbottom, 9=pillartop, 10=flask, 11=loose, 12=panelwof,
13=mirror, 14=rubble, 15=upressplate, 16=exit, 17=exit2, 18=slicer, 19=torch,
20=block, 21=bones, 22=sword, 23=window, 24=window2, 25=archbot, 26-29=archtop1-4

## Floor Tile Convention (CRITICAL)
In POP, almost ALL tiles have a walkable floor surface at the top of the cell.
Only these tiles have NO floor:
- Space (0) — empty air
- PillarTop (9) — hanging decoration from above
- PanelWOF (12) — "panel without floor"
- ArchTop1-4 (26-29) — upper arch decorations

Everything else (floor, torch, block, posts, gate, spikes, slicer, flask, mirror,
rubble, bones, sword, window, loose, pressplates, exit, pillarbot, archbot) IS walkable.

The character stands AT the same row as the floor tile (not one row above).
`HasFloorAt(ch)` checks the tile at the character's current (row, col).
If no floor at current position → character enters FreeFall.

## Physics Model
- Character position: (Screen, BlockX, BlockY) — screen is 1-indexed
- FaceDir: -1 = left (native Apple II sprite direction), +1 = right
- Gravity: YVel += FFAccel (3) per tick, capped at FFTermVel (29)
- Falling: move BlockY++ each tick, land when HasFloorAt is true
- PlayerController calls PhysicsEngine.Update first, then processes input
- Action state machine: OnGround/Running/Turning → Jumping/FreeFall → Hanging → ClimbUp/Down
- Arrow keys only work in OnGround/Running/Turning states (not during FreeFall)

## MAP Neighbours
MAP[(screen-1)*4+dir]: 0=left, 1=right, 2=above, 3=below
Values are 1-indexed screen numbers; 0 = no neighbour

## INFO Section (offset 0x800)
- INFO[0]=screen count, INFO[64]=KidStartScrn, INFO[65]=KidStartBlock,
  INFO[66]=KidStartFace (0xFF=left)
- INFO[68-69]=SwordStart, INFO[71+]=guard arrays (block/face/x/seq/prog, 24 each)

## Level 1 Reference Data
- KidStartScrn=1, block=0 (row=0 col=0), face=0xFF (left)
- Screen 1 row 0: space space space floor floor floor floor floor block block
- Screen 1 row 1: torch torch floor posts space block block block block block
- Screen 1 row 2: block block block block rubble posts loose floor floor block
- Screen 1 neighbours: left=5, right=0, above=0, below=2
- Guard on screen 3 at block 17 (row=1 col=7)
- Guard on screen 21 at block 6 (row=0 col=6)

## Common Pitfalls
- IsFloorTile must use exclusion list (not inclusion) — most tiles are walkable
- Character sprites use tile-consistent scaling (TileH/AppleTileHeight), NOT fit-to-cell
- Apple II sprites face LEFT natively; flip condition is `FaceDir > 0`
- UpdateGrounded only checks HasFloorAt (not HasFloorBelow) to avoid floating
- UpdateAirborne: check HasFloorAt before AND after BlockY++ to catch landings
- Input uses RaylibInput (not legacy Console-based InputHandler)
- No Python available on this machine; use C#/dotnet or bash for scripting
