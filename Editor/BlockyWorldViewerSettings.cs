using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeartreeGames.BlockyWorldStreamer.Editor
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
        public int day;
        public List<Scene> scenes;
        [NonSerialized] public Scene Selected;
    }
}