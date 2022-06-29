using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace PeartreeGames.BlockyWorldStreamer.Editor
{
    public class BlockyWorldViewer : EditorWindow
    {
        private const int Size = 512;
        [SerializeField] private BlockyWorldViewerSettings settings;
        private SerializedObject serializedSettings;

        [MenuItem("Tools/Blocky/WorldViewer")]
        private static void ShowWindow()
        {
            var window = GetWindow<BlockyWorldViewer>();
            window.titleContent = new GUIContent("BlockyWorldViewer");
            window.Show();
        }

        private void OnEnable()
        {
            if (settings == null) CreateSettings();
            EditorSceneManager.activeSceneChangedInEditMode += OnSceneHierarchyChanged;
            EditorSceneManager.sceneOpened += OnSceneHierarchyChanged;
            EditorSceneManager.sceneClosed += OnSceneHierarchyChanged;
            rootVisualElement.styleSheets.Add(Resources.Load<StyleSheet>("BlockyWorldViewer"));
            serializedSettings = new SerializedObject(settings);
            var cameraMaskField = new PropertyField(serializedSettings.FindProperty("cameraMask"));
            cameraMaskField.Bind(serializedSettings);
            rootVisualElement.Add(cameraMaskField);
            var dayField = new PropertyField(serializedSettings.FindProperty("day"));
            dayField.Bind(serializedSettings);
            rootVisualElement.Add(dayField);

            var openGroup = new GroupBox();
            openGroup.AddToClassList("horizontal");
            var single = new Button(() =>
            {
                if (settings.selected == null) return;
                EditorSceneManager.OpenScene(settings.selected.reference.ScenePath, OpenSceneMode.Single);
                RefreshWorldGrid();
            }) {text = "Open Scene Single"};
            single.AddToClassList("grow");
            openGroup.Add(single);
            var add = new Button(() =>
            {
                if (settings.selected == null) return;
                EditorSceneManager.OpenScene(settings.selected.reference.ScenePath, OpenSceneMode.Additive);
                RefreshWorldGrid();
            }) {text = "Open Scene Additive"};
            add.AddToClassList("grow");
            openGroup.Add(add);
            rootVisualElement.Add(openGroup);

            var dayGroup = new GroupBox();
            dayGroup.AddToClassList("horizontal");
            var openDay = new Button(() =>
            {
                if (settings.selected == null) return;
                var sceneName = $"{settings.selected.reference.SceneName}_{settings.day:000}";
                if (!SceneManager.GetSceneByName(sceneName).IsValid()) return;
                for (var i = 0; i < EditorSceneManager.loadedSceneCount; i++)
                {
                    var openScene = SceneManager.GetSceneAt(i);
                    if (openScene.name.StartsWith(settings.selected.reference.SceneName) && openScene.name != settings.selected.reference.SceneName)
                        EditorSceneManager.CloseScene(openScene, true);
                }
                EditorSceneManager.OpenScene(sceneName);
            }) {text = "Open Day"};
            openDay.AddToClassList("grow");
            
            var newDay = new Button(() =>
            {
                if (settings.selected == null) return;
                var sceneName = $"{settings.selected.reference.SceneName}_{settings.day:000}";
                if (SceneManager.GetSceneByName(sceneName).IsValid()) return;
                for (var i = 0; i < EditorSceneManager.loadedSceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    if (scene.name.StartsWith(settings.selected.reference.SceneName) && scene.name != settings.selected.reference.SceneName)
                        EditorSceneManager.CloseScene(scene, true);
                }
                var newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                newScene.name = sceneName;
                EditorSceneManager.SaveScene(newScene);
            }) {text = "New Day"};
            newDay.AddToClassList("grow");
            dayGroup.Add(openDay);
            dayGroup.Add(newDay);
            rootVisualElement.Add(dayGroup);
            
            rootVisualElement.Add(new Button(() =>
            {
                EditorCoroutineUtility.StartCoroutineOwnerless(TakeScreenshot(SceneManager.GetActiveScene()));
                RefreshWorldGrid();
            }) {text = "Screenshot Active Scene"});
            rootVisualElement.Add(new Button(() =>
            {
                EditorCoroutineUtility.StartCoroutineOwnerless(TakeAllScreenshots());
            }) {text = "Screenshot All Scene"});
            RefreshWorldGrid();
        }

        private void OnDisable()
        {
            EditorSceneManager.activeSceneChangedInEditMode -= OnSceneHierarchyChanged;
            EditorSceneManager.sceneOpened -= OnSceneHierarchyChanged;
            EditorSceneManager.sceneClosed -= OnSceneHierarchyChanged;
        }

        private void OnSceneHierarchyChanged(Scene scene) => RefreshWorldGrid();
        private void OnSceneHierarchyChanged(Scene scene, OpenSceneMode mode) => OnSceneHierarchyChanged(scene);
        private void OnSceneHierarchyChanged(Scene current, Scene next) => RefreshWorldGrid();

        private void CreateSettings()
        {
            var assets = AssetDatabase.FindAssets($"t:{typeof(BlockyWorldViewerSettings)}");
            if (assets.Length > 0)
                settings = AssetDatabase.LoadAssetAtPath<BlockyWorldViewerSettings>(
                    AssetDatabase.GUIDToAssetPath(assets[0]));
            else
            {
                settings = CreateInstance<BlockyWorldViewerSettings>();
                settings.scenes = new List<BlockyWorldViewerSettings.Scene>();
                settings.cameraMask = -1;
                AssetDatabase.CreateAsset(settings, "Assets/BlockyWorldViewerSettings.asset");
            }
        }

        private void RefreshWorldGrid()
        {
            var scroll = new ScrollView
            {
                name = "Grid",
                horizontalScrollerVisibility = ScrollerVisibility.AlwaysVisible,
                verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible
            };
            scroll.AddToClassList("preview-scroll");
            var prev = rootVisualElement.Q("Grid");
            if (prev != null) rootVisualElement.Remove(prev);

            var container = new GroupBox();
            container.AddToClassList("preview-container");
            scroll.Add(container);
            rootVisualElement.Insert(5, scroll);
            CreatePreviewButtons(container);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            container.MarkDirtyRepaint();
        }

        private IEnumerator TakeAllScreenshots()
        {
            var loadedScenes = new List<string>();
            var active = SceneManager.GetActiveScene().path;
            for (var i = 0; i < SceneManager.sceneCount; ++i)
            {
                loadedScenes.Add(SceneManager.GetSceneAt(i).path);
            }

            var assets = AssetDatabase.FindAssets("t:Scene");
            foreach (var asset in assets)
            {
                var path = AssetDatabase.GUIDToAssetPath(asset);
                var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
                if (!sceneAsset.name.StartsWith(BlockyWorldUtilities.ScenePrefix)) continue;
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                SceneManager.SetActiveScene(scene);
                yield return TakeScreenshot(scene);
            }

            for (var i = 0; i < loadedScenes.Count; i++)
            {
                var loadedScene = loadedScenes[i];
                var scene = EditorSceneManager.OpenScene(loadedScene,
                    i == 0 ? OpenSceneMode.Single : OpenSceneMode.Additive);
                if (loadedScene == active) SceneManager.SetActiveScene(scene);
            }

            RefreshWorldGrid();
        }

        private void CreatePreviewButtons(VisualElement container)
        {
            var buttons = new List<Button>();
            var minX = int.MaxValue;
            var maxX = int.MinValue;
            var minY = int.MaxValue;
            var maxY = int.MinValue;

            foreach (var scene in settings.scenes)
            {
                if (minX > scene.key.x) minX = scene.key.x;
                if (maxX < scene.key.x) maxX = scene.key.x;
                if (minY > scene.key.y) minY = scene.key.y;
                if (maxY < scene.key.y) maxY = scene.key.y;
            }

            var active = SceneManager.GetActiveScene().name;
            for (var x = minX; x <= maxX; x++)
            {
                var row = new VisualElement();
                for (var y = maxY; y >= minY; y--)
                {
                    var key = new Vector2Int(x, y);
                    var viewer = settings.scenes.Find(s => s.key == key);
                    var button = new Button
                    {
                        userData = viewer
                    };
                    buttons.Add(button);
                    button.AddToClassList("preview");
                    if (viewer != null)
                    {
                        if (active == viewer.reference.SceneName) button.AddToClassList("preview-active");
                        else if (SceneManager.GetSceneByName(viewer.reference.SceneName).isLoaded)
                            button.AddToClassList("preview-loaded");

                        if (viewer == settings.selected) button.AddToClassList("preview-selected");
                        var img = new Image {image = viewer.texture};
                        img.AddToClassList("preview-image");
                        button.Add(img);
                        var label = new Label(viewer.key.ToString());
                        label.AddToClassList("label");
                        button.Add(label);
                    }

                    row.Add(button);
                }

                container.Add(row);
            }

            void SelectButton(VisualElement button)
            {
                foreach (var btn in buttons)
                {
                    btn.RemoveFromClassList("preview-selected");
                    var sceneName = (btn.userData as BlockyWorldViewerSettings.Scene)?.reference.SceneName;
                    if (active == sceneName) btn.AddToClassList("preview-active");
                    else if (SceneManager.GetSceneByName(sceneName).isLoaded) btn.AddToClassList("preview-loaded");
                }

                if (settings.selected == button?.userData) settings.selected = null;
                else if (button?.userData != null)
                {
                    settings.selected = button.userData as BlockyWorldViewerSettings.Scene;
                    button.RemoveFromClassList("preview-active");
                    button.RemoveFromClassList("preview-loaded");
                    button.AddToClassList("preview-selected");
                }

                Repaint();
            }

            container.AddManipulator(new Clickable(() => { SelectButton(null); }));
            foreach (var btn in buttons) btn.RegisterCallback<ClickEvent>(_ => SelectButton(btn));
        }

        private IEnumerator TakeScreenshot(Scene scene)
        {
            if (!scene.name.StartsWith(BlockyWorldUtilities.ScenePrefix)) yield break;
            if (settings == null) CreateSettings();
            Undo.RecordObject(settings, "Checked Scene Viewer");
            var key = BlockyWorldUtilities.GetCellFromSceneName(scene.name);
            var viewer = settings.scenes.Find(s => s.key == key);
            if (viewer == null)
            {
                viewer = new BlockyWorldViewerSettings.Scene();
                settings.scenes.Add(viewer);
            }

            viewer.key = key;
            viewer.reference = new BlockySceneReference(AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path));
            var subAssets = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(settings));
            viewer.texture = subAssets.FirstOrDefault(sub => sub is Texture2D && sub.name == scene.name) as Texture2D;
            if (viewer.texture == null)
            {
                viewer.texture = new Texture2D(Size, Size, TextureFormat.ARGB32, false)
                {
                    name = scene.name
                };
                AssetDatabase.AddObjectToAsset(viewer.texture, settings);
            }

            Undo.RecordObject(viewer.texture, "Added Screenshot");
            var light = new GameObject().AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(35, 35, 0);

            var cam = new GameObject().AddComponent<Camera>();
            cam.orthographic = true;
            cam.cullingMask = settings.cameraMask;
            cam.farClipPlane = 2000;
            cam.orthographicSize = 50;
            var w = Screen.height / Screen.width;
            var rect = cam.rect;
            rect.width = w;
            cam.rect = rect;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color();
            cam.transform.position = new Vector3(BlockyWorldUtilities.SceneGridSize * key.x + 0.5f, 500,
                BlockyWorldUtilities.SceneGridSize * key.y + 0.5f);
            cam.transform.rotation = Quaternion.Euler(90, 0, 0);

            var currentRT = RenderTexture.active;
            var rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            RenderTexture.active = rt;
            cam.Render();
            viewer.texture.ReadPixels(new Rect(0, 0, Size, Size), 0, 0, false);
            viewer.texture.Apply(false);

            RenderTexture.active = currentRT;
            DestroyImmediate(cam.gameObject);
            DestroyImmediate(light.gameObject);
            rt.Release();
        }
    }
}