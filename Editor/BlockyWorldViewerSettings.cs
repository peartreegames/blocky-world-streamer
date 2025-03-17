using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PeartreeGames.Blocky.WorldStreamer.Editor
{
    public class BlockyWorldViewerSettings : ScriptableObject
    {
        [Serializable]
        public class Scene
        {
            public Vector2Int key;
            public Texture2D texture;
            public BlockySceneReference reference;
        }

        public LayerMask cameraMask;
        public BlockyWorldKey worldKey;
        public List<Scene> scenes;
        [NonSerialized] public Scene Selected;

#if UNITY_EDITOR
        private void OnValidate()
        {
            var worldScenes = UnityEditor.AssetDatabase.FindAssets("t:Scene")
                .Select(UnityEditor.AssetDatabase.GUIDToAssetPath)
                .Select(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>)
                .Where(s => BlockyWorldUtilities.WorldSceneRegex.IsMatch(s.name))
                .ToList();
            foreach (var s in worldScenes)
            {
                var key = BlockyWorldUtilities.GetCellFromSceneName(s.name);
                if (!s.name.Contains(worldKey.Key)) continue;
                if (scenes.Exists(scene => scene.key == key))
                    continue;
                scenes.Add(new Scene
                {
                    reference = new BlockySceneReference(s),
                    key = key,
                    texture = null
                });
            }

            scenes.RemoveAll(s =>
                worldScenes.TrueForAll(w => w.name != s.reference.SceneName));
        }
#endif
    }
}