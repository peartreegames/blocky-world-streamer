using System.Collections.Generic;
using PeartreeGames.BlockyWorldEditor.Editor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PeartreeGames.BlockyWorldStreamer.Editor
{
    public class BlockyWorldStreamerScenePreprocessor : IBlockyScenePreprocessor
    {
        public void ProcessScene(BlockyEditorWindow window, Scene scene)
        {
            var mapParent = BlockyWorldStreamerParentSetter.GetMapParent(scene, "Map");
            if (mapParent == null) return;
            var toAddList = new List<GameObject>();
            foreach (Transform child in mapParent.transform)
            {
                var (combiners, colliders) = CombineMeshes(child.gameObject);
                if (combiners != null) toAddList.Add(combiners);
                if (colliders != null) toAddList.Add(colliders);
            }

            foreach (var toAdd in toAddList) toAdd.transform.SetParent(mapParent.transform);
        }

        public void RevertScene(BlockyEditorWindow window, Scene scene) => RevertScene(scene);

        private static (GameObject comb, GameObject cols) CombineMeshes(GameObject obj)
        {
            if (obj.name.Contains("Combined") || obj.name.Contains("Colliders")) return (null, null);
            var prev = obj.transform.parent.Find($"{obj.name}_Combined");
            if (prev != null) Object.DestroyImmediate(prev.gameObject);
            var prevColliders = obj.transform.parent.Find($"{obj.name}_Colliders");
            if (prevColliders != null) Object.DestroyImmediate(prevColliders.gameObject);

            var originalPos = obj.transform.position;
            obj.transform.position = Vector3.zero;

            var filters = obj.GetComponentsInChildren<MeshFilter>();
            var dict = new Dictionary<Mesh, (Material mat, List<CombineInstance> combs)>();
            foreach (var filter in filters)
            {
                if (!dict.TryGetValue(filter.sharedMesh, out var combines))
                {
                    combines = (filter.GetComponent<Renderer>().sharedMaterial, new List<CombineInstance>());
                    dict.Add(filter.sharedMesh, combines);
                }

                combines.combs.Add(new CombineInstance()
                {
                    mesh = filter.sharedMesh,
                    transform = filter.transform.localToWorldMatrix
                });
            }

            var container = new GameObject()
            {
                name = $"{obj.name}_Combined"
            };
            foreach (var (key, (mat, combs)) in dict)
            {
                var combined = new GameObject
                {
                    name = $"{key.name}_Combined"
                };
                var mesh = new Mesh();
                mesh.CombineMeshes(combs.ToArray(), true, true);
                combined.AddComponent<MeshFilter>().mesh = mesh;
                combined.AddComponent<MeshRenderer>().material = mat;
                combined.transform.SetParent(container.transform);
            }

            var colliders = obj.GetComponentsInChildren<BoxCollider>();
            var maxHeight = int.MinValue;
            var totalCount = colliders.Length;
            var colliderPositions = new Dictionary<int, List<Vector3Int>>();
            var offset = Vector3.one * 0.25f;
            foreach (var col in colliders)
            {
                if (Vector3.SqrMagnitude(col.size - Vector3.one) > Mathf.Epsilon) continue;
                var pos = col.gameObject.transform.position;
                var height = pos.y;
                pos -= offset;
                var key = Mathf.RoundToInt(height);
                if (key > maxHeight) maxHeight = key;
                if (!colliderPositions.ContainsKey(key)) colliderPositions.Add(key, new List<Vector3Int>());
                colliderPositions[key].Add(Vector3Int.RoundToInt(pos));
            }

            var collidersContainer = new GameObject($"{obj.name}_Colliders")
            {
                isStatic = true,
                layer = obj.layer
            };
            var colliderSelectionObject = new GameObject("ColliderSelection");
            colliderSelectionObject.transform.SetParent(collidersContainer.transform);
            offset = new Vector3(0, 0.5f, 0);
            var newCount = 0;
            for (var i = 0; i <= maxHeight; i++)
            {
                if (!colliderPositions.ContainsKey(i)) continue;
                var cols = colliderPositions[i];
                if (cols.Count == 0) continue;
                var bounds = BlockyWorldUtilities.DecomposeBounds(cols.ToArray());
                foreach (var bound in bounds)
                {
                    var col = colliderSelectionObject.AddComponent<BoxCollider>();
                    col.center = bound.center + offset;
                    col.size = bound.size;
                    newCount++;
                }
            }

            container.transform.position = originalPos;
            collidersContainer.transform.position = originalPos;
            obj.transform.position = originalPos;
            obj.SetActive(false);
            return (container, collidersContainer);
        }

        public static void RevertScene(Scene scene)
        {
            var mapParent = BlockyWorldStreamerParentSetter.GetMapParent(scene, "Map");
            if (mapParent == null) return;
            var toDestroyList = new List<GameObject>();
            foreach (Transform child in mapParent.transform)
            {
                if (child.gameObject.name.Contains("Combined") || child.gameObject.name.Contains("Colliders"))
                    toDestroyList.Add(child.gameObject);
                else child.gameObject.SetActive(true);
            }

            foreach (var toDestroy in toDestroyList) Object.DestroyImmediate(toDestroy);
        }
    }
}