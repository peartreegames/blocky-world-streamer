using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeartreeGames.BlockyWorldStreamer.Editor
{
    public static class BlockyWorldUtilities
    {
        public static Bounds[] DecomposeBounds(Vector3Int[] positions)
        {
            if (positions.Length == 0) return Array.Empty<Bounds>();
            var result = new List<Bounds>();
            var set = new HashSet<(int x, int y)>();

            var p1 = positions[0];
            var (iXMax, iYMax) = (p1.x, p1.z);
            var (iXMin, iYMin) = (p1.x, p1.z);
            set.Add((p1.x, p1.z));

            // determine boundaries
            for (var i = 1; i < positions.Length; i++)
            {
                var p = positions[i];
                if (p.x > iXMax) iXMax = p.x;
                if (p.x < iXMin) iXMin = p.x;
                if (p.z > iYMax) iYMax = p.z;
                if (p.z < iYMin) iYMin = p.z;
                set.Add((p.x, p.z));
            }

            var pMin = new Vector2Int(iXMin, iYMin);
            var maxX = Mathf.Abs(iXMin - iXMax) + 1;
            var maxY = Mathf.Abs(iYMin - iYMax) + 1;

            var matrix = new int[maxY][];
            for (var i = 0; i < matrix.Length; i++) matrix[i] = new int[maxX];

            var cache = new List<int>();
            var stack = new Stack<Vector2Int>();
            // fill matrix
            for (var y = 0; y < maxY; y++)
            {
                for (var x = 0; x < maxX; x++)
                {
                    if (set.Contains((x + iXMin, y + iYMin))) matrix[y][x] = 1;
                }
            }

            void UpdateCache(IReadOnlyList<int> row)
            {
                for (var m = 0; m < maxX; m++)
                {
                    if (row[m] == 0) cache[m] = 0;
                    else cache[m]++;
                }
            }

            BlockyRectangle FindMaximal()
            {
                var bestRect = new BlockyRectangle(0, Vector2Int.zero, Vector2Int.zero);
                cache.Clear();
                for (var c = 0; c <= maxX; c++)
                {
                    cache.Add(0);
                    stack.Push(Vector2Int.zero);
                }

                for (var n = 0; n < maxY; n++)
                {
                    var openWidth = 0;
                    UpdateCache(matrix[n]);
                    for (var m = 0; m <= maxX; m++)
                    {
                        if (cache[m] > openWidth)
                        {
                            stack.Push(new Vector2Int(m, openWidth));
                            openWidth = cache[m];
                        }
                        else if (cache[m] < openWidth)
                        {
                            Vector2Int p;
                            do
                            {
                                p = stack.Pop();
                                var area = openWidth * (m - p.x);
                                if (area > bestRect.Area)
                                {
                                    bestRect = new BlockyRectangle(area, new Vector2Int(p.x, n),
                                        new Vector2Int(m - 1, n - openWidth + 1));
                                }

                                openWidth = p.y;
                            } while (cache[m] < openWidth);

                            openWidth = cache[m];
                            if (openWidth != 0) stack.Push(new Vector2Int(p.x, p.y));
                        }
                    }
                }

                return bestRect;
            }

            var rects = new List<BlockyRectangle>();
            while (set.Count > 0)
            {
                var best = FindMaximal();
                for (var y = best.BottomRight.y; y <= best.TopLeft.y; y++)
                {
                    for (var x = best.TopLeft.x; x <= best.BottomRight.x; x++)
                    {
                        matrix[y][x] = 0;
                        set.Remove((x + iXMin, y + iYMin));
                    }
                }

                rects.Add(best);
            }

            foreach (var r in rects)
            {
                var left = r.TopLeft + pMin;
                var right = r.BottomRight + pMin;
                var center = new Vector3((left.x + right.x) / 2f + 0.5f, 0, (left.y + right.y) / 2f + 0.5f);
                var extends = new Vector3(Mathf.Abs(center.x - left.x), 0.5f,
                    Mathf.Abs(center.z - right.y)) * 2;
                result.Add(new Bounds(center, extends));
            }

            return result.ToArray();
        }
    }
}