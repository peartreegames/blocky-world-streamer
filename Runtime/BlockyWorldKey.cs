using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PeartreeGames.Blocky.Streamer
{
    [CreateAssetMenu(fileName = "bWorld_", menuName = "Blocky/World Key", order = 0)]
    public class BlockyWorldKey : ScriptableObject
    {
        [field: SerializeField] public string Key { get; private set; }

        [Serializable]
        public class Scene
        {
            public Vector2Int key;
            public Texture2D texture;
            public BlockySceneReference reference;
        }

        public LayerMask cameraMask;
        public List<Scene> scenes;
        public Dictionary<Vector2Int, BlockySceneReference> Scenes;

        private void OnEnable()
        {
            Scenes = new Dictionary<Vector2Int, BlockySceneReference>(scenes?.Count ?? 0);
            if (scenes == null) return;
            foreach (var s in scenes)
            {
                if (s?.reference == null) continue;
                if (!Scenes.TryAdd(s.key, s.reference))
                    Debug.LogError($"BlockyWorldKey '{Key}': duplicate cell {s.key} — only the first entry will load.", this);
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Refresh Scenes From Project")]
        private void RefreshScenes()
        {
            var worldScenes = UnityEditor.AssetDatabase.FindAssets("t:Scene")
                .Select(UnityEditor.AssetDatabase.GUIDToAssetPath)
                .Select(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>)
                .Where(s => s != null)
                .Where(s => BlockyWorldUtilities.WorldSceneRegex.IsMatch(s.name))
                .Where(s => s.name.Contains(Key))
                .ToList();

            var validNames = new HashSet<string>(worldScenes.Select(s => s.name));
            scenes ??= new List<Scene>();
            var removed = scenes.RemoveAll(s => s?.reference == null || !validNames.Contains(s.reference.SceneName));
            var added = 0;
            foreach (var s in worldScenes)
            {
                var key = BlockyWorldUtilities.GetCellFromSceneName(s.name);
                if (scenes.Exists(scene => scene.key == key)) continue;
                scenes.Add(new Scene
                {
                    reference = new BlockySceneReference(s),
                    key = key,
                    texture = null
                });
                added++;
            }

            if (removed > 0 || added > 0)
            {
                UnityEditor.EditorUtility.SetDirty(this);
                Debug.Log($"BlockyWorldKey '{Key}': +{added} added, -{removed} removed.", this);
            }
        }
#endif
    }
}
