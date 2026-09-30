using POPGame.Data;

namespace POPGame.Sim;

/// <summary>
/// Picking up the sword and drinking potions (seg005 check_get_item / get_item,
/// seg006 do_pickup / proc_get_object), the potions' effects, and a falling loose
/// floor hitting the kid (seg007 fell_on_your_head).
/// </summary>
public sealed partial class KidEngine
{
    /// <summary>upside_down: the screen is drawn flipped (the blue "invert" potion).</summary>
    public bool UpsideDown { get; private set; }

    /// <summary>is_feather_fall: ticks of slow fall so far, 0 = none (the green potion).</summary>
    private int _isFeatherFall;

    /// <summary>
    /// How long a feather fall lasts. The DOS game ends it with its sound; SDLPoP's
    /// fixed length is 18.75 s at 12 ticks a second.
    /// </summary>
    private const int FeatherFallTicks = 225;

    /// <summary>pickup_obj_type: -1 the sword, 1..6 a potion, 0 nothing.</summary>
    private int _pickupObjType;

    /// <summary>The flash the screen gives on a pickup: colour index and ticks (flash_color / flash_time).</summary>
    public int FlashColor { get; private set; }
    public int FlashTime { get; set; }

    /// <summary>timers (seg003): the parts of it the kid needs.</summary>
    private void Timers()
    {
        if (_isFeatherFall != 0 && ++_isFeatherFall > FeatherFallTicks) _isFeatherFall = 0;
    }

    /// <summary>
    /// check_get_item: with an item underfoot, step back a block if there is floor
    /// behind; then an item in front gets picked up.
    /// </summary>
    private bool CheckGetItem()
    {
        if (GetTileAtChar() == TileId.Flask || _currTile2 == TileId.Sword)
        {
            if (!TileIsFloor(GetTileBehindChar())) return false;
            _ch.X = _ch.DxForward(-14);
            LoadFramDetCol();
        }
        if (GetTileInfrontofChar() == TileId.Flask || _currTile2 == TileId.Sword)
        {
            GetItem();
            return true;
        }
        return false;
    }

    /// <summary>get_item: standing, crouch in reach of it first; crouched, take it.</summary>
    private void GetItem()
    {
        if (_ch.Frame != 109)
        {
            int distance = GetEdgeDistance();
            if (_edgeType != EdgeFloor) _ch.X = _ch.DxForward(distance);
            if (_ch.FacingRight) _ch.X = _ch.DxForward((_currTile2 == TileId.Flask ? 1 : 0) - 2);
            Crouch();
        }
        else if (_currTile2 == TileId.Sword)
        {
            DoPickup(-1);
            StartSeq(Sim.Seq.PickUpSword);
        }
        else
        {
            DoPickup(Modif() >> 3);
            StartSeq(Sim.Seq.DrinkPotion);
        }
    }

    /// <summary>do_pickup: the item leaves plain floor behind; its effect comes later in the sequence.</summary>
    private void DoPickup(int objType)
    {
        _pickupObjType = objType;
        _controlShift2 = Ignore;
        if (_posRoom <= 0) return;
        int i = (_posRoom - 1) * Level.CellsPerScreen + _posRow * Coord.Cols + _posCol;
        _level.LiveBlueType[i] = (byte)TileId.Floor;
        _level.LiveBlueSpec[i] = 0;
    }

    /// <summary>proc_get_object: the get-item opcode of the pickup/drink sequence.</summary>
    private void ProcGetObject()
    {
        if (_pickupObjType == 0) return;
        if (_pickupObjType == -1)
        {
            _ch.HasSword = true;
            Flash(14, 8);                                   // bright yellow
            return;
        }
        switch (_pickupObjType)
        {
            case 1:                                         // health
                if (_ch.Hp != _ch.MaxHp)
                {
                    _ch.Hp++;
                    Flash(4, 2);
                }
                break;
            case 2:                                         // life: one more, and full health
                _ch.MaxHp = Math.Min(_ch.MaxHp + 1, 10);
                _ch.Hp = _ch.MaxHp;
                Flash(4, 4);
                break;
            case 3:                                         // feather: slow fall
                _isFeatherFall = 1;
                Flash(2, 3);
                break;
            case 4:                                         // invert the screen
                UpsideDown = !UpsideDown;
                break;
            case 6:                                         // open: the button at room 8's first tile
                _hazards.PressButton(8, 0, 0);
                break;
            case 5:                                         // hurt
                TakeHp(1);
                break;
        }
    }

    private void Flash(int color, int time)
    {
        FlashColor = color;
        FlashTime = time;
    }

    /// <summary>
    /// fell_on_your_head: a falling loose floor hits the kid. It costs a hit point and
    /// knocks him down (or kills him), except while he runs (frames 5..14), hangs or
    /// climbs.
    /// </summary>
    public void LooseFellOnKid(CharState ch, SeqEffects fx)
    {
        _ch = ch;
        _fx = fx;
        int frame = _ch.Frame;
        var action = _ch.Action;
        if (!((_levelNumber == 13 || frame < 5 || frame >= 15)
              && (action < CharAction.HangClimb || action == CharAction.Turn)))
            return;

        _ch.Y = Coord.FloorY(_ch.Row);
        if (TakeHp(1))
        {
            StartSeq(Sim.Seq.HardLand);                     // seq_22_crushed
            if (frame == 177) _ch.X = _ch.DxForward(-12);   // spiked
        }
        else if (frame != 109)
        {
            if (GetTileBehindChar() == TileId.Space) _ch.X = _ch.DxForward(-2);
            StartSeq(Sim.Seq.Crush);                        // seq_52_loose_floor_fell_on_kid
        }
    }
}
