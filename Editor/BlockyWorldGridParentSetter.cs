using System;
using System.Collections.Generic;
using System.Linq;
using PeartreeGames.BlockyWorldEditor;
using PeartreeGames.BlockyWorldEditor.Editor;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PeartreeGames.BlockyWorldStreamer.Editor
{
    public class BlockyWorldGridParentSetter : BlockyParentSetter
    {
        private Dictionary<string, GameObject> _mapParents;

        public override void Init(BlockyEditorWindow window)
        {
            _mapParents = new Dictionary<string, GameObject>();
            window.onBlockyModeChange -= OnModeChange;
            window.onBlockyModeChange += OnModeChange;
        }

        private void OnModeChange(BlockyEditMode mode)
        {
            switch (mode)
            {
                case BlockyEditMode.None:
                    ProcessScenes();
                    break;
                case BlockyEditMode.Paint:
                    DestroyCombinedMeshes();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        private void DestroyCombinedMeshes()
        {
            var openSceneCount = EditorSceneManager.loadedSceneCount;
            for (var i = 0; i < openSceneCount; i++)
            {
                var mapParent = GetMapParent(SceneManager.GetSceneAt(i));
                if (mapParent == null) continue;
                var toDestroyList = new List<GameObject>();
                foreach(Transform child in mapParent.transform)
                {
                    if (child.gameObject.name.Contains("Combined") || child.gameObject.name.Contains("Colliders")) toDestroyList.Add(child.gameObject);
                    else child.gameObject.SetActive(true);
                }
                foreach(var toDestroy in toDestroyList) DestroyImmediate(toDestroy);
            }
        
        }

        private void ProcessScenes()
        {
            var openSceneCount = EditorSceneManager.loadedSceneCount;
            for (var i = 0; i < openSceneCount; i++)
            {
                var mapParent = GetMapParent(SceneManager.GetSceneAt(i));
                if (mapParent == null) continue;
                var toAddList = new List<GameObject>();
                foreach(Transform child in mapParent.transform)
                {
                    Debug.Log(child.name);
                    var (combiners, colliders) = CombineMeshes(child.gameObject);
                    if (combiners != null) toAddList.Add(combiners);
                    if (colliders != null) toAddList.Add(colliders);
                }
                foreach(var toAdd in toAddList) toAdd.transform.SetParent(mapParent.transform);
            }
        }

        private (GameObject comb, GameObject cols) CombineMeshes(GameObject obj)
        {
            if (obj.name.Contains("Combined") || obj.name.Contains("Colliders")) return (null, null);
            var prev = obj.transform.parent.Find($"{obj.name}_Combined");
            if (prev != null) DestroyImmediate(prev.gameObject);
            var prevColliders = obj.transform.parent.Find($"{obj.name}_Colliders");
            if (prevColliders != null) DestroyImmediate(prevColliders.gameObject);
            
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
            foreach (var (key, value) in dict)
            {
                var combined = new GameObject
                {
                    name = $"{key.name}_Combined"
                };
                var mesh = new Mesh();
                mesh.CombineMeshes(value.combs.ToArray(), true, true);
                combined.AddComponent<MeshFilter>().mesh = mesh;
                combined.AddComponent<MeshRenderer>().material = value.mat;
                combined.transform.SetParent(container.transform);
            }
            
            var colliders = obj.GetComponentsInChildren<BoxCollider>();
            var maxHeight = int.MinValue;
            var totalCount = colliders.Length;
            var colliderPositions = new Dictionary<int, List<Vector3Int>>();
            var offset = Vector3.one * 0.25f;
            foreach (var col in colliders)
            {
                if (Vector3.SqrMagnitude(col.size - Vector3.one) > Mathf.Epsilon ) continue;
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
                    col.center = bound.center - offset;
                    col.size = bound.size;
                    newCount++;
                }
            }

            container.transform.position = originalPos;
            collidersContainer.transform.position = originalPos;
            obj.transform.position = originalPos;
            obj.SetActive(false);
            Debug.Log($"Colliders: {totalCount} => {newCount}");
            return (container, collidersContainer);
        }


        public static GameObject GetMapParent(Scene scene)
        {
            var roots = scene.GetRootGameObjects().ToList();
            return roots.Find(root => root.name == "Map");
        }

        public override Transform GetParent(BlockyObject block)
        {
            var pos = Vector3Int.RoundToInt(block.transform.position);
            var center = GetSectionPosition(pos, pos.y);
            var sceneName = $"world_{Mathf.RoundToInt(center.x / 100)}_{Mathf.RoundToInt(center.z / 100)}";
            if (!_mapParents.TryGetValue(sceneName, out var mapParent))
            {
                var sceneGuids = AssetDatabase.FindAssets("t:Scene");
                var foundScene = sceneGuids.Any(sceneGuid =>
                    SceneManager.GetSceneByPath(AssetDatabase.GUIDToAssetPath(sceneGuid)).name == sceneName);
                Scene scene;
                if (!foundScene)
                {
                    var baseSettings = AddressableAssetSettingsDefaultObject.Settings;
                    var worldGroup = baseSettings.FindGroup("World");
                    if (worldGroup == null)
                        worldGroup = baseSettings.CreateGroup("World", false, false, false,
                            baseSettings.DefaultGroup.Schemas);
                    scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                    scene.name = sceneName;
                    EditorSceneManager.SaveScene(scene, $"Assets/Scenes/{sceneName}.unity");
                    var guid = AssetDatabase.AssetPathToGUID(scene.path);
                    var entry = baseSettings.CreateOrMoveEntry(guid, worldGroup);
                    entry.address = sceneName;
                    baseSettings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
                    AssetDatabase.SaveAssets();
                }
                else
                {
                    scene = SceneManager.GetSceneByName(sceneName);
                    if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(scene.path);
                }
                
                mapParent = GetMapParent(scene);
                if (mapParent == null)
                {
                    mapParent = new GameObject
                    {
                        name = "Map",
                        transform =
                        {
                            position = center
                        }
                    };
                    SceneManager.MoveGameObjectToScene(mapParent, scene);
                }

                _mapParents.Add(sceneName, mapParent);
            }

            var quadPos = GetQuadPosition(pos, pos.y);
            var x = Mathf.RoundToInt(quadPos.x / 50f);
            var z = Mathf.RoundToInt(quadPos.z / 50f);
            var quad = mapParent.transform.Find($"{x},{z}")?.gameObject;
            if (quad != null) return quad.transform;
            quad = new GameObject {name = $"{x},{z}", transform = {position = quadPos}};
            quad.transform.SetParent(mapParent.transform);
            return quad.transform;
        }
        
        public override void SetBoundsVisualization(Vector3Int target, int gridHeight)
        {
            var originalColor = Handles.color;
            Handles.color = Color.cyan * 0.5f;
            var center = GetSectionPosition(target, gridHeight);
            Handles.DrawWireCube(center, new Vector3(100, 0, 100));
            var quad = GetQuadPosition(target, gridHeight);
            Handles.color = Color.cyan * 0.35f;
            Handles.DrawWireCube(quad, new Vector3(50, 0, 50));
            Handles.color = originalColor;
        }
        
        private static Vector3 GetSectionPosition(Vector3Int target, int gridHeight) =>
            BlockyUtilities.SnapToGrid(target - BlockyEditorWindow.GridOffset, gridHeight, 100) + BlockyEditorWindow.GridOffset;

        private static Vector3 GetQuadPosition(Vector3Int target, int gridHeight) =>
            BlockyUtilities.SnapToGrid(target - new Vector3(25.5f, 0, 25.5f), gridHeight, 50) + new Vector3(25.5f, 0, 25.5f);
    }
}