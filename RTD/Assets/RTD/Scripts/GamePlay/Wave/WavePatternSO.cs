using UnityEngine;

namespace RTD.Scripts.GamePlay.Wave
{
    [System.Serializable]
    public struct WaveSpawnEntry
    {
        public MonsterArchetypeSO archetype;
        public MonsterColorSO color;
        public int count;
    }

    [CreateAssetMenu(menuName = "RTD/Wave/WavePattern", fileName = "WavePattern_")]
    public class WavePatternSO : ScriptableObject
    {
        [Header("Meta")]
        public int waveIndex;

        [Header("Spawn")]
        public WaveSpawnEntry[] spawns;
        public float spawnInterval = 0.4f;

        [Header("Wave Modifiers")]
        public WaveModifierType[] modifiers;

        [Header("Boss")]
        public bool isBossWave;
        public BossMonsterDataSO bossData;
    }
}