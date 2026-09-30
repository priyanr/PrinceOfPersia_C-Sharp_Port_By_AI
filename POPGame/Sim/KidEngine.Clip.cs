using POPGame.Data;

namespace POPGame.Sim;

/// <summary>A clipping rectangle in 320x200 screen pixels; Right and Bottom are exclusive.</summary>
public readonly record struct ClipRect(int Left, int Top, int Right, int Bottom)
{
    /// <summary>reset_obj_clip (seg006): the whole play area, above the status line.</summary>
    public static readonly ClipRect Full = new(0, 0, 320, 192);
}

/// <summary>
/// What add_kid_to_objtable (seg008) works out for drawing the kid: his clip rectangle
/// (clip_char), the tile he is filed under for drawing (set_objtile_at_char), his
/// sprite's bottom y, and the tiles redraw_at_char2 marks to be drawn again over him —
/// <see cref="FloorOverlay"/> for climbing (frames 137..144), <see cref="Redraw2"/>
/// while hanging, jumping or falling. Tile positions are 0..29 in the drawn room.
/// </summary>
public sealed record KidDrawInfo(ClipRect Clip, int ObjTilepos, int ObjY,
                                 IReadOnlyList<int> Redraw2, IReadOnlyList<int> FloorOverlay);

/// <summary>
/// clip_char (seg006): where the kid's sprite is cut off so walls, doortops and the
/// floor above hide the parts of him behind them. add_kid_to_objtable (seg008) runs it
/// at draw time on a copy of the kid (loadkid without savekid).
/// </summary>
public sealed partial class KidEngine
{
    private static readonly int[] YClip = [-60, 3, 66, 129, 192];

    /// <summary>
    /// The clip rectangle the kid is drawn with this frame. The level door's position
    /// (<paramref name="leveldoorYBottom"/>, <paramref name="leveldoorRight"/>) comes
    /// from the room drawer, for climbing the exit stairs.
    /// </summary>
    public KidDrawInfo KidDraw(CharState kid, int leveldoorYBottom, int leveldoorRight)
    {
        // Drawing must not disturb the simulation, which keeps these globals between
        // routines: work on a copy of the kid and put everything back afterwards.
        var savedCh = _ch;
        var savedFrame = _frame;
        int savedObjX = _objX, savedObjY = _objY;
        int savedHalf = _charWidthHalf, savedHeight = _charHeight;
        int sL = _charXLeft, sR = _charXRight, sLC = _charXLeftColl, sRC = _charXRightColl;
        int savedRoom = _currRoom, savedCol = _tileCol, savedRow = _tileRow;
        var savedTile = _currTile2;
        int savedPosRoom = _posRoom, savedPosRow = _posRow, savedPosCol = _posCol;
        try
        {
            _ch = kid.Clone();
            LoadFramDetCol();
            LoadFrameToObj();
            SetCharCollision();

            // The rest of set_char_collision: the rows and columns the sprite spans.
            int charTopY = _objY - _charHeight + 1;
            if (charTopY >= 192) charTopY = 0;
            int charTopRow = YToRowMod4(charTopY);
            int charBottomRow = YToRowMod4(_objY);
            if (charBottomRow == -1) charBottomRow = 3;
            int charColLeft = Math.Max(Coord.BlockX(_charXLeft), 0);
            int charColRight = Math.Min(Coord.BlockX(_charXRight), 9);

            int objTilepos = SetObjtileAtChar(charBottomRow, charColLeft);
            var (redraw2, floorOverlay) = RedrawAtChar2(charTopRow, charBottomRow, charColLeft, charColRight);
            var clip = ClipCharCore(leveldoorYBottom, leveldoorRight);
            return new KidDrawInfo(clip, objTilepos, _objY, redraw2, floorOverlay);
        }
        finally
        {
            _ch = savedCh;
            _frame = savedFrame;
            _objX = savedObjX; _objY = savedObjY;
            _charWidthHalf = savedHalf; _charHeight = savedHeight;
            _charXLeft = sL; _charXRight = sR; _charXLeftColl = sLC; _charXRightColl = sRC;
            _currRoom = savedRoom; _tileCol = savedCol; _tileRow = savedRow;
            _currTile2 = savedTile;
            _posRoom = savedPosRoom; _posRow = savedPosRow; _posCol = savedPosCol;
        }
    }

    /// <summary>get_tilepos (seg006): -(col + 1) above the room, 30 anywhere else outside it.</summary>
    private static int GetTilepos(int tileCol, int tileRow)
    {
        if (tileRow < 0) return -(tileCol + 1);
        if (tileRow >= 3 || tileCol >= 10 || tileCol < 0) return 30;
        return tileRow * 10 + tileCol;
    }

    /// <summary>
    /// set_objtile_at_char (seg006): the tile the kid is drawn with — the one left of
    /// his column while he climbs, hangs or is in the air.
    /// </summary>
    private int SetObjtileAtChar(int charBottomRow, int charColLeft)
    {
        int frame = _ch.Frame;
        var action = _ch.Action;
        int row, col;
        if (action == CharAction.RunJump) { row = charBottomRow; col = charColLeft; }
        else { row = _ch.Row; col = _ch.Col; }
        if (frame is >= 135 and < 149 || action is CharAction.HangClimb or CharAction.InMidair
            or CharAction.InFreefall or CharAction.HangStraight)
        {
            --col;
        }
        int tilepos = GetTilepos(col, row);
        return tilepos < 0 ? 30 : tilepos;     // get_tilepos_nominus
    }

    /// <summary>
    /// redraw_at_char2 (seg003): the tiles the kid overlaps that are drawn again over
    /// him. Climbing up (frames 137..144) redraws the floor's front edge
    /// (set_redraw_floor_overlay); hanging, jumping and falling redraw the tiles whose
    /// left neighbour is open (set_redraw2). Tiles above the room are left out
    /// (redraw_frames_above only repaints the back layer).
    /// </summary>
    private (List<int> Redraw2, List<int> FloorOverlay) RedrawAtChar2(
        int charTopRow, int charBottomRow, int charColLeft, int charColRight)
    {
        var redraw2 = new List<int>();
        var floorOverlay = new List<int>();
        var action = _ch.Action;
        int frame = _ch.Frame;
        var marks = redraw2;
        if (frame is < 78 or >= 80)
        {
            if (frame is >= 137 and < 145)
            {
                marks = floorOverlay;
            }
            else if (action is not (CharAction.HangClimb or CharAction.InMidair or CharAction.InFreefall
                                    or CharAction.HangStraight)
                     && (action != CharAction.Bumped || frame is < 102 or > 106))
            {
                return (redraw2, floorOverlay);
            }
        }
        for (int col = charColRight; col >= charColLeft; --col)
        {
            if (action != CharAction.HangClimb) Mark(marks, GetTilepos(col, charBottomRow));
            if (charTopRow != charBottomRow) Mark(marks, GetTilepos(col, charTopRow));
        }
        return (redraw2, floorOverlay);

        static void Mark(List<int> list, int tilepos)
        {
            if (tilepos is >= 0 and < 30) list.Add(tilepos);
        }
    }

    private ClipRect ClipCharCore(int leveldoorYBottom, int leveldoorRight)
    {
        int frame = _ch.Frame;
        var action = _ch.Action;
        int room = _ch.Room;
        int row = _ch.Row;
        int top = 0, right = 320;

        int charTopY = _objY - _charHeight + 1;
        if (charTopY >= 192) charTopY = 0;
        int charTopRow = YToRowMod4(charTopY);
        int charColLeft = Math.Max(Coord.BlockX(_charXLeft), 0);
        int charColRight = Math.Min(Coord.BlockX(_charXRight), 9);

        if (frame is >= 224 and < 229)
        {
            // Going up the exit stairs: behind the door.
            top = leveldoorYBottom + 1;
            right = leveldoorRight;
        }
        else
        {
            // Under a floor or wall above: cut off at the floor line of the row above.
            if (GetTile(room, charColLeft, charTopRow) == TileId.Block || TileIsFloor(_currTile2))
            {
                if ((action == CharAction.Stand && frame is 79 or 81)
                    || GetTile(room, charColRight, charTopRow) == TileId.Block || TileIsFloor(_currTile2))
                {
                    int clipRow = row + 1;
                    if (clipRow is >= 0 and < 5)
                    {
                        int clipY = YClip[clipRow];
                        if (clipRow == 1 || (clipY < _objY && clipY - 15 < charTopY))
                            top = clipY;
                    }
                }
            }

            // Behind a doortop or a wall to the right.
            int col = Coord.BlockX(_charXLeftColl - 4);
            if (GetTile(room, col + 1, row) == TileId.PanelWF || _currTile2 == TileId.PanelWOF)
            {
                right = (_tileCol << 5) + 32;
            }
            else if ((GetTile(room, col, row) != TileId.PanelWF && _currTile2 != TileId.PanelWOF)
                     || action == CharAction.InMidair
                     || (action == CharAction.InFreefall && frame == 106)
                     || (action == CharAction.Bumped && frame == 107)
                     || (!_ch.FacingRight && (action is CharAction.HangClimb or CharAction.HangStraight
                                              || (action == CharAction.RunJump && frame is >= 137 and < 140))))
            {
                col = Coord.BlockX(_charXRightColl);
                if ((GetTile(room, col, row) == TileId.Block
                     || (_currTile2 == TileId.Mirror && _ch.FacingRight))
                    && (GetTile(room, col, charTopRow) == TileId.Block || _currTile2 == TileId.Mirror)
                    && room == _currRoom)
                {
                    right = _tileCol << 5;
                }
            }
            else
            {
                right = (_tileCol << 5) + 32;
            }
        }

        return ClipRect.Full with { Top = top, Right = right };
    }
}
