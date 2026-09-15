using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;
using RTD.Scripts.GamePlay.Wave;
using RTD.Scripts.Network;

public class MonsterSpawner : MonoBehaviour
{
    public static MonsterSpawner Instance { get; private set; }
    
    [SerializeField] private float spawnInterval = 0.6f;

    [Header("Wave Scaling")]
    [SerializeField] private int hpAddPerWave = 2;

    [Header("MiniMap")]
    [SerializeField] private MiniMapMonsterUIRenderer miniMapMonsterUI;

    private CancellationTokenSource _waveSpawnCts;
    public event System.Action<int, int> OnWaveMonsterCountChanged;
    public event System.Action OnWaveCleared;

    public int ActiveCount => _activeCount;
    public int TotalThisWave => _totalThisWave;
    public bool IsSpawning => _isSpawning;

    private int _totalThisWave;
    private int _spawnedCount;
    private int _activeCount;
    private int _killedCount;
    private int _escapedCount;
    private int _currentWaveId;
    private int _nextNetId = 1;

    private bool _isSpawning;
    private bool _spawnFinished;
    
    // ===== Debug counters =====
    [SerializeField] private bool debugSpawnLog = true;

    private int _scheduledCount;   // 예약한 스폰 수
    private int _canceledCount;    // Delay에서 cancel로 빠진 수
    private int _earlyReturnCount; // delay 이후 조건(isSpawning/waveId mismatch 등)으로 return된 수

    private int _startWaveCallCount;   // StartWaveTracking 중복 호출 감지
    private int _waveClearedCallCount; // OnWaveCleared 중복 호출 감지

    private int _currentWaveTokenId;   // CTS 구분용(간단히 증가)
    
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
        _currentWaveId++;
        _startWaveCallCount++;

        _waveSpawnCts?.Cancel();
        _waveSpawnCts?.Dispose();
        _waveSpawnCts = new CancellationTokenSource();
        
        
        _totalThisWave = Mathf.Max(0, total);

        _spawnedCount = 0;
        _activeCount = 0;
        _killedCount = 0;
        _escapedCount = 0;
        
        _spawnFinished = false;
        _isSpawning = true;
        
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
                    _waveSpawnCts.Token);
        }
        catch (System.OperationCanceledException)
        {
            return;
        }
    
        if (!_isSpawning) return;
        if (waveId != _currentWaveId) return;
    
        if (archetype == null || archetype.prefab == null)
        {
            Debug.LogError("SpawnOneAfterAsync: archetype or archetype.prefab is null");
            return;
        }
    
        if (GridManager.Instance == null)
        {
            Debug.LogError("SpawnOneAfterAsync: GridManager.Instance is null");
            return;
        }
    
        Debug.Log($"[SpawnFire] spawnWave={waveIndex} | currentWave={GameRuntime.Instance?.CurrentWave} | time={Time.time:F2}");
    
        GameObject go = (SimplePool.Instance != null)
            ? SimplePool.Instance.Get(archetype.prefab)
            : Instantiate(archetype.prefab);
        
        RegisterSpawnedMonster(go);
    
        // ✅ 멀티 확장 포인트: 이 몬스터가 속한 "플레이어 슬롯(P1~P4)"
        // 지금은 싱글이거나 "내 월드"만 스폰한다고 가정해서 0 고정.
        int worldSlotId = MultiplayerContext.MyLaneId;
    
        // ✅ 세션 내 유니크 몬스터 ID
        int netId = _nextNetId++;
    
        MonsterAI ai = go.GetComponent<MonsterAI>();
        if (ai == null)
        {
            Debug.LogError("SpawnOneAfterAsync: Monster prefab has no MonsterAI component");
            // 풀에서 가져온 거면 반납하는 게 안전
            if (SimplePool.Instance != null) SimplePool.Instance.Release(go);
            else Destroy(go);
            return;
        }
    
        // ✅ 경로 레인(PathLaneIndex)은 MonsterAI가 랜덤으로 뽑지 말고,
        // 스포너가 확정해서 상대에게도 동일값을 보내야 재현이 '완전히 동일'해짐.
        int laneCount = Mathf.Max(1, GridManager.Instance.LaneCount);
        int pathLaneIndex = Random.Range(0, laneCount);
    
        // ✅ Identity + Path 주입
        ai.ConfigureIdentity(worldSlotId, netId);
        ai.ConfigurePathLane(pathLaneIndex, force: true);
    
        // ✅ 기존 스탯/색/스케일링
        ai.ApplyArchetype(archetype, isBoss: false);
        ai.ApplyColor(color);
        ai.ApplyWaveScaling(waveIndex, mods);
    
        // ✅ 출발
        ai.BeginRun();
        
        var bridge = LaneCombatBridge.Instance;
        if (bridge != null)
        {
            if (MonsterTypeRegistry.TryGetTypeId(archetype.prefab, out int typeId))
            {
                bridge.SpawnMonsterServerRpc(
                    worldSlotId,            // laneId
                    netId,
                    typeId,
                    ai.transform.position,
                    ai.MaxHp,
                    ai.CurrentHp,
                    ai.ShieldHp
                );
            }
            else
            {
                Debug.LogError($"[MonsterSpawner] typeId not found for prefab={archetype.prefab.name}. MonsterTypeRegistry prefabs 배열 매칭 필요");
            }
        }
        else
        {
            Debug.LogWarning("[MonsterSpawner] LaneCombatBridge.Instance is null. (Bridge가 씬에 존재 + NetworkObject로 스폰되어야 RPC 가능)");
        }
    
        // (선택) 여기서 네트워크 이벤트 발행할 거면 이 지점이 "정답 위치"임
        // LaneCombatBridge.Instance?.SpawnMonster(worldSlotId, netId, archetypeId, pathLaneIndex, ai.transform.position, ai.MaxHp, ai.CurrentHp);
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
                    _waveSpawnCts.Token);
        }
        catch (System.OperationCanceledException)
        {
            return;
        }

        if (!_isSpawning)
            return;

        if (waveId != _currentWaveId)
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
            //ai.ApplyWaveModifiers(mods);
            ai.ApplyWaveScaling(waveIndex, mods); 
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
                    _waveSpawnCts.Token);
        }
        catch (System.OperationCanceledException)
        {
            return;
        }

        if (waveId != _currentWaveId) 
            return;
        
        _spawnFinished = true;
        
        try
        {
            await UniTask.WhenAny(
                UniTask.WaitUntil(() => _spawnedCount >= _totalThisWave, PlayerLoopTiming.Update, _waveSpawnCts.Token),
                UniTask.Delay(System.TimeSpan.FromSeconds(2.0f), DelayType.DeltaTime, PlayerLoopTiming.Update, _waveSpawnCts.Token)
            );
        }
        catch (System.OperationCanceledException)
        {
            return;
        }
        
        _isSpawning = false;
        
        if (_spawnedCount < _totalThisWave)
        {
            DLog($"[FinishTimeout] waveId={waveId} spawned={_spawnedCount}/{_totalThisWave} " +
                 $"active={_activeCount} killed={_killedCount} escaped={_escapedCount} time={Time.time:F2}");
        }

        TryNotifyWaveCleared();
    }

    private void RegisterSpawnedMonster(GameObject go)
    {
        if (go == null) 
            return;

        _spawnedCount++;
        _activeCount++;
        
        RaiseMonsterCountChanged();
        
        var laneId = MultiplayerContext.MyLaneId;
        MiniMapMonsterUIRenderer rendererForMyLane =
            (MiniMapLaneRegistry.Instance != null)
                ? MiniMapLaneRegistry.Instance.GetMonsterRenderer(laneId)
                : miniMapMonsterUI;
        
        if (rendererForMyLane == null)
            rendererForMyLane = miniMapMonsterUI;

        if (rendererForMyLane != null)
        {
            var mm = go.GetComponent<MiniMapMonsterReporter>();
            if (mm == null) mm = go.AddComponent<MiniMapMonsterReporter>();

            mm.Init(rendererForMyLane, go.transform);
        }

        var ai = go.GetComponent<MonsterAI>();
        if (ai != null)
            ai.SetSpawner(this);
    }

    // ====== 외부(몬스터)에서 호출 ======

    internal void NotifyMonsterKilled(MonsterAI ai)
    {
        if (ai == null) return;

        _activeCount = Mathf.Max(0, _activeCount - 1);
        _killedCount++;
        RaiseMonsterCountChanged();
        TryNotifyWaveCleared();
        
        var bridge = LaneCombatBridge.Instance;
        if (bridge != null)
            bridge.DespawnMonsterServerRpc(MultiplayerContext.MyLaneId, ai.NetId);
    }

    internal void NotifyMonsterEscaped(MonsterAI ai)
    {
        if (ai == null) return;

        _activeCount = Mathf.Max(0, _activeCount - 1);
        _escapedCount++;
        RaiseMonsterCountChanged();
        TryNotifyWaveCleared();
        
        var bridge = LaneCombatBridge.Instance;
        if (bridge != null)
            bridge.DespawnMonsterServerRpc(MultiplayerContext.MyLaneId, ai.NetId);
    }

    private void RaiseMonsterCountChanged()
    {
        OnWaveMonsterCountChanged?.Invoke(_killedCount, _totalThisWave);
    }

    private void TryNotifyWaveCleared()
    {
        if (_spawnFinished && (_killedCount + _escapedCount) >= _totalThisWave)
            OnWaveCleared?.Invoke();
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
        if (hasBoss) 
            total++;
    
        StartWaveTracking(total);

        int waveId = _currentWaveId;
        
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
        _isSpawning = false;
        _spawnFinished = true;

        _waveSpawnCts?.Cancel();

        if (destroyAlive)
        {
            int myLane = MultiplayerContext.MyLaneId;

            var monsters = FindObjectsByType<MonsterAI>(FindObjectsSortMode.None);
            foreach (var m in monsters)
            {
                if (m == null) continue;
                
                if (m.WorldSlotId != myLane) continue;
                if (m.GetComponent<ProxyMonster>() != null) continue;
                if (SimplePool.Instance != null) 
                    SimplePool.Instance.Release(m.gameObject);
                else 
                    Destroy(m.gameObject);
            }
        }

        RaiseMonsterCountChanged();
    }
    
    private void OnDestroy()
    {
        _waveSpawnCts?.Cancel();
        _waveSpawnCts?.Dispose();
        _waveSpawnCts = null;
    }
}
