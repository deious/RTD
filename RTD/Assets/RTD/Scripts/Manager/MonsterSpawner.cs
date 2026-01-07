using UnityEngine;
using Cysharp.Threading.Tasks;
using RTD.Scripts.GamePlay.Wave;

public class MonsterSpawner : MonoBehaviour
{
    public static MonsterSpawner Instance { get; private set; }

    [SerializeField] private GameObject monsterPrefab;
    [SerializeField] private int spawnCountPerWave = 5;
    [SerializeField] private float spawnInterval = 0.6f;
    
    [Header("Wave Scaling")]
    [SerializeField] private int spawnAddPerWave = 1;
    [SerializeField] private int maxSpawnCount = 30;
    [SerializeField] private int hpAddPerWave = 2;  

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void SpawnWave(int waveIndex, WaveModifiers mods)
    {
        if (monsterPrefab == null)
        {
            Debug.LogError("MonsterSpawner: monsterPrefab is null");
            return;
        }
        
        int count = spawnCountPerWave + (waveIndex - 1) * spawnAddPerWave;
        if (count < 1) count = 1;
        if (count > maxSpawnCount) count = maxSpawnCount;

        for (int i = 0; i < count; i++)
        {
            float delay = i * spawnInterval;
            SpawnOneAfterAsync(delay, waveIndex, mods, null).Forget();
        }
    }
    
    public void SpawnPattern(WavePatternSO pattern, WaveModifiers mods)
    {
        if (pattern == null)
        {
            Debug.LogError("MonsterSpawner: pattern is null");
            return;
        }
    
        float interval = (pattern.spawnInterval > 0f) ? pattern.spawnInterval : spawnInterval;
    
        int seq = 0;
        int waveIndex = pattern.waveIndex;
    
        // Normal spawns
        if (pattern.spawns != null)
        {
            for (int e = 0; e < pattern.spawns.Length; e++)
            {
                var entry = pattern.spawns[e];
                if (entry.monsterPrefab == null || entry.count <= 0)
                    continue;
    
                int count = entry.count;
                if (count > maxSpawnCount) count = maxSpawnCount;
    
                for (int i = 0; i < count; i++)
                {
                    float delay = seq * interval;
                    seq++;
    
                    SpawnOneAfterAsync(delay, waveIndex, mods, entry.monsterPrefab).Forget();
                }
            }
        }
    
        // Boss spawn
        if (pattern.isBossWave && pattern.bossPrefab != null)
        {
            float delay = seq * interval;
            SpawnOneAfterAsync(delay, waveIndex, mods, pattern.bossPrefab, true).Forget();
        }
    }

    private async UniTaskVoid SpawnOneAfterAsync(
        float delay,
        int waveIndex,
        WaveModifiers mods,
        GameObject prefabOverride = null,
        bool isBoss = false)
    {
        if (delay > 0f)
            await UniTask.Delay(
                System.TimeSpan.FromSeconds(delay),
                DelayType.DeltaTime,
                PlayerLoopTiming.Update,
                this.GetCancellationTokenOnDestroy());

        GameObject prefabToUse = (prefabOverride != null) ? prefabOverride : monsterPrefab;
        if (prefabToUse == null)
        {
            Debug.LogError("MonsterSpawner: prefabToUse is null");
            return;
        }

        GameObject go = Instantiate(prefabToUse);

        MonsterAI ai = go.GetComponent<MonsterAI>();
        
        if (ai != null)
        {
            ai.SetIsBoss(isBoss);

            ai.AddBaseHp(hpAddPerWave * (waveIndex - 1));
            ai.ApplyWaveModifiers(mods);
        }
    }

}