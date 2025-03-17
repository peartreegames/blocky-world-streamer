using System;
using System.Collections;
using UnityEngine;

namespace PeartreeGames.Blocky.WorldStreamer
{
    public readonly struct BlockySceneAction : IComparable<BlockySceneAction>, IEquatable<BlockySceneAction>
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
        public bool Equals(BlockySceneAction other) => Cell.Equals(other.Cell);
        public override bool Equals(object obj) => obj is BlockySceneAction other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Cell, _priority);
    }
}