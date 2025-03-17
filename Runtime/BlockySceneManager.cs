using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PeartreeGames.Blocky.World;
using PeartreeGames.Evt.Variables;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace PeartreeGames.Blocky.Streamer
{
    [DefaultExecutionOrder(-10000)]
    public class BlockySceneManager : MonoBehaviour
    {
        [SerializeField] private AssetReferenceT<EvtTransform> targetRef;
        [SerializeField] private float loadDelay = 2f;
        [SerializeField] private bool debug;
        [SerializeField] private bool quickLoad;
        [SerializeField, Tooltip("First key will be used as default")] private List<BlockyWorldKey> keys;
        private EvtTransform _target;

        public static List<BlockyWorldKey> WorldKeys { get; set; }
        public static BlockyWorldKey WorldKey { get; private set; }

        public List<EvtBool> readyObjects;

        public static Action OnWorldSceneReady;
        public static Action<Scene> OnSceneAlreadyLoaded;
        private Vector2Int CurrentCell { get; set; }
        private Vector2Int NextCell { get; set; }

        private Dictionary<Vector2Int, SceneInstance> _loadedScenes;
        private List<BlockySceneAction> _actions;
        private (Vector2Int cell, IEnumerator co) _loadingCoroutine;
        private float _loadDelay;
        private bool _isLoading;
        private void Awake()
        {
            WorldKeys = keys;
            if (WorldKey == null) WorldKey = keys[0];
#if !DEVELOPMENT_BUILD && !UNITY_EDITOR
            quickLoad = false;
#endif

#if UNITY_EDITOR
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (BlockyWorldUtilities.WorldSceneRegex.IsMatch(scene.name))
                    SceneManager.UnloadSceneAsync(scene);
            }
#endif
            _actions = new List<BlockySceneAction>();
            _loadedScenes = new Dictionary<Vector2Int, SceneInstance>();
        }


#if UNITY_EDITOR
        private void OnValidate()
        {
            keys = AssetDatabase.FindAssets("t:BlockyWorldKey")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<BlockyWorldKey>)
                .Where(k => k != null).ToList();
        }
#endif

        public static void SetKey(string keyName)
        {
            Debug.Assert(WorldKeys != null, "WorldKeys is null");
            var key = WorldKeys.Find(k => k.Name == keyName);
            Debug.Assert(key != null, "WorldKey not found!");
            WorldKey = key;
        }

        private IEnumerator Start()
        {
            enabled = false;
            var targetAo = targetRef.LoadAssetAsync();
            yield return targetAo;
            _target = targetAo.Result;
            while (_target.Value == null) yield return null;
            SceneManager.SetActiveScene(gameObject.scene);

            CurrentCell = BlockyWorldUtilities.GetCellFromWorldPosition(_target.Value.position);

            yield return StartCoroutine(LoadSceneCell(CurrentCell));
            var list = GetNeighbourLoadsAndUnloads(CurrentCell).toLoad.Select(key => LoadSceneCell(key));
            foreach (var load in list)
            {
                yield return load;
            }

            if (!quickLoad)
            {
                while (!readyObjects.TrueForAll(obj => obj.Value))
                    yield return null;

                yield return null;
            }

            enabled = true;
            OnWorldSceneReady?.Invoke();
        }

        private void OnDestroy()
        {
            if (targetRef.IsValid() && targetRef.OperationHandle.IsDone) targetRef.ReleaseAsset();
        }

        private void Update()
        {
            if (_target.Value == null) return;
            var targetCell = BlockyWorldUtilities.GetCellFromWorldPosition(_target.Value.position);
            if (targetCell == CurrentCell) _loadDelay = loadDelay;
            if (targetCell == NextCell) _loadDelay -= Time.deltaTime;
            else NextCell = targetCell;

            if (_loadDelay < 0)
            {
                CurrentCell = targetCell;
                RequestSceneLoad(CurrentCell);
            }

            if (_actions.Count == 0 || _isLoading) return;
            var next = _actions.Max();
            _loadingCoroutine.cell = next.Cell;
            _loadingCoroutine.co = next.Action.Invoke(next.Cell);
            StartCoroutine(_loadingCoroutine.co);
            _actions.Remove(next);
        }


        public void RequestSceneLoad(Vector2Int cell)
        {
            var sceneName = BlockyWorldUtilities.GetSceneNameFromCell(WorldKey.Key, cell);
            var scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.isLoaded) EnqueueLoad(cell, 20);
            else OnSceneAlreadyLoaded?.Invoke(scene);
            EnqueueNeighbours(cell);
        }

        private void EnqueueLoad(Vector2Int cell, int priority = 10)
        {
            if (_loadingCoroutine.cell == cell && _loadingCoroutine.co != null)
                StopCoroutine(_loadingCoroutine.co);
            _actions.RemoveAll(action => action.Cell == cell);
            var action = new BlockySceneAction(cell, LoadSceneCell, priority);
            _actions.Add(action);
        }

        private (HashSet<Vector2Int> toLoad, HashSet<Vector2Int> toUnload)
            GetNeighbourLoadsAndUnloads(Vector2Int cell)
        {
            var toLoad = BlockyWorldUtilities.GetNeighbouringCells(cell).ToHashSet();
            var toUnload = _loadedScenes.Keys.ToHashSet();
            toUnload.ExceptWith(toLoad);
            if (toUnload.Contains(CurrentCell)) toUnload.Remove(CurrentCell);
            return (toLoad, toUnload);
        }

        public void EnqueueNeighbours(Vector2Int cell)
        {
            var (toLoad, toUnload) = GetNeighbourLoadsAndUnloads(cell);
            foreach (var load in toLoad) EnqueueLoad(load, 5);
            foreach (var unload in toUnload) EnqueueUnload(unload);
        }

        private void EnqueueUnload(Vector2Int cell)
        {
            if (_loadingCoroutine.cell == cell && _loadingCoroutine.co != null)
                StopCoroutine(_loadingCoroutine.co);
            _actions.RemoveAll(action => action.Cell == cell);
            _actions.Add(new BlockySceneAction(cell, UnloadSceneCell, 0));
        }

        private IEnumerator UnloadSceneCell(Vector2Int cell)
        {
            _isLoading = true;
            if (_loadedScenes.TryGetValue(cell, out var instance))
            {
                yield return Addressables.UnloadSceneAsync(instance);
                _loadedScenes.Remove(cell);
            }

            _isLoading = false;
        }

        private IEnumerator LoadSceneCell(Vector2Int cell)
        {
            _isLoading = true;
            var sceneName = BlockyWorldUtilities.GetSceneNameFromCell(WorldKey.Key, cell);
            if (SceneManager.GetSceneByName(sceneName).isLoaded || _loadedScenes.ContainsKey(cell))
                goto complete;
            
            #if UNITY_EDITOR
            #endif

            var key = Addressables.LoadResourceLocationsAsync(sceneName);
            yield return key; 
            if (key.Result.Count == 0) goto complete;

            var loadAo = Addressables.LoadSceneAsync(sceneName, LoadSceneMode.Additive, false);
            if (!loadAo.IsValid()) goto complete;
            yield return loadAo;
            if (!_loadedScenes.TryAdd(cell, loadAo.Result))
            {
                Debug.LogError($"Could not add {cell} to _loadedScenes");
            }
            
            yield return loadAo.Result.ActivateAsync();
            complete:
            _isLoading = false;
        }

        private void OnDrawGizmos()
        {
            if (!debug) return;
            var size = new Vector3(BlockyWorldUtilities.SceneGridSize, 0,
                BlockyWorldUtilities.SceneGridSize);
            Gizmos.color = Color.cyan;
            var neighbours = BlockyWorldUtilities.GetNeighbouringCells(CurrentCell);
            for (var x = -25; x < 25; x++)
            {
                for (var y = -25; y < 25; y++)
                {
                    var cell = new Vector2Int(x, y);
                    var pos = new Vector3(x * BlockyWorldUtilities.SceneGridSize, 0,
                        y * BlockyWorldUtilities.SceneGridSize);
                    Gizmos.DrawWireCube(pos, size);
                    if (CurrentCell == cell)
                    {
                        Gizmos.color = Color.cyan * 0.5f;
                        Gizmos.DrawCube(pos, size);
                    }

                    if (neighbours.Contains(cell))
                    {
                        Gizmos.color = Color.cyan * 0.25f;
                        Gizmos.DrawCube(pos, size);
                    }

                    Gizmos.color = Color.cyan;
                }
            }

            if (_target.Value == null) return;
            Gizmos.color = new Color(0.4f, 1, 0.4f, 1);
            Gizmos.DrawSphere(_target.Value.position, 3);
        }
    }
}