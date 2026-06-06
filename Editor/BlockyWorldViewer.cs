using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace PeartreeGames.Blocky.Streamer.Editor
{
    public class BlockyWorldViewer : EditorWindow
    {
        private const int Size = 512;
        [SerializeField] private SceneAsset overworldScene;
        [SerializeField] private BlockyWorldKey worldKey;
        private BlockyWorldKey.Scene _selected;

        [MenuItem("Tools/Blocky/WorldViewer")]
        private static void ShowWindow()
        {
            var window = GetWindow<BlockyWorldViewer>();
            window.titleContent = new GUIContent("BlockyWorldViewer");
            window.Show();
        }

        private static string GetScenePath(string sceneName)
        {
            var sceneGuids = AssetDatabase.FindAssets("t:Scene");
            var guid = sceneGuids.FirstOrDefault(sceneGuid =>
                AssetDatabase.LoadAssetAtPath<SceneAsset>(AssetDatabase.GUIDToAssetPath(sceneGuid))
                    ?.name == sceneName);
            return guid == null ? null : AssetDatabase.GUIDToAssetPath(guid);
        }

        private void OnEnable()
        {
            EditorCoroutineUtility.StartCoroutineOwnerless(Open());
        }

        private IEnumerator Open()
        {
            yield return new EditorWaitForSeconds(0.5f);
            if (SceneManager.sceneCount == 0) yield break;
            while (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode ||
                   EditorApplication.isCompiling) yield return null;
            EditorApplication.playModeStateChanged += OnPlayModeChange;
            Subscribe();
            rootVisualElement.styleSheets.Add(Resources.Load<StyleSheet>("BlockyWorldViewer"));

            var overworld = new ObjectField("Orchestrator")
            {
                objectType = typeof(SceneAsset),
                value = overworldScene
            };
            overworld.RegisterValueChangedCallback(v => overworldScene = (SceneAsset)v.newValue);
            rootVisualElement.Add(overworld);

            var dropdown = new PopupField<BlockyWorldKey>("WorldKey", GetKeyChoices(), 0, 
                key => key == null ? "None" : key.name,
                key => key == null ? "None" : key.name)
            {
                value = worldKey
            };

            dropdown.RegisterCallback<MouseDownEvent>(_ =>
            {
                var choices = GetKeyChoices();
                dropdown.choices = choices;
            });
            
            dropdown.RegisterValueChangedCallback(v =>
            {
                worldKey = v.newValue;
                RefreshWorldGrid();
            });
            rootVisualElement.Add(dropdown);

            var openGroup = new GroupBox();
            openGroup.AddToClassList("horizontal");
            var single = new Button(() =>
            {
                if (_selected?.reference == null) return;
                EditorSceneManager.SaveOpenScenes();
                EditorSceneManager.OpenScene(_selected.reference.ScenePath, OpenSceneMode.Single);
                if (overworldScene != null)
                {
                    var main = EditorSceneManager.OpenScene(AssetDatabase.GetAssetPath(overworldScene), OpenSceneMode.Additive);
                    SceneManager.SetActiveScene(main);
                }
                RefreshWorldGrid();
            }) { text = "Open Scene Single" };
            single.AddToClassList("grow");
            openGroup.Add(single);
            var add = new Button(() =>
            {
                if (_selected?.reference == null) return;
                EditorSceneManager.OpenScene(_selected.reference.ScenePath,
                    OpenSceneMode.Additive);
                RefreshWorldGrid();
            }) { text = "Open Scene Additive" };
            add.AddToClassList("grow");
            openGroup.Add(add);
            rootVisualElement.Add(openGroup);
            rootVisualElement.Add(new Button(() =>
            {
                if (_selected?.reference == null) return;
                EditorCoroutineUtility.StartCoroutineOwnerless(
                    TakeScreenshot(
                        SceneManager.GetSceneByPath(_selected.reference.ScenePath)));
            }) { text = "Screenshot Selected Scene" });

            var refresh = new Button(RefreshWorldGrid) { text = "Refresh" };
            rootVisualElement.Add(refresh);
            RefreshWorldGrid();
            yield break;

            List<BlockyWorldKey> GetKeyChoices()
            {
                var worldKeys = AssetDatabase.FindAssets("t:BlockyWorldKey");
                return worldKeys.Select(AssetDatabase.GUIDToAssetPath)
                    .Select(AssetDatabase.LoadAssetAtPath<BlockyWorldKey>).ToList();
            }
        }

        private void OnPlayModeChange(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                    Unsubscribe();
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    Subscribe();
                    break;
            }
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChange;
        }


        private void Subscribe()
        {
            EditorSceneManager.activeSceneChangedInEditMode += OnSceneHierarchyChanged;
            EditorSceneManager.sceneOpened += OnSceneHierarchyChanged;
            EditorSceneManager.sceneClosed += OnSceneHierarchyChanged;
        }

        private void Unsubscribe()
        {
            EditorSceneManager.activeSceneChangedInEditMode -= OnSceneHierarchyChanged;
            EditorSceneManager.sceneOpened -= OnSceneHierarchyChanged;
            EditorSceneManager.sceneClosed -= OnSceneHierarchyChanged;
        }

        private void OnSceneHierarchyChanged(Scene scene) => RefreshWorldGrid();

        private void OnSceneHierarchyChanged(Scene scene, OpenSceneMode mode) =>
            OnSceneHierarchyChanged(scene);

        private void OnSceneHierarchyChanged(Scene current, Scene next) => RefreshWorldGrid();

        private void RefreshWorldGrid()
        {
            if (worldKey == null) return;
            var prev = rootVisualElement.Q("Grid");
            if (prev != null) rootVisualElement.Remove(prev);

            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            var scroll = new ScrollView
            {
                name = "Grid",
                horizontalScrollerVisibility = ScrollerVisibility.AlwaysVisible,
                verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible
            };
            scroll.AddToClassList("preview-scroll");

            var container = new GroupBox();
            container.AddToClassList("preview-container");
            scroll.Add(container);
            rootVisualElement.Insert(4, scroll);
            CreatePreviewButtons(container);
            container.MarkDirtyRepaint();
        }

        private void CreatePreviewButtons(VisualElement container)
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            var buttons = new List<Button>();
            var minX = int.MaxValue;
            var maxX = int.MinValue;
            var minY = int.MaxValue;
            var maxY = int.MinValue;

            foreach (var scene in worldKey.scenes)
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
                    var viewer = worldKey.scenes.Find(s => s.key == key);
                    var button = new Button
                    {
                        userData = viewer
                    };
                    buttons.Add(button);
                    button.AddToClassList("preview");
                    if (viewer != null && viewer.reference != null)
                    {
                        if (active == viewer.reference.SceneName)
                            button.AddToClassList("preview-active");
                        else if (SceneManager.GetSceneByName(viewer.reference.SceneName).isLoaded)
                            button.AddToClassList("preview-loaded");

                        if (viewer == _selected) button.AddToClassList("preview-selected");
                        var img = new Image { image = viewer.texture };
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
                    var sceneName = (btn.userData as BlockyWorldKey.Scene)?.reference?.SceneName;
                    if (string.IsNullOrEmpty(sceneName)) continue;
                    if (active == sceneName) btn.AddToClassList("preview-active");
                    else if (SceneManager.GetSceneByName(sceneName).isLoaded)
                        btn.AddToClassList("preview-loaded");
                }

                if (_selected == button?.userData) _selected = null;
                else if (button?.userData != null)
                {
                    _selected = button.userData as BlockyWorldKey.Scene;
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
            if (!scene.name.StartsWith(BlockyWorldUtilities.ScenePrefix))
            {
                Debug.LogWarning($"{scene.name} is not valid");
                yield break;
            }
            yield return new EditorWaitForSeconds(0.1f);
            var key = BlockyWorldUtilities.GetCellFromSceneName(scene.name);
            var viewerScene = worldKey.scenes.Find(s => s.key == key);
            if (viewerScene == null)
            {
                viewerScene = new BlockyWorldKey.Scene();
                worldKey.scenes.Add(viewerScene);
            }

            viewerScene.key = key;
            viewerScene.reference =
                new BlockySceneReference(AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path));
            EditorUtility.SetDirty(worldKey);
            var texture = new Texture2D(Size, Size, TextureFormat.ARGB32, false)
            {
                name = scene.name
            };

            var light = new GameObject().AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(35, 35, 0);

            var cam = new GameObject().AddComponent<Camera>();
            cam.orthographic = true;
            cam.cullingMask = worldKey.cameraMask;
            cam.farClipPlane = 2000;
            cam.orthographicSize = 50;

            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color();
            cam.transform.position = new Vector3(BlockyWorldUtilities.SceneGridSize * key.x + 0.5f,
                500,
                BlockyWorldUtilities.SceneGridSize * key.y + 0.5f);
            cam.transform.rotation = Quaternion.Euler(90, 0, 0);

            var currentRT = RenderTexture.active;
            var rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
            try
            {
                RenderTexture.active = rt;
                cam.targetTexture = rt;
                cam.Render();
                texture.ReadPixels(new Rect(0, 0, Size, Size), 0, 0, false);
                texture.Apply(false);

                var subAssets = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(worldKey));
                foreach (var asset in subAssets)
                {
                    if (asset != null && asset.name == texture.name) AssetDatabase.RemoveObjectFromAsset(asset);
                }

                AssetDatabase.AddObjectToAsset(texture, worldKey);
                viewerScene.texture = texture;
                AssetDatabase.SaveAssets();
            }
            finally
            {
                cam.targetTexture = null;
                RenderTexture.active = currentRT;
                rt.Release();
                DestroyImmediate(rt);
                DestroyImmediate(cam.gameObject);
                DestroyImmediate(light.gameObject);
            }
            yield return new EditorWaitForSeconds(0.1f);
            RefreshWorldGrid();
        }
    }
}