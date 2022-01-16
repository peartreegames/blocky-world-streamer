using System.Collections.Generic;
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
            var mapParent = BlockyWorldGridParentSetter.GetMapParent(scene);
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