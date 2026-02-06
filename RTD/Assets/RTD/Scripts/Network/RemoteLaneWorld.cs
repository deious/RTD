using System;
using System.Collections.Generic;
using UnityEngine;

public class RemoteLaneWorld : MonoBehaviour
{
    public static RemoteLaneWorld Instance { get; private set; }

    [Header("Proxy Prefabs")]
    [Tooltip("MonsterTypeRegistry에서 프리팹을 못 찾을 때 대신 쓸 최소 프리팹")]
    [SerializeField] private ProxyMonster proxyMonsterPrefab;

    [Header("Options")]
    [Tooltip("Sync에서 누락된 몬스터를 제거(유령 몹 방지)")]
    [SerializeField] private bool cleanupMissingOnSync = true;

    [Tooltip("Sync에 포함된 위치로 이동할 때, 이 거리 이상이면 텔레포트")]
    [SerializeField] private float teleportIfFartherThan = 6.0f;
    
    [Header("Disable On Proxy Spawn")]
    [SerializeField] private bool disableMonsterAIOnProxy = true;
    [SerializeField] private bool disableCollidersOnProxy = true;

    // key: (laneId, netId)
    private readonly Dictionary<long, ProxyMonster> _monsters = new();

    // Sync 동안 seen 체크용(매번 할당 줄이려고 재사용)
    private readonly HashSet<long> _seenInLastSync = new();

    private int MyLaneId => MultiplayerContext.MyLaneId;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        //DontDestroyOnLoad(gameObject);
    }

    // ✅ laneId + netId를 유니크 키로
    private static long Key(int laneId, int netId)
    {
        return ((long)laneId << 32) | (uint)netId;
    }

    // ---------------------------
    // Spawn / Despawn (이벤트 기반)
    // ---------------------------

    public void OnRemoteSpawnMonster(int laneId, int netId, int typeId, Vector3 pos, float hpMax, float hp, float shieldHp)
    {
        // 내 레인은 리모트로 만들지 않음
        if (laneId == MyLaneId) return;

        long k = Key(laneId, netId);

        if (_monsters.TryGetValue(k, out var existing) && existing != null)
        {
            existing.SetVitals(hpMax, hp, shieldHp);
            existing.Teleport(pos);
            return;
        }

        // 프리팹 선택: 가능하면 실제 몬스터 프리팹, 아니면 proxyMonsterPrefab
        GameObject prefab = null;

        // MonsterTypeRegistry가 프로젝트에 있다면 사용(없으면 null 반환 처리)
        try
        {
            prefab = MonsterTypeRegistry.GetPrefab(typeId);
        }
        catch
        {
            // MonsterTypeRegistry가 없거나 예외나도 무시하고 proxy로 대체
            prefab = null;
        }

        GameObject go;
        if (prefab != null)
        {
            go = Instantiate(prefab, pos, Quaternion.identity);
        }
        else
        {
            if (proxyMonsterPrefab == null)
            {
                Debug.LogWarning("[RemoteLaneWorld] proxyMonsterPrefab is null (and MonsterTypeRegistry prefab missing)");
                return;
            }
            go = Instantiate(proxyMonsterPrefab.gameObject, pos, Quaternion.identity);
        }
        
        ApplyProxyDisable(go);
        var pm = go.GetComponent<ProxyMonster>();
        if (pm == null) pm = go.AddComponent<ProxyMonster>();

        pm.Init(laneId, netId, typeId, hpMax, hp, shieldHp);
        pm.Teleport(pos);
        
        AttachMiniMapReporter(go.transform, laneId);

        _monsters[k] = pm;
    }

    public void OnRemoteDespawnMonster(int laneId, int netId)
    {
        if (laneId == MyLaneId) return;

        long k = Key(laneId, netId);

        if (_monsters.TryGetValue(k, out var pm))
        {
            _monsters.Remove(k);
            if (pm != null) 
                Destroy(pm.gameObject);
        }
    }

    // ---------------------------
    // Sync (스냅샷 기반) + 유령 정리
    // ---------------------------

    /// <summary>
    /// packedData 포맷:
    /// [count:int]
    /// 반복 count번:
    ///   netId:int
    ///   x:float y:float z:float
    ///   hp:float
    ///   hpMax:float
    ///   shieldHp:float
    /// </summary>
    public void OnRemoteSyncMonsters(int laneId, byte[] packedData)
    {
        if (laneId == MyLaneId) return;
        if (packedData == null || packedData.Length < 4) return;

        _seenInLastSync.Clear();

        int offset = 0;

        if (!TryReadInt(packedData, ref offset, out int count))
            return;

        if (count < 0 || count > 10000) // 안전장치(이상한 데이터 방어)
        {
            Debug.LogWarning($"[RemoteLaneWorld] Sync count invalid: {count}");
            return;
        }

        for (int i = 0; i < count; i++)
        {
            if (!TryReadInt(packedData, ref offset, out int netId)) break;
            if (!TryReadFloat(packedData, ref offset, out float x)) break;
            if (!TryReadFloat(packedData, ref offset, out float y)) break;
            if (!TryReadFloat(packedData, ref offset, out float z)) break;
            if (!TryReadFloat(packedData, ref offset, out float hp)) break;
            if (!TryReadFloat(packedData, ref offset, out float hpMax)) break;
            if (!TryReadFloat(packedData, ref offset, out float shieldHp)) break;

            long k = Key(laneId, netId);
            _seenInLastSync.Add(k);

            Vector3 pos = new Vector3(x, y, z);

            if (_monsters.TryGetValue(k, out var pm) && pm != null)
            {
                pm.SetVitals(hpMax, hp, shieldHp);

                // 너무 멀면 텔레포트, 아니면 스무스
                float sqr = (pm.transform.position - pos).sqrMagnitude;
                if (sqr >= teleportIfFartherThan * teleportIfFartherThan)
                    pm.Teleport(pos);
                else
                    pm.SmoothTo(pos);
            }
            else
            {
                // Sync에 있는데 로컬에 없으면 "유실된 몬스터" → 최소 프록시로 생성
                // (typeId가 packedData에 없다면 Init typeId는 -1로 둠)
                SpawnProxyFallbackFromSync(laneId, netId, pos, hpMax, hp, shieldHp);
            }
        }

        if (cleanupMissingOnSync)
            CleanupMissingAfterSync(laneId, _seenInLastSync);
    }

    private void SpawnProxyFallbackFromSync(int laneId, int netId, Vector3 pos, float hpMax, float hp, float shieldHp)
    {
        if (proxyMonsterPrefab == null)
        {
            // 대체 프리팹도 없으면 생성 불가
            return;
        }

        long k = Key(laneId, netId);

        var pm = Instantiate(proxyMonsterPrefab, pos, Quaternion.identity);
        pm.Init(laneId, netId, typeId: -1, hpMax, hp, shieldHp);
        pm.Teleport(pos);

        _monsters[k] = pm;
    }
    
    private void AttachMiniMapReporter(Transform monsterRoot, int laneId)
    {
        if (!monsterRoot) return;

        MiniMapMonsterUIRenderer renderer =
            (MiniMapLaneRegistry.Instance != null)
                ? MiniMapLaneRegistry.Instance.GetMonsterRenderer(laneId)
                : null;

        if (renderer == null) return;

        var rep = monsterRoot.GetComponent<MiniMapMonsterReporter>();
        if (!rep) rep = monsterRoot.gameObject.AddComponent<MiniMapMonsterReporter>();

        rep.Init(renderer, monsterRoot);
    }
    
    private void ApplyProxyDisable(GameObject go)
    {
        if (go == null) return;

        if (disableMonsterAIOnProxy)
        {
            var ai = go.GetComponent<MonsterAI>();
            if (ai != null)
            {
                try { ai.SetAsProxyMode(); }
                catch { ai.enabled = false; }
            }
        }

        if (disableCollidersOnProxy)
        {
            var cols = go.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
                cols[i].enabled = false;
        }
    }
    
    private void CleanupMissingAfterSync(int laneId, HashSet<long> seenKeys)
    {
        // Dictionary 순회 중 삭제하면 터지니까 리스트로 수집
        List<long> toRemove = null;

        foreach (var kv in _monsters)
        {
            long key = kv.Key;

            // key에서 laneId 추출
            int keyLane = (int)(key >> 32);
            if (keyLane != laneId) continue;

            if (!seenKeys.Contains(key))
            {
                toRemove ??= new List<long>();
                toRemove.Add(key);
            }
        }

        if (toRemove == null) return;

        for (int i = 0; i < toRemove.Count; i++)
        {
            long k = toRemove[i];

            if (_monsters.TryGetValue(k, out var pm))
            {
                _monsters.Remove(k);
                if (pm != null) Destroy(pm.gameObject);
            }
        }
    }

    // ---------------------------
    // Safe Binary Readers
    // ---------------------------

    private static bool TryReadInt(byte[] data, ref int o, out int v)
    {
        v = 0;
        if (data == null) return false;
        if (o + 4 > data.Length) return false;

        v = BitConverter.ToInt32(data, o);
        o += 4;
        return true;
    }

    private static bool TryReadFloat(byte[] data, ref int o, out float v)
    {
        v = 0;
        if (data == null) return false;
        if (o + 4 > data.Length) return false;

        v = BitConverter.ToSingle(data, o);
        o += 4;
        return true;
    }
    
    private ProxyMonster FindProxyMonster(int laneId, int monsterNetId)
    {
        long key = Key(laneId, monsterNetId);
        if (_monsters.TryGetValue(key, out var pm))
            return pm;
        return null;
    }
    
    private SpectatorProjectileHitListener EnsureSpectateHitListener(GameObject projGo)
    {
        var l = projGo.GetComponent<SpectatorProjectileHitListener>();
        if (l == null) l = projGo.AddComponent<SpectatorProjectileHitListener>();
        l.BindWorld(this);
        return l;
    }
    
    public void OnRemoteTowerFire(
        int laneId,
        int towerNetId,
        int targetMonsterNetId,
        Vector3 firePos,
        string towerTypeId
    )
    {
        OnRemoteTowerFire(
            laneId,
            towerNetId,
            targetMonsterNetId,
            firePos,
            towerTypeId,
            splashRadius: 0f,
            splashRatio: 0f,
            traitType: -1,
            traitValue: 0f,
            traitRange: 0f,
            traitDuration: 0f,
            traitCount: 0
        );
    }
    
    public void OnRemoteTowerFire(
        int laneId,
        int towerNetId,
        int targetMonsterNetId,
        Vector3 firePos,
        string towerTypeId,

        float splashRadius,
        float splashRatio,

        int traitType,
        float traitValue,
        float traitRange,
        float traitDuration,
        int traitCount
    )
    {
        if (laneId == MyLaneId) return;

        if (ProjectilePool.Instance == null) return;
        if (SpectatorProjectileLibrary.Instance == null) return;

        var target = FindProxyMonster(laneId, targetMonsterNetId);
        if (target == null) return;

        if (!SpectatorProjectileLibrary.Instance.TryGet(towerTypeId, out var projPrefab, out var speed, out var lifeTime))
            return;

        var proj = ProjectilePool.Instance.Get(projPrefab, firePos, Quaternion.identity);
        if (proj == null) return;
        
        var listener = EnsureSpectateHitListener(proj.gameObject);
        listener.Configure(
            laneId,
            splashRadius,
            splashRatio,
            traitType,
            traitValue,
            traitRange,
            traitDuration,
            traitCount
        );
        
        proj.InitSpectate(target.transform, speed, lifeTime, listener);
    }
    
    public ProxyMonster FindNearestProxyMonster(int laneId, Vector3 center, float radius, HashSet<ProxyMonster> exclude = null)
    {
        float bestSq = float.PositiveInfinity;
        ProxyMonster best = null;

        float rSq = radius * radius;

        foreach (var kv in _monsters)
        {
            int keyLane = (int)(kv.Key >> 32);
            if (keyLane != laneId) continue;

            var pm = kv.Value;
            if (pm == null) continue;
            if (exclude != null && exclude.Contains(pm)) continue;

            float sq = (pm.transform.position - center).sqrMagnitude;
            if (sq <= rSq && sq < bestSq)
            {
                bestSq = sq;
                best = pm;
            }
        }

        return best;
    }
}
