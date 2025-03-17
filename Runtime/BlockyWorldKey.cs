using UnityEngine;

namespace PeartreeGames.Blocky.Streamer
{
    [CreateAssetMenu(fileName = "bWorld_", menuName = "Blocky/World Key", order = 0)]
    public class BlockyWorldKey : ScriptableObject
    {
       [field:SerializeField] public string Key { get; private set; } 
       [field:SerializeField] public string Name { get; private set; }
    }
}