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
    
    public event System.Action<int, int> OnWaveMonsterCountChanged;
    public event System.Action OnWaveCleared;

    public int AliveCount => _aliveCount;
    public int TotalThisWave => _totalThisWave;
    public bool IsSpawning => _isSpawning;

    private int _aliveCount;
    private int _totalThisWave;
    private bool _isSpawning;
    private bool _spawnFinished;

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
        _totalThisWave = Mathf.Max(0, total);
        _aliveCount = 0;
        _spawnFinished = false;
        _isSpawning = true;

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

        GameObject prefabToUse = (prefabOverride != null) ? prefabOverride : monsterPrefab;
        if (prefabToUse == null)
        {
            Debug.LogError("MonsterSpawner: prefabToUse is null");
            return;
        }

        GameObject go = Instantiate(prefabToUse);
        RegisterSpawnedMonster(go);
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

        GameObject go = Instantiate(bossData.prefab);
        RegisterSpawnedMonster(go);
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

        _spawnFinished = true;
        _isSpawning = false;

        TryNotifyWaveCleared();
    }
    
    private void RegisterSpawnedMonster(GameObject go)
    {
        if (go == null) return;

        _aliveCount++;
        RaiseMonsterCountChanged();
        
        var reporter = go.AddComponent<MonsterLifeReporter>();
        reporter.Init(this);
    }

    internal void NotifyMonsterRemoved()
    {
        _aliveCount = Mathf.Max(0, _aliveCount - 1);
        RaiseMonsterCountChanged();
        TryNotifyWaveCleared();
    }

    private void RaiseMonsterCountChanged()
    {
        int killed = _totalThisWave - _aliveCount;
        if (killed < 0) killed = 0;

        OnWaveMonsterCountChanged?.Invoke(killed, _totalThisWave);
    }

    private void TryNotifyWaveCleared()
    {
        if (_spawnFinished && _aliveCount <= 0)
        {
            OnWaveCleared?.Invoke();
        }
    }
    
    private class MonsterLifeReporter : MonoBehaviour
    {
        private MonsterSpawner _spawner;
        private bool _done;

        public void Init(MonsterSpawner spawner) => _spawner = spawner;

        private void OnDestroy()
        {
            if (_done) return;
            _done = true;
            _spawner?.NotifyMonsterRemoved();
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

                int count = Mathf.Min(entry.count, maxSpawnCount);
                total += count;
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

                int count = Mathf.Min(entry.count, maxSpawnCount);

                for (int i = 0; i < count; i++)
                {
                    float delay = seq * interval;
                    seq++;
                    SpawnOneAfterAsync(delay, waveIndex, mods, entry.monsterPrefab).Forget();
                }
            }
        }

        float lastDelay = 0f;

        if (hasBoss)
        {
            float delay = seq * interval;
            lastDelay = delay;
            SpawnBossAfterAsync(delay, waveIndex, mods, pattern.bossData).Forget();
        }
        else
        {
            lastDelay = (seq > 0) ? ( (seq - 1) * interval ) : 0f;
        }

        FinishSpawnAfterAsync(lastDelay + 0.01f).Forget();
    }

}