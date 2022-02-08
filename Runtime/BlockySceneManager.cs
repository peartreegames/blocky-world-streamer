using System;
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
#if !UNITY_EDITOR
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
#endif
        }

        private void Update()
        {
            var targetCell = BlockyWorldUtilities.GetCellFromWorldPosition(target.Value.position);
            if (targetCell != CurrentCell)
            {
                if (targetCell == NextCell) _loadDelay -= Time.deltaTime;
                else
                {
                    NextCell = targetCell;
                    _loadDelay = loadDelay;
                }

                if (_loadDelay < 0)
                {
                    CurrentCell = NextCell;
                    LoadScene(CurrentCell);
                }
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
            yield return new WaitForSeconds(1f);
            CurrentCell = BlockyWorldUtilities.GetCellFromWorldPosition(target.Value.position);
            yield return LoadSubScene(CurrentCell);
            foreach (var load in GetNeighbourLoadsAndUnloads(CurrentCell).toLoad) yield return LoadSubScene(load);
            enabled = true;
            OnWorldSceneReady?.Invoke();
        }

        public void LoadScene(Vector2Int cell)
        {
            var sceneName = BlockyWorldUtilities.GetScenePathFromCell(cell);
            if (!SceneManager.GetSceneByName(sceneName).isLoaded) 
            {
                EnqueueLoad(cell, 20);
            }
            EnqueueNeighbours(cell);
        }

        private void EnqueueLoad(Vector2Int cell, int priority = 10)
        {
            if (_loadingCoroutine.cell == cell && _loadingCoroutine.co != null) StopCoroutine(_loadingCoroutine.co);
            _actions.RemoveAll(action => action.Cell == cell);
            var action = new BlockySceneAction(cell, LoadSubScene, priority);
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
            _actions.Add(new BlockySceneAction(cell, UnloadSubScene, 0));
        }

        private IEnumerator UnloadSubScene(Vector2Int cell)
        {
            _isLoading = true;
            if (_loadedScenes.TryGetValue(cell, out var instance))
            {
                yield return Addressables.UnloadSceneAsync(instance);
                _loadedScenes.Remove(cell);
            }
            _isLoading = false;
        }
        
        private IEnumerator LoadSubScene(Vector2Int cell)
        {
            _isLoading = true;
            var sceneName = BlockyWorldUtilities.GetScenePathFromCell(cell);
            if (SceneManager.GetSceneByName(sceneName).isLoaded)
            {
                _isLoading = false;
                yield break;
            }
            var key = Addressables.LoadResourceLocationsAsync(sceneName);
            while (!key.IsDone) yield return null;
            if (key.Result.Count == 0)
            {
                _isLoading = false;
                yield break;
            }
            var loadAo = Addressables.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (!loadAo.IsValid())
            {
                _isLoading = false;
                Debug.LogError($"{sceneName} is not a valid address!");
                yield break;
            }

            while (!loadAo.IsDone) {yield return null;}
            _loadedScenes.Add(cell, loadAo.Result);
            _isLoading = false;
            // var roots = loadAo.Result.Scene.GetRootGameObjects();
            // foreach (var root in roots)
            // {
            //     if (!root.TryGetComponent<PreAwakeController>(out var preAwake)) continue;
            //     // ConsoleProDebug.LogScene($"{scene.SceneName} Running PreAwakeController");
            //     yield return preAwake.PreAwake();
            //     break;
            // }
        }
    }
}