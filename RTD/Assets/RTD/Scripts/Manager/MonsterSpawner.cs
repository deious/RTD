using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using RTD.Scripts.GamePlay.Wave;

public class MonsterSpawner : MonoBehaviour
{
    public static MonsterSpawner Instance { get; private set; }

    //[SerializeField] private GameObject monsterPrefab;
    //[SerializeField] private int spawnCountPerWave = 5;
    [SerializeField] private float spawnInterval = 0.6f;

    [Header("Wave Scaling")]
    //[SerializeField] private int spawnAddPerWave = 1;
    //[SerializeField] private int maxSpawnCount = 30;
    [SerializeField] private int hpAddPerWave = 2;

    [Header("MiniMap")]
    [SerializeField] private MiniMapMonsterUIRenderer miniMapMonsterUI;

    private CancellationTokenSource waveSpawnCts;
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
    private int currentWaveId;

    private bool isSpawning;
    private bool spawnFinished;
    
    // ===== Debug counters =====
    [SerializeField] private bool debugSpawnLog = true;

    private int scheduledCount;   // "예약"한 스폰 수
    private int canceledCount;    // Delay에서 cancel로 빠진 수
    private int earlyReturnCount; // delay 이후 조건(isSpawning/waveId mismatch 등)으로 return된 수

    private int startWaveCallCount;   // StartWaveTracking 중복 호출 감지
    private int waveClearedCallCount; // OnWaveCleared 중복 호출 감지

    private int currentWaveTokenId;   // CTS 구분용(간단히 증가)

// log helper
    private void DLog(string msg)
    {
        if (!debugSpawnLog) return;
        Debug.Log(msg);
    }

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
        currentWaveId++;
        ///
        startWaveCallCount++;
        int prevWaveId = currentWaveId;
        int prevTokenId = currentWaveTokenId;
        bool prevCtsCanceled = waveSpawnCts != null && waveSpawnCts.IsCancellationRequested;
        ///
        waveSpawnCts?.Cancel();
        waveSpawnCts?.Dispose();
        waveSpawnCts = new CancellationTokenSource();

        // currentWaveTokenId++ 추후 삭제
        currentWaveTokenId++;
        
        totalThisWave = Mathf.Max(0, total);

        spawnedCount = 0;
        activeCount = 0;
        killedCount = 0;
        escapedCount = 0;

        // 아래 3개 삭제
        scheduledCount = 0;
        canceledCount = 0;
        earlyReturnCount = 0;
        
        spawnFinished = false;
        isSpawning = true;

        DLog($"[WaveStart] waveIndex? (pattern) | waveId={currentWaveId} (prev={prevWaveId}) " +
             $"StartCalls={startWaveCallCount} prevCTS(tokenId={prevTokenId}) wasCanceled={prevCtsCanceled} " +
             $"newCTS(tokenId={currentWaveTokenId}) totalThisWave={totalThisWave} time={Time.time:F2}");
        
        RaiseMonsterCountChanged();
    }

    private async UniTaskVoid SpawnOneAfterAsync(
        float delay,
        int waveIndex,
        int waveId,
        WaveModifiers mods,
        MonsterArchetypeSO archetype,
        MonsterColorSO color)
    {
        try
        {
            if (delay > 0f)
                await UniTask.Delay(
                    System.TimeSpan.FromSeconds(delay),
                    DelayType.DeltaTime,
                    PlayerLoopTiming.Update,
                    waveSpawnCts.Token);
        }
        catch (System.OperationCanceledException)
        {
            return;
        }

        if (!isSpawning) return;
        if (waveId != currentWaveId) return;

        if (archetype == null || archetype.prefab == null)
        {
            Debug.LogError("SpawnOneAfterAsync: archetype or archetype.prefab is null");
            return;
        }

        Debug.Log($"[SpawnFire] spawnWave={waveIndex} | currentWave={GameRuntime.Instance?.CurrentWave} | time={Time.time:F2}");

        GameObject go = (SimplePool.Instance != null)
            ? SimplePool.Instance.Get(archetype.prefab)
            : Instantiate(archetype.prefab);
        
        RegisterSpawnedMonster(go);

        MonsterAI ai = go.GetComponent<MonsterAI>();
        if (ai != null)
        {
            ai.ApplyArchetype(archetype, isBoss: false);
            ai.ApplyColor(color);
            ai.AddBaseHp(hpAddPerWave * (waveIndex - 1));
            ai.ApplyWaveModifiers(mods);
            ai.BeginRun();
        }
    }

    private async UniTaskVoid SpawnBossAfterAsync(
        float delay,
        int waveIndex,
        int waveId,
        WaveModifiers mods,
        BossMonsterDataSO bossData)
    {
        try
        {
            if (delay > 0f)
                await UniTask.Delay(
                    System.TimeSpan.FromSeconds(delay),
                    DelayType.DeltaTime,
                    PlayerLoopTiming.Update,
                    waveSpawnCts.Token);
        }
        catch (System.OperationCanceledException)
        {
            return;
        }

        if (!isSpawning)
            return;

        if (waveId != currentWaveId)
            return;

        Debug.Log($"[SpawnFire] spawnWave={waveIndex} | currentWave={GameRuntime.Instance?.CurrentWave} | time={Time.time:F2}");

        GameObject go = (SimplePool.Instance != null)
            ? SimplePool.Instance.Get(bossData.prefab)
            : Instantiate(bossData.prefab);
        
        RegisterSpawnedMonster(go);

        go.transform.localScale = Vector3.one * bossData.scale;

        if (CameraShaker.Instance != null)
            CameraShaker.Instance.Shake(bossData.shakeDuration, bossData.shakeStrength);

        MonsterAI ai = go.GetComponent<MonsterAI>();
        if (ai != null)
        {
            ai.ApplyBaseStats(bossData.maxHp, bossData.moveSpeed, bossData.shieldHp, true);
            ai.ApplyWaveModifiers(mods);
            ai.BeginRun();
        }
    }

    private async UniTaskVoid FinishSpawnAfterAsync(float delay, int waveId)
    {
        try
        {
            if (delay > 0f)
                await UniTask.Delay(
                    System.TimeSpan.FromSeconds(delay),
                    DelayType.DeltaTime,
                    PlayerLoopTiming.Update,
                    waveSpawnCts.Token);
        }
        catch (System.OperationCanceledException)
        {
            return;
        }

        if (waveId != currentWaveId) 
            return;
        
        spawnFinished = true;
        
        try
        {
            await UniTask.WhenAny(
                UniTask.WaitUntil(() => spawnedCount >= totalThisWave, PlayerLoopTiming.Update, waveSpawnCts.Token),
                UniTask.Delay(System.TimeSpan.FromSeconds(2.0f), DelayType.DeltaTime, PlayerLoopTiming.Update, waveSpawnCts.Token)
            );
        }
        catch (System.OperationCanceledException)
        {
            return;
        }
        
        isSpawning = false;
        
        if (spawnedCount < totalThisWave)
        {
            DLog($"[FinishTimeout] waveId={waveId} spawned={spawnedCount}/{totalThisWave} " +
                 $"active={activeCount} killed={killedCount} escaped={escapedCount} time={Time.time:F2}");
        }

        TryNotifyWaveCleared();
    }

    private void RegisterSpawnedMonster(GameObject go)
    {
        if (go == null) 
            return;

        spawnedCount++;
        activeCount++;
        
        DLog($"[Spawned] waveId={currentWaveId} tokenId={currentWaveTokenId} " +
             $"spawned={spawnedCount}/{totalThisWave} scheduled={scheduledCount} " +
             $"active={activeCount} killed={killedCount} escaped={escapedCount} time={Time.time:F2}");

        
        RaiseMonsterCountChanged();
        
        if (miniMapMonsterUI != null)
        {
            var mm = go.GetComponent<MiniMapMonsterReporter>();
            if (mm == null)
                mm = go.AddComponent<MiniMapMonsterReporter>();

            mm.Init(miniMapMonsterUI, go.transform);
        }

        var ai = go.GetComponent<MonsterAI>();
        if (ai != null)
            ai.SetSpawner(this);
    }

    // ====== 외부(몬스터)에서 호출 ======

    internal void NotifyMonsterKilled(MonsterAI ai)
    {
        if (ai == null) return;

        activeCount = Mathf.Max(0, activeCount - 1);
        killedCount++;
        RaiseMonsterCountChanged();
        TryNotifyWaveCleared();
    }

    internal void NotifyMonsterEscaped(MonsterAI ai)
    {
        if (ai == null) return;

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

    /*private class MonsterLifeReporter : MonoBehaviour
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
    }*/

    /*public void SpawnWave(int waveIndex, WaveModifiers mods)
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
        int waveId = currentWaveId;

        for (int i = 0; i < count; i++)
        {
            float delay = i * spawnInterval;
            SpawnOneAfterAsync(delay, waveIndex, waveId, mods, null).Forget();
        }

        float lastDelay = (count > 0) ? ((count - 1) * spawnInterval) : 0f;
        FinishSpawnAfterAsync(lastDelay + 0.01f, waveId).Forget();
    }*/

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
            for (int i = 0; i < pattern.spawns.Length; i++)
            {
                var entry = pattern.spawns[i];
                if (entry.archetype == null || entry.archetype.prefab == null) continue;
                if (entry.count <= 0) continue;
    
                int c = entry.count;
                total += c;
            }
        }
    
        bool hasBoss = pattern.isBossWave && pattern.bossData != null && pattern.bossData.prefab != null;
        if (hasBoss) total += 1;
    
        StartWaveTracking(total);
        DLog($"[SpawnPattern] waveIndex={waveIndex} waveId={currentWaveId} tokenId={currentWaveTokenId} " +
             $"interval={interval:F2} totalComputed={total} hasBoss={hasBoss} time={Time.time:F2}");
        int waveId = currentWaveId;
        
        if (pattern.spawns != null)
        {
            for (int i = 0; i < pattern.spawns.Length; i++)
            {
                var entry = pattern.spawns[i];
                if (entry.archetype == null || entry.archetype.prefab == null) continue;
                if (entry.count <= 0) continue;
    
                int c = entry.count;
    
                for (int k = 0; k < c; k++)
                {
                    float delay = seq * interval;
                    seq++;
                    
                    // 아래 두개 삭제
                    scheduledCount++;
                    DLog($"[Schedule] waveIndex={waveIndex} waveId={waveId} tokenId={currentWaveTokenId} " +
                         $"seq={seq-1} delay={delay:F2} entryIndex={i} archetype={entry.archetype.id /*없으면 다른 id*/} " +
                         $"color={(entry.color != null ? entry.color.name : "null")} scheduled={scheduledCount}/{totalThisWave}");
    
                    SpawnOneAfterAsync(
                        delay,
                        waveIndex,
                        waveId,
                        mods,
                        entry.archetype,
                        entry.color
                    ).Forget();
                }
            }
        }
    
        float lastDelay;
    
        if (hasBoss)
        {
            float delay = seq * interval;
            lastDelay = delay;
    
            // 보스는 기존 BossDataSO대로 프리팹 별도
            SpawnBossAfterAsync(delay, waveIndex, waveId, mods, pattern.bossData).Forget();
        }
        else
        {
            lastDelay = (seq > 0) ? ((seq - 1) * interval) : 0f;
        }
    
        FinishSpawnAfterAsync(lastDelay + 0.01f, waveId).Forget();
    }
    
    public void StopAllSpawning(bool destroyAlive = false)
    {
        DLog($"[StopAllSpawning] waveId={currentWaveId} tokenId={currentWaveTokenId} " +
             $"destroyAlive={destroyAlive} BEFORE total={totalThisWave} scheduled={scheduledCount} spawned={spawnedCount} " +
             $"active={activeCount} killed={killedCount} escaped={escapedCount} time={Time.time:F2}");
        isSpawning = false;
        spawnFinished = true;

        waveSpawnCts?.Cancel();

        if (destroyAlive)
        {
            var monsters = FindObjectsByType<MonsterAI>(FindObjectsSortMode.None);
            foreach (var m in monsters)
            {
                if (m == null) continue;
                if (SimplePool.Instance != null) SimplePool.Instance.Release(m.gameObject);
                else Destroy(m.gameObject);
            }
        }

        RaiseMonsterCountChanged();
    }
    
    private void OnDestroy()
    {
        waveSpawnCts?.Cancel();
        waveSpawnCts?.Dispose();
        waveSpawnCts = null;
    }
}
