namespace POPGame.Data;

public enum TileId : byte
{
    Space        = 0,
    Floor        = 1,
    Spikes       = 2,
    Posts        = 3,
    Gate         = 4,
    DPressPlate  = 5,   // pressure plate down (triggered)
    PressPlate   = 6,   // pressure plate up (normal)
    PanelWF      = 7,   // panel with floor
    PillarBot    = 8,
    PillarTop    = 9,
    Flask        = 10,
    Loose        = 11,
    PanelWOF     = 12,  // panel without floor
    Mirror       = 13,
    Rubble       = 14,
    UPressPlate  = 15,  // upward pressure plate
    Exit         = 16,
    Exit2        = 17,
    Slicer       = 18,
    Torch        = 19,
    Block        = 20,
    Bones        = 21,
    Sword        = 22,
    Window       = 23,
    Window2      = 24,
    ArchBot      = 25,
    ArchTop1     = 26,
    ArchTop2     = 27,
    ArchTop3     = 28,
    ArchTop4     = 29,
}
