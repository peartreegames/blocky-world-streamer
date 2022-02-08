using System;
using System.Collections;
using UnityEngine;

namespace PeartreeGames.BlockyWorldStreamer
{
    public readonly struct BlockySceneAction : IComparable<BlockySceneAction>
    {
        public BlockySceneAction(Vector2Int cell, Func<Vector2Int, IEnumerator> action, int priority)
        {
            Cell = cell;
            Action = action;
            _priority = priority;
        }

        public readonly Vector2Int Cell;
        public readonly Func<Vector2Int, IEnumerator> Action;
        private readonly int _priority;

        public int CompareTo(BlockySceneAction other) => _priority.CompareTo(other._priority);
    }
}