using System.Collections;
using PeartreeGames.Evt.Variables;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace PeartreeGames.Blocky.WorldStreamer
{
    public class BlockyWorldSceneTarget : MonoBehaviour
    {
        [SerializeField] private AssetReferenceT<EvtTransform> targetRef;
        
        private IEnumerator Start()
        {
            var ao = targetRef.LoadAssetAsync();
            yield return ao;
            ao.Result.Value = transform;
        }

        private void OnDestroy()
        {
            if (targetRef.IsValid() && targetRef.OperationHandle.IsValid()) targetRef.ReleaseAsset();
        }
    }
}