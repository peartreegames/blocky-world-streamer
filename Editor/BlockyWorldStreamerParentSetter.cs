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
    public class BlockyWorldStreamerParentSetter : BlockyParentSetter
    {
        private static readonly Dictionary<string, GameObject> MapParents = new();

        private void OnEnable()
        {
            MapParents.Clear();
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

        public override Transform GetParent(BlockyObject block) => GetParent(block, "Map", "World");

        public static Transform GetParent(BlockyObject block, string mapParentName, string assetGroupName)
        {
            var position = block.transform.position;
            var pos = Vector3Int.RoundToInt(position);
            var center = BlockyWorldUtilities.GetCellPosition(pos);
            var cell = BlockyWorldUtilities.GetCellFromWorldPosition(position);
            var sceneName = BlockyWorldUtilities.GetScenePathFromCell(cell);
            if (!MapParents.TryGetValue(sceneName, out var mapParent))
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
                    if (!scene.isLoaded) scene = EditorSceneManager.OpenScene($"Assets/Scenes/World/{sceneName}.unity", OpenSceneMode.Additive);
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
            var x = Mathf.RoundToInt(quadPos.x / BlockyWorldUtilities.SceneQuadSize);
            var z = Mathf.RoundToInt(quadPos.z / BlockyWorldUtilities.SceneQuadSize);
            var quad = mapParent.transform.Find($"{x},{z}")?.gameObject;
            if (quad != null) return quad.transform;
            quad = new GameObject {name = $"{x},{z}", transform = {position = quadPos}};
            quad.transform.SetParent(mapParent.transform);
            return quad.transform;
        }
        
        public override void SetVisualization(Vector3Int target, int gridHeight) => SetVisualization(target, gridHeight, Color.cyan * 0.5f, Color.cyan * 0.35f);

        public static void SetVisualization(Vector3Int target, int gridHeight, Color primaryColor, Color secondaryColor)
        {
            var originalColor = Handles.color;
            Handles.color = primaryColor;
            var center = BlockyWorldUtilities.GetCellPosition(target);
            Handles.DrawWireCube(center, new Vector3(BlockyWorldUtilities.SceneGridSize, gridHeight, BlockyWorldUtilities.SceneGridSize));
            var quad = BlockyWorldUtilities.GetQuadPosition(target);
            Handles.color = secondaryColor;
            Handles.DrawWireCube(quad, new Vector3(BlockyWorldUtilities.SceneQuadSize, gridHeight, BlockyWorldUtilities.SceneQuadSize));
            Handles.color = originalColor;
        }
    }
}