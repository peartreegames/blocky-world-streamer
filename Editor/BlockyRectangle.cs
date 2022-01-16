using System;
using UnityEngine;

namespace PeartreeGames.BlockyWorldStreamer.Editor
{
    public class BlockyRectangle
    {
        public readonly int Area;
        public Vector2Int TopLeft;
        public Vector2Int BottomRight;

        public BlockyRectangle(int area, Vector2Int topLeft, Vector2Int bottomRight)
        {
            Area = area;
            TopLeft = topLeft;
            BottomRight = bottomRight;
        }
    }
}