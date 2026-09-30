using POPGame.Data;

namespace POPGame.Sim;

/// <summary>A clipping rectangle in 320x200 screen pixels; Right and Bottom are exclusive.</summary>
public readonly record struct ClipRect(int Left, int Top, int Right, int Bottom)
{
    /// <summary>reset_obj_clip (seg006): the whole play area, above the status line.</summary>
    public static readonly ClipRect Full = new(0, 0, 320, 192);
}

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
    public ClipRect ClipChar(CharState kid, int leveldoorYBottom, int leveldoorRight)
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
            return ClipCharCore(leveldoorYBottom, leveldoorRight);
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

    private ClipRect ClipCharCore(int leveldoorYBottom, int leveldoorRight)
    {
        int frame = _ch.Frame;
        var action = _ch.Action;
        int room = _ch.Room;
        int row = _ch.Row;
        int top = 0, right = 320;

        // The rest of set_char_collision: the sprite's top and the columns it spans.
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
