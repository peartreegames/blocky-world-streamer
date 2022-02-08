using System;
using PeartreeGames.EvtVariables;
using UnityEngine;

namespace PeartreeGames.BlockyWorldStreamer
{
    public class BlockyWorldSceneTarget : MonoBehaviour
    {
        [SerializeField] private EvtTransformObject target;

        private void Awake()
        {
            target.Value = transform;
        }
    }
}