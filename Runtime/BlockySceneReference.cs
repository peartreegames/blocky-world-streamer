﻿using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
#if UNITY_EDITOR
#endif

namespace PeartreeGames.Blocky.Streamer
{
    [Serializable]
    public class BlockySceneReference : AssetReference, IEquatable<BlockySceneReference>
    {
        [SerializeField] private string sceneName;
        [SerializeField] private string scenePath;
        public string SceneName => sceneName;
        public string ScenePath => scenePath;

#if UNITY_EDITOR
        public BlockySceneReference(SceneAsset scene)
            : base(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(scene)))
        {
            sceneName = scene.name;
            scenePath = AssetDatabase.GetAssetPath(scene);
        }

        public override bool ValidateAsset(string path)
        {
            return ValidateAsset(AssetDatabase.LoadAssetAtPath<SceneAsset>(path));
        }

        public override bool ValidateAsset(UnityEngine.Object obj)
        {
            if (obj == null || obj is not SceneAsset scene) return false;
            sceneName = scene.name;
            scenePath = AssetDatabase.GetAssetPath(scene);
            return true;

        }

        public override bool SetEditorAsset(UnityEngine.Object value)
        {
            if (!base.SetEditorAsset(value))
                return false;

            if (value is SceneAsset scene)
            {
                sceneName = scene.name;
                scenePath = AssetDatabase.GetAssetPath(scene);
                return true;
            }

            sceneName = string.Empty;
            scenePath = string.Empty;
            return false;
        }

#endif
        public bool Equals(BlockySceneReference other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return sceneName == other.sceneName;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.GetType() == GetType() && Equals((BlockySceneReference) obj);
        }

        public override int GetHashCode() => sceneName != null ? sceneName.GetHashCode() : 0;
    }
}