using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using PeartreeGames.BlockyWorldEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PeartreeGames.BlockyWorldStreamer
{
    public static class BlockyWorldUtilities
    {
        public const string ScenePrefix = "world_";
        public const int SceneGridSize = 100;
        public const int SceneQuadSize = 50;
        public static float SceneQuadExtents => SceneQuadSize / 2f;

        public static List<Vector2Int> GetNeighbouringCells(Vector2Int pos)
        {
            var result = new List<Vector2Int>(8);
            for (var x = -1; x <= 1; x++)
            {
                for (var y = -1; y <= 1; y++)
                {
                    if (x == 0 && y == 0) continue;
                    result.Add(pos + new Vector2Int(x, y));
                }
            }
            return result;
        }
        
        public static Vector2Int GetCellFromWorldPosition(Vector3 pos)
        {
            var targetPos = Vector3Int.RoundToInt(pos);
            var center = GetCellPosition(targetPos);
            return new Vector2Int(Mathf.RoundToInt(center.x / SceneGridSize), Mathf.RoundToInt(center.z / SceneGridSize));
        }

        public static Vector2Int GetCellFromSceneName(string name)
        {
            var regex = new Regex(@"\((?<numbers>[^)]*)\)");
            var match = regex.Match(name).Groups["numbers"];
            var numbers = match.Value.Split(',');
            return new Vector2Int(int.Parse(numbers[0]), int.Parse(numbers[1]));
        }

        public static string GetSceneNameFromCell(Vector2Int pos) => $"{ScenePrefix}{pos}";
        
        public static Vector3 GetCellPosition(Vector3Int target) =>
            BlockyUtilities.SnapToGrid(target - BlockyUtilities.GridOffset, 0, SceneGridSize) + BlockyUtilities.GridOffset;

        public static Vector3 GetQuadPosition(Vector3Int target) =>
            BlockyUtilities.SnapToGrid(target - new Vector3(SceneQuadExtents, 0, SceneQuadExtents), 0, SceneQuadSize) + new Vector3(SceneQuadExtents, 0, SceneQuadExtents);
        
        public static GameObject CombineColliders(GameObject obj)
        {
            if (ExceptionNames.Any(e => obj.name.Contains(e))) return null;
            var prevColliders = obj.transform.parent.Find($"{obj.name}_Colliders");
            if (prevColliders != null) Object.DestroyImmediate(prevColliders.gameObject);

            var originalPos = obj.transform.position;
            obj.transform.position = Vector3.zero;

            var colliders = obj.GetComponentsInChildren<BoxCollider>();
            var maxHeight = int.MinValue;
            var minHeight = int.MaxValue;
            var colliderPositions = new Dictionary<int, (List<Vector3Int> list, GameObject go)>();
            var offset = Vector3.one * 0.25f;
            var collidersContainer = new GameObject($"{obj.name}_Colliders")
            {
                isStatic = true,
                layer = obj.layer
            };
            var collidersByLayer = new Dictionary<LayerMask, GameObject>();
            
            foreach (var col in colliders)
            {
                if (!collidersByLayer.TryGetValue(col.gameObject.layer, out var colliderParent))
                {
                    colliderParent = new GameObject(LayerMask.LayerToName(col.gameObject.layer))
                    {
                        layer = col.gameObject.layer
                    };
                    colliderParent.transform.SetParent(collidersContainer.transform);
                    collidersByLayer.Add(col.gameObject.layer, colliderParent);
                }
                if (!col.size.Equals(Vector3.one) || Mathf.Abs(Mathf.RoundToInt(col.transform.position.y % 1)) != 0)
                {
                    var newCol = colliderParent.AddComponent<BoxCollider>();
                    var pivot = col.transform.position;
                    var point = pivot + col.center;
                    var rot = col.transform.rotation;
                    newCol.center = rot * (point - pivot) + pivot;
                    var size = rot * col.size;
                    newCol.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
                    continue;
                }
                var pos = col.gameObject.transform.position;
                var height = pos.y;
                pos -= offset;
                var key = Mathf.RoundToInt(height);
                if (key > maxHeight) maxHeight = key;
                if (key < minHeight) minHeight = key;
                if (!colliderPositions.ContainsKey(key)) colliderPositions.Add(key, (new List<Vector3Int>(), colliderParent));
                colliderPositions[key].list.Add(Vector3Int.RoundToInt(pos));
            }


            offset = new Vector3(-0.5f, -0.5f, -0.5f);
            for (var i = minHeight; i <= maxHeight; i++)
            {
                if (!colliderPositions.ContainsKey(i)) continue;
                var cols = colliderPositions[i];
                if (cols.list.Count == 0) continue;
                var bounds = DecomposeBounds(cols.list.ToArray());
                foreach (var bound in bounds)
                {
                    var col = cols.go.AddComponent<BoxCollider>();
                    col.center = bound.center + offset;
                    col.size = bound.size;
                }
            }

            collidersContainer.transform.position = originalPos;
            obj.transform.position = originalPos;
            obj.SetActive(false);
            return collidersContainer;
        }

        public static Bounds[] DecomposeBounds(Vector3Int[] positions, float height = 0.5f)
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
                var center = new Vector3((left.x + right.x) / 2f + 0.5f, p1.y, (left.y + right.y) / 2f + 0.5f);
                var extends = new Vector3(Mathf.Abs(center.x - left.x), height,
                    Mathf.Abs(center.z - right.y)) * 2;
                result.Add(new Bounds(center, extends));
            }

            return result.ToArray();
        }

        public static readonly string[] ExceptionNames = {"Exclusions", "Colliders"};
    }
}