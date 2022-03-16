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
using Random = UnityEngine.Random;

namespace PeartreeGames.BlockyWorldStreamer.Editor
{
    public class BlockyWorldStreamerParentSetter : BlockyParentSetter
    {
        [SerializeField] private bool randomHeight;
        [SerializeField] private Vector2Int currentCell;
        private static readonly Dictionary<string, GameObject> MapParents = new();
        private Collider[] boxResults;

        private void OnEnable()
        {
            MapParents.Clear();
            boxResults = new Collider[2];
        }

        [BlockyButton]
        public void LoadNeighbours()
        {
            var active = SceneManager.GetActiveScene();
            if (!active.name.StartsWith("world_"))
            {
                Debug.LogWarning($"{active.name} scene is not a world scene");
                return;
            }

            var cell = BlockyWorldUtilities.GetCellFromSceneName(active.name);
            var neighbours = BlockyWorldUtilities.GetNeighbouringCells(cell);
            var sceneGuids = AssetDatabase.FindAssets("t:Scene");

            foreach (var neighbour in neighbours)
            {
                var sceneName = BlockyWorldUtilities.GetScenePathFromCell(neighbour);
                var foundScene = sceneGuids.FirstOrDefault(sceneGuid =>
                    GetSceneNameFromPath(AssetDatabase.GUIDToAssetPath(sceneGuid)) == sceneName);
                if (foundScene == string.Empty) continue;
                if (SceneManager.GetSceneByName(sceneName).isLoaded) continue;
                EditorSceneManager.OpenScene($"Assets/Scenes/World/{sceneName}.unity", OpenSceneMode.Additive);
            }
        }

        public static GameObject GetMapParent(Scene scene, string name)
        {
            var roots = scene.GetRootGameObjects().ToList();
            return roots.Find(root => root.name == name);
        }

        public static string GetSceneNameFromPath(string path)
        {
            var split = path.Split('/');
            var file = split[^1];
            if (!file.EndsWith(".unity")) return null;
            var name = file.Split('.')[0];
            return name;
        }
        

        public override Transform GetParent(BlockyObject block)
        {
            if (randomHeight)
            {
                var offset = Vector3.zero;
                var overlaps = Physics.OverlapBoxNonAlloc(block.transform.position + new Vector3(0, -0.5f, 0), new Vector3(0.2f, 0.8f, 0.2f),
                    boxResults, Quaternion.identity, LayerMask.GetMask("Ground"));
                switch (overlaps)
                {
                    case 0 when Random.Range(0f, 1f) > 0.85f:
                        offset.y = Random.Range(-1, 2) / 10f;
                        break;
                    case > 0:
                    {
                        var lowest = boxResults[0].transform.position;
                        for (var i = 1; i < overlaps; i++)
                        {
                            var next = boxResults[i].transform.position;
                            if (lowest.y > next.y) lowest = next;
                        }

                        var diff = Mathf.RoundToInt(lowest.y * 10 % 10);
                        if (diff != 0) offset.y = diff > 5 ? -0.1f : 0.1f;
                        break;
                    }
                }
                block.transform.position += offset;
            }
            return GetParent(block, "Map", "World");
        }

        public static Transform GetParent(BlockyObject block, string mapParentName, string assetGroupName)
        {
            var position = block.transform.position;
            var pos = Vector3Int.RoundToInt(position);
            var center = BlockyWorldUtilities.GetCellPosition(pos);
            var cell = BlockyWorldUtilities.GetCellFromWorldPosition(position);
            var sceneName = BlockyWorldUtilities.GetScenePathFromCell(cell);
            if (!MapParents.TryGetValue($"{sceneName}_{mapParentName}", out var mapParent))
            {
                var sceneGuids = AssetDatabase.FindAssets("t:Scene");
                var foundScene = sceneGuids.Any(sceneGuid =>
                    GetSceneNameFromPath(AssetDatabase.GUIDToAssetPath(sceneGuid)) == sceneName);
                Scene scene;
                if (!foundScene)
                {
                    var baseSettings = AddressableAssetSettingsDefaultObject.Settings;
                    var group = baseSettings.FindGroup(assetGroupName);
                    if (group == null)
                        group = baseSettings.CreateGroup(assetGroupName, false, false, false,
                            baseSettings.DefaultGroup.Schemas);
                    scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                    scene.name = sceneName;
                    EditorSceneManager.SaveScene(scene, $"Assets/Scenes/World/{sceneName}.unity");
                    var guid = AssetDatabase.AssetPathToGUID(scene.path);
                    var entry = baseSettings.CreateOrMoveEntry(guid, group);
                    entry.address = sceneName;
                    baseSettings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
                    AssetDatabase.SaveAssets();
                }
                else
                {
                    scene = SceneManager.GetSceneByName(sceneName);
                    if (!scene.isLoaded)
                        scene = EditorSceneManager.OpenScene($"Assets/Scenes/World/{sceneName}.unity",
                            OpenSceneMode.Additive);
                    BlockyWorldStreamerScenePreprocessor.RevertScene(scene);
                }

                mapParent = GetMapParent(scene, mapParentName);
                if (mapParent == null)
                {
                    mapParent = new GameObject
                    {
                        name = mapParentName,
                        transform =
                        {
                            position = center
                        }
                    };
                    SceneManager.MoveGameObjectToScene(mapParent, scene);
                }

                MapParents.Add($"{sceneName}_{mapParentName}", mapParent);
            }

            var quadPos = BlockyWorldUtilities.GetQuadPosition(pos);
            var x = Mathf.RoundToInt((quadPos.x - center.x) / BlockyWorldUtilities.SceneQuadSize);
            var z = Mathf.RoundToInt((quadPos.z - center.z) / BlockyWorldUtilities.SceneQuadSize);
            var quad = mapParent.transform.Find($"{x},{z}")?.gameObject;
            if (quad != null) return quad.transform;
            quad = new GameObject {name = $"{x},{z}", transform = {position = quadPos}};
            quad.transform.SetParent(mapParent.transform);
            return quad.transform;
        }

        public override void SetVisualization(Vector3Int target, int gridHeight)
        {
            currentCell = BlockyWorldUtilities.GetCellFromWorldPosition(target);
            SetVisualization(target, gridHeight, Color.cyan * 0.5f, Color.cyan * 0.35f);
        }

        public static void SetVisualization(Vector3Int target, int gridHeight, Color primaryColor, Color secondaryColor)
        {
            var originalColor = Handles.color;
            Handles.color = primaryColor;
            var center = BlockyWorldUtilities.GetCellPosition(target);
            center.y = gridHeight;
            Handles.DrawWireCube(center,
                new Vector3(BlockyWorldUtilities.SceneGridSize, 0, BlockyWorldUtilities.SceneGridSize));
            var quad = BlockyWorldUtilities.GetQuadPosition(target);
            quad.y = gridHeight;
            Handles.color = secondaryColor;
            Handles.DrawWireCube(quad,
                new Vector3(BlockyWorldUtilities.SceneQuadSize, 0, BlockyWorldUtilities.SceneQuadSize));
            Handles.color = originalColor;
        }
    }
}