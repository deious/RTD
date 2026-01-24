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

    [Header("MiniMap")]
    [SerializeField] private MiniMapMonsterUIRenderer miniMapMonsterUI;

    public event System.Action<int, int> OnWaveMonsterCountChanged;
    public event System.Action OnWaveCleared;

    public int ActiveCount => activeCount;
    public int TotalThisWave => totalThisWave;
    public bool IsSpawning => isSpawning;

    private int totalThisWave;
    private int spawnedCount;
    private int activeCount;
    private int killedCount;
    private int escapedCount;

    private bool isSpawning;
    private bool spawnFinished;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void StartWaveTracking(int total)
    {
        totalThisWave = Mathf.Max(0, total);

        spawnedCount = 0;
        activeCount = 0;
        killedCount = 0;
        escapedCount = 0;

        spawnFinished = false;
        isSpawning = true;

        RaiseMonsterCountChanged();
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

        if (!isSpawning) 
            return;
        
        GameObject prefabToUse = (prefabOverride != null) ? prefabOverride : monsterPrefab;
        if (prefabToUse == null)
        {
            Debug.LogError("MonsterSpawner: prefabToUse is null");
            return;
        }

        GameObject go = Instantiate(prefabToUse);
        RegisterSpawnedMonster(go, isBoss);

        MonsterAI ai = go.GetComponent<MonsterAI>();
        if (ai != null)
        {
            ai.SetIsBoss(isBoss);
            ai.AddBaseHp(hpAddPerWave * (waveIndex - 1));
            ai.ApplyWaveModifiers(mods);
        }
    }

    private async UniTaskVoid SpawnBossAfterAsync(
        float delay,
        int waveIndex,
        WaveModifiers mods,
        BossMonsterDataSO bossData)
    {
        if (delay > 0f)
            await UniTask.Delay(
                System.TimeSpan.FromSeconds(delay),
                DelayType.DeltaTime,
                PlayerLoopTiming.Update,
                this.GetCancellationTokenOnDestroy());

        if (!isSpawning) 
            return;
        
        GameObject go = Instantiate(bossData.prefab);
        RegisterSpawnedMonster(go, isBoss: true);

        go.transform.localScale = Vector3.one * bossData.scale;

        if (CameraShaker.Instance != null)
            CameraShaker.Instance.Shake(bossData.shakeDuration, bossData.shakeStrength);

        MonsterAI ai = go.GetComponent<MonsterAI>();
        if (ai != null)
        {
            ai.ApplyBaseStats(bossData.maxHp, bossData.moveSpeed, bossData.shieldHp, true);
            ai.ApplyWaveModifiers(mods);
        }
    }

    private async UniTaskVoid FinishSpawnAfterAsync(float delay)
    {
        if (delay > 0f)
            await UniTask.Delay(
                System.TimeSpan.FromSeconds(delay),
                DelayType.DeltaTime,
                PlayerLoopTiming.Update,
                this.GetCancellationTokenOnDestroy());
        
        if (!isSpawning) 
            return;
        
        spawnFinished = true;
        isSpawning = false;

        TryNotifyWaveCleared();
    }

    private void RegisterSpawnedMonster(GameObject go, bool isBoss)
    {
        if (go == null) return;

        spawnedCount++;
        activeCount++;
        RaiseMonsterCountChanged();
        
        if (miniMapMonsterUI != null)
        {
            var mm = go.AddComponent<MiniMapMonsterReporter>();
            mm.Init(miniMapMonsterUI, go.transform);
        }

        var reporter = go.AddComponent<MonsterLifeReporter>();
        reporter.Init(this);
        
        var ai = go.GetComponent<MonsterAI>();
        if (ai != null)
            ai.SetSpawner(this);
    }

    // ====== 외부(몬스터)에서 호출 ======

    internal void NotifyMonsterKilled()
    {
        activeCount = Mathf.Max(0, activeCount - 1);
        killedCount++;
        RaiseMonsterCountChanged();
        TryNotifyWaveCleared();
    }

    internal void NotifyMonsterEscaped()
    {
        activeCount = Mathf.Max(0, activeCount - 1);
        escapedCount++;
        RaiseMonsterCountChanged();
        TryNotifyWaveCleared();
    }

    private void RaiseMonsterCountChanged()
    {
        OnWaveMonsterCountChanged?.Invoke(killedCount, totalThisWave);
    }

    private void TryNotifyWaveCleared()
    {
        if (spawnFinished && (killedCount + escapedCount) >= totalThisWave)
            OnWaveCleared?.Invoke();
    }

    private class MonsterLifeReporter : MonoBehaviour
    {
        private MonsterSpawner _spawner;
        private bool _done;
        private bool _wasKilled;

        public void Init(MonsterSpawner spawner)
        {
            _spawner = spawner;
        }

        public void MarkKilled()
        {
            _wasKilled = true;
        }

        private void OnDestroy()
        {
            if (_done) return;
            _done = true;
        }
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

        StartWaveTracking(count);

        for (int i = 0; i < count; i++)
        {
            float delay = i * spawnInterval;
            SpawnOneAfterAsync(delay, waveIndex, mods, null).Forget();
        }

        float lastDelay = (count > 0) ? ((count - 1) * spawnInterval) : 0f;
        FinishSpawnAfterAsync(lastDelay + 0.01f).Forget();
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
        int total = 0;

        if (pattern.spawns != null)
        {
            for (int e = 0; e < pattern.spawns.Length; e++)
            {
                var entry = pattern.spawns[e];
                if (entry.monsterPrefab == null || entry.count <= 0)
                    continue;

                int c = Mathf.Min(entry.count, maxSpawnCount);
                total += c;
            }
        }

        bool hasBoss = pattern.isBossWave && pattern.bossData != null && pattern.bossData.prefab != null;
        if (hasBoss) total += 1;

        StartWaveTracking(total);

        if (pattern.spawns != null)
        {
            for (int e = 0; e < pattern.spawns.Length; e++)
            {
                var entry = pattern.spawns[e];
                if (entry.monsterPrefab == null || entry.count <= 0)
                    continue;

                int c = Mathf.Min(entry.count, maxSpawnCount);

                for (int i = 0; i < c; i++)
                {
                    float delay = seq * interval;
                    seq++;
                    SpawnOneAfterAsync(delay, waveIndex, mods, entry.monsterPrefab).Forget();
                }
            }
        }

        float lastDelay;

        if (hasBoss)
        {
            float delay = seq * interval;
            lastDelay = delay;
            SpawnBossAfterAsync(delay, waveIndex, mods, pattern.bossData).Forget();
        }
        else
        {
            lastDelay = (seq > 0) ? ((seq - 1) * interval) : 0f;
        }

        FinishSpawnAfterAsync(lastDelay + 0.01f).Forget();
    }
    
    public void StopAllSpawning(bool destroyAlive = false)
    {
        isSpawning = false;
        spawnFinished = true;

        if (destroyAlive)
        {
            var monsters = FindObjectsByType<MonsterAI>(FindObjectsSortMode.None);
            foreach (var m in monsters)
            {
                if (m != null) Destroy(m.gameObject);
            }
        }

        RaiseMonsterCountChanged();
    }
}
