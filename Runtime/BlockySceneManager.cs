using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace PeartreeGames.Blocky.Streamer
{
    [DefaultExecutionOrder(-10000)]
    public class BlockySceneManager : MonoBehaviour
    {
        [SerializeField] private float loadDelay = 2f;
        [SerializeField, Min(0)] private int gizmoRadius = 3;

        public static Action<Scene> OnSceneAlreadyLoaded;

        private float _loadDelay;
        private bool _isLoading;

        public Transform Target { get; set; }
        private BlockyWorldKey _worldKey;
        private Vector2Int _currentCell;
        private Vector2Int _nextCell;
        private Dictionary<Vector2Int, SceneInstance> _loadedScenes;
        private List<BlockySceneAction> _actions;

        private void Awake()
        {
            _actions = new List<BlockySceneAction>();
            _loadedScenes = new Dictionary<Vector2Int, SceneInstance>();
            _loadDelay = loadDelay;
        }

        private void Start()
        {
#if UNITY_EDITOR
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (BlockyWorldUtilities.WorldSceneRegex.IsMatch(scene.name))
                    SceneManager.UnloadSceneAsync(scene);
            }
#endif
            SceneManager.SetActiveScene(gameObject.scene);
        }

        public IEnumerator Load(BlockyWorldKey key, Transform target)
        {
            enabled = false;
            StopAllCoroutines();
            _isLoading = false;

            _worldKey = key;
            Target = target;
            _actions.Clear();

            foreach (var instance in _loadedScenes.Values)
                yield return Addressables.UnloadSceneAsync(instance);
            _loadedScenes.Clear();

            _currentCell = BlockyWorldUtilities.GetCellFromWorldPosition(Target.position);
            _nextCell = _currentCell;
            _loadDelay = loadDelay;

            yield return StartCoroutine(LoadSceneCell(_currentCell));
            var list = GetNeighbourLoadsAndUnloads(_currentCell).toLoad.Select(LoadSceneCell);
            foreach (var load in list)
            {
                yield return load;
            }

            enabled = true;
        }

        private void Update()
        {
            if (Target == null || _worldKey == null) return;
            var targetCell = BlockyWorldUtilities.GetCellFromWorldPosition(Target.position);
            if (targetCell == _currentCell) _loadDelay = loadDelay;
            if (targetCell == _nextCell) _loadDelay -= Time.deltaTime;
            else _nextCell = targetCell;

            if (_loadDelay < 0)
            {
                _currentCell = targetCell;
                RequestSceneLoad(_currentCell);
            }

            if (_actions.Count == 0 || _isLoading) return;
            var next = _actions.Max();
            StartCoroutine(next.Action.Invoke(next.Cell));
            _actions.Remove(next);
        }


        private void RequestSceneLoad(Vector2Int cell)
        {
            var sceneName = BlockyWorldUtilities.GetSceneNameFromCell(_worldKey.Key, cell);
            var scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.isLoaded) EnqueueLoad(cell, 20);
            else OnSceneAlreadyLoaded?.Invoke(scene);
            EnqueueNeighbours(cell);
        }

        private void EnqueueLoad(Vector2Int cell, int priority = 10)
        {
            _actions.RemoveAll(action => action.Cell == cell);
            _actions.Add(new BlockySceneAction(cell, LoadSceneCell, priority));
        }

        private (HashSet<Vector2Int> toLoad, HashSet<Vector2Int> toUnload)
            GetNeighbourLoadsAndUnloads(Vector2Int cell)
        {
            var toLoad = BlockyWorldUtilities.GetNeighbouringCells(cell).ToHashSet();
            var toUnload = _loadedScenes.Keys.ToHashSet();
            toUnload.ExceptWith(toLoad);
            if (toUnload.Contains(_currentCell)) toUnload.Remove(_currentCell);
            return (toLoad, toUnload);
        }

        private void EnqueueNeighbours(Vector2Int cell)
        {
            var (toLoad, toUnload) = GetNeighbourLoadsAndUnloads(cell);
            foreach (var load in toLoad) EnqueueLoad(load, 5);
            foreach (var unload in toUnload) EnqueueUnload(unload);
        }

        private void EnqueueUnload(Vector2Int cell)
        {
            _actions.RemoveAll(action => action.Cell == cell);
            _actions.Add(new BlockySceneAction(cell, UnloadSceneCell, 0));
        }

        private IEnumerator UnloadSceneCell(Vector2Int cell)
        {
            _isLoading = true;
            try
            {
                if (_loadedScenes.TryGetValue(cell, out var instance))
                {
                    yield return Addressables.UnloadSceneAsync(instance);
                    _loadedScenes.Remove(cell);
                }
            }
            finally
            {
                _isLoading = false;
            }
        }

        private IEnumerator LoadSceneCell(Vector2Int cell)
        {
            if (_worldKey?.Scenes == null) yield break;
            if (!_worldKey.Scenes.TryGetValue(cell, out var scene)) yield break;
            var sceneName = scene.SceneName;
            if (SceneManager.GetSceneByName(sceneName).isLoaded ||
                _loadedScenes.ContainsKey(cell)) yield break;

            var loadAo = Addressables.LoadSceneAsync(sceneName, LoadSceneMode.Additive, false);
            if (!loadAo.IsValid()) yield break;
            _isLoading = true;
            try
            {
                yield return loadAo;
                if (!_loadedScenes.TryAdd(cell, loadAo.Result))
                    Debug.LogError($"Could not add {cell} to _loadedScenes");
                yield return loadAo.Result.ActivateAsync();
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (gizmoRadius <= 0) return;
            var size = new Vector3(BlockyWorldUtilities.SceneGridSize, 0,
                BlockyWorldUtilities.SceneGridSize);
            Gizmos.color = Color.cyan;
            var neighbours = BlockyWorldUtilities.GetNeighbouringCells(_currentCell);
            for (var x = -gizmoRadius; x <= gizmoRadius; x++)
            {
                for (var y = -gizmoRadius; y <= gizmoRadius; y++)
                {
                    var cell = new Vector2Int(_currentCell.x + x, _currentCell.y + y);
                    var pos = new Vector3(cell.x * BlockyWorldUtilities.SceneGridSize, 0,
                        cell.y * BlockyWorldUtilities.SceneGridSize);
                    Gizmos.DrawWireCube(pos, size);
                    if (_currentCell == cell)
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

            if (Target == null) return;
            Gizmos.color = new Color(0.4f, 1, 0.4f, 1);
            Gizmos.DrawSphere(Target.position, 3);
        }
    }
}
