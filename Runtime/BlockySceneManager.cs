using System.Collections;
using System.Collections.Generic;
using System.Linq;
using PeartreeGames.EvtVariables;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace PeartreeGames.BlockyWorldStreamer
{
    public class BlockySceneManager : MonoBehaviour
    {
        [SerializeField] private EvtTransformObject target;
        [SerializeField] private float loadDelay = 2f;
        [SerializeField] private bool debug;
        public List<EvtBoolObject> readyObjects;
        

        public static readonly EvtEvent OnWorldSceneReady = new();
        private Vector2Int CurrentCell { get; set; }
        private Vector2Int NextCell { get; set; }
        private float _loadDelay;

        private Dictionary<Vector2Int, SceneInstance> _loadedScenes;
        private (Vector2Int cell, IEnumerator co) _loadingCoroutine;
        private bool _isLoading;
        private List<BlockySceneAction> _actions;


        private void Awake()
        {
            _actions = new List<BlockySceneAction>();
            _loadedScenes = new Dictionary<Vector2Int, SceneInstance>();
        }

        private void Update()
        {
            var targetCell = BlockyWorldUtilities.GetCellFromWorldPosition(target.Value.position);
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

        private IEnumerator Start()
        {
#if UNITY_EDITOR
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.name.StartsWith(BlockyWorldUtilities.ScenePrefix))
                    yield return SceneManager.UnloadSceneAsync(scene);
            }
#endif
            enabled = false;
            while (target.Value == null) yield return null;
            SceneManager.SetActiveScene(gameObject.scene);
            CurrentCell = BlockyWorldUtilities.GetCellFromWorldPosition(target.Value.position);
            yield return LoadSceneCell(CurrentCell);
            foreach (var load in GetNeighbourLoadsAndUnloads(CurrentCell).toLoad) yield return LoadSceneCell(load);
            while (!readyObjects.TrueForAll(obj => obj.Value)) yield return null;
            enabled = true;
            OnWorldSceneReady?.Invoke();
        }

        public void RequestSceneLoad(Vector2Int cell)
        {
            var sceneName = BlockyWorldUtilities.GetScenePathFromCell(cell);
            if (!SceneManager.GetSceneByName(sceneName).isLoaded) EnqueueLoad(cell, 20);
            EnqueueNeighbours(cell);
        }

        private void EnqueueLoad(Vector2Int cell, int priority = 10)
        {
            if (_loadingCoroutine.cell == cell && _loadingCoroutine.co != null) StopCoroutine(_loadingCoroutine.co);
            _actions.RemoveAll(action => action.Cell == cell);
            var action = new BlockySceneAction(cell, LoadSceneCell, priority);
            _actions.Add(action);
        }

        private (HashSet<Vector2Int> toLoad, HashSet<Vector2Int> toUnload) GetNeighbourLoadsAndUnloads(Vector2Int cell)
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
            if (_loadingCoroutine.cell == cell && _loadingCoroutine.co != null) StopCoroutine(_loadingCoroutine.co);
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
            var sceneName = BlockyWorldUtilities.GetScenePathFromCell(cell);
            if (SceneManager.GetSceneByName(sceneName).isLoaded) goto complete;

            var key = Addressables.LoadResourceLocationsAsync(sceneName);
            while (!key.IsDone) yield return null;
            if (key.Result.Count == 0) goto complete;

            var loadAo = Addressables.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (!loadAo.IsValid())
            {
                Debug.LogError($"{sceneName} is not a valid address!");
                goto complete;
            }

            while (!loadAo.IsDone) yield return null;

            _loadedScenes.Add(cell, loadAo.Result);
            yield return new WaitForSeconds(0.2f);

            complete:
            _isLoading = false;
        }

        private void OnDrawGizmos()
        {
            if (!debug) return;
            var size = new Vector3(BlockyWorldUtilities.SceneGridSize, 0, BlockyWorldUtilities.SceneGridSize);
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
            if (target.Value == null) return;
            Gizmos.color = new Color(0.4f, 1, 0.4f, 1);
            Gizmos.DrawSphere(target.Value.position, 3);
        }
    }
}