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

namespace Editor
{
    public class BlockyWorldGridParentSetter : BlockyParentSetter
    {
        private Dictionary<string, GameObject> _mapParents;

        public void OnEnable()
        {
            _mapParents = new Dictionary<string, GameObject>();
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

                var roots = scene.GetRootGameObjects().ToList();
                mapParent = roots.Find(root => root.name == "Map");

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