using System.Collections.Generic;
using System.Linq;
using PeartreeGames.BlockyWorldEditor.Editor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PeartreeGames.BlockyWorldStreamer.Editor
{
    public class BlockyWorldStreamerScenePreprocessor : IBlockyScenePreprocessor
    {
        public int Order => 0;

        public void ProcessScene(BlockyEditorWindow window, Scene scene)
        {
            var mapParent = BlockyWorldStreamerParentSetter.GetMapParent(scene, "Map");
            if (mapParent == null) return;
            ProcessObject(mapParent);
        }

        private static void ProcessObject(GameObject go)
        { 
            var toAddList = new List<GameObject>();
            foreach (Transform child in go.transform)
            {
                var colliders = BlockyWorldUtilities.CombineColliders(child.gameObject);
                if (colliders != null) toAddList.Add(colliders);
            }

            foreach (var toAdd in toAddList) toAdd.transform.SetParent(go.transform);
        }

        public void RevertScene(BlockyEditorWindow window, Scene scene) => RevertScene(scene);

        public static void RevertScene(Scene scene)
        {
            var mapParent = BlockyWorldStreamerParentSetter.GetMapParent(scene, "Map");
            if (mapParent == null) return;
            RevertObject(mapParent);
        }

        private static void RevertObject(GameObject obj)
        {
            var toDestroyList = new List<GameObject>();
            
            foreach (Transform child in obj.transform)
            {
                if (BlockyWorldUtilities.ExceptionNames.Any(e => child.gameObject.name.Contains(e))) toDestroyList.Add(child.gameObject);
                else
                {
                    child.gameObject.SetActive(true);
                    if (child.transform.childCount > 0 && child.transform.GetChild(0) != null) child.transform.GetChild(0).gameObject.SetActive(true);
                }
            }

            foreach (var toDestroy in toDestroyList) Object.DestroyImmediate(toDestroy);
        }
    }
}