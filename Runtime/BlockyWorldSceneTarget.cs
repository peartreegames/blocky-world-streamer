using System;
using PeartreeGames.Evt.Variables;
using UnityEngine;

namespace PeartreeGames.BlockyWorldStreamer
{
    public class BlockyWorldSceneTarget : MonoBehaviour
    {
        [SerializeField] private EvtTransform target;

        private void Awake()
        {
            target.Value = transform;
        }
    }
}