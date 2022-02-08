﻿using System.Collections.Generic;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PeartreeGames.BlockyWorldStreamer.Editor
{
    public class BlockyWorldSceneProcessor : IProcessSceneWithReport
    {
        public int callbackOrder { get; }
        public void OnProcessScene(Scene scene, BuildReport report)
        {
            var mapParent = BlockyWorldStreamerParentSetter.GetMapParent(scene, "Map");
            if (mapParent == null) return;
            for(var i = mapParent.transform.childCount - 1; i >= 0; i--)
            {
                var child = mapParent.transform.GetChild(i);
                if (child.gameObject.name.Contains("Combined")) continue;
                Object.DestroyImmediate(child.gameObject);
            }
        }
    }
}