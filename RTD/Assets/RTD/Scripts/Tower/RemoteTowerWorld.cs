using System.Collections.Generic;
using UnityEngine;

public class RemoteTowerWorld : MonoBehaviour
{
    [Header("Prefab Source")]
    [SerializeField] private TowerData[] towerPoolForView;

    [Header("Root (optional)")]
    [Tooltip("laneRoots가 비어있을 때 fallback parent")]
    [SerializeField] private Transform towerRoot;

    [Header("Lane Roots (P1~P4)")]
    [Tooltip("각 플레이어 맵의 원점(Transform). worldSlotId(0~3) 인덱스와 일치해야 함")]
    [SerializeField] private Transform[] laneRoots = new Transform[4];

    [Header("Auto Bind (optional)")]
    [SerializeField] private bool autoBindByName = true;
    [SerializeField] private string laneRootNameFormat = "LaneRoot_P{0}"; // P1~P4
    
    [SerializeField] private float tileSize = 10f;

    private readonly Dictionary<long, ProxyTower> _towers = new();
    private readonly Dictionary<string, GameObject> _prefabMap = new();

    private int MySlot => MultiplayerContext.MyLaneId;

    private static long Key(int worldSlotId, int towerId)
        => ((long)worldSlotId << 32) | (uint)towerId;

    private void Awake()
    {
        if (towerRoot == null) towerRoot = transform;

        if (autoBindByName)
            AutoBindLaneRootsByName();

        BuildPrefabMap();
    }

    private void BuildPrefabMap()
    {
        _prefabMap.Clear();

        if (towerPoolForView == null) return;

        foreach (var d in towerPoolForView)
        {
            if (d == null || d.towerPrefab == null) continue;
            if (string.IsNullOrEmpty(d.towerId)) continue;
            _prefabMap[d.towerId] = d.towerPrefab;
        }
    }

    private void AutoBindLaneRootsByName()
    {
        if (laneRoots == null || laneRoots.Length != 4)
            laneRoots = new Transform[4];

        for (int i = 0; i < laneRoots.Length; i++)
        {
            if (laneRoots[i] != null) continue;

            string n = string.Format(laneRootNameFormat, i + 1);
            var go = GameObject.Find(n);
            if (go != null) laneRoots[i] = go.transform;
        }
    }

    private Transform GetLaneRoot(int worldSlotId)
    {
        if (laneRoots != null && worldSlotId >= 0 && worldSlotId < laneRoots.Length)
        {
            var r = laneRoots[worldSlotId];
            if (r != null) return r;
        }
        return towerRoot != null ? towerRoot : transform;
    }

    private Vector3 GridToWorld(int worldSlotId, int gx, int gy)
    {
        // ✅ 핵심: laneRoot의 position을 “원점(offset)”으로 사용
        Transform root = GetLaneRoot(worldSlotId);
        Vector3 origin = root.position;

        // 네 프로젝트는 타일이 (x,z)라서 gy를 z로 사용
        return origin + new Vector3(gx * tileSize, 0f, gy * tileSize);
    }

    public void OnSpawnOrUpdateTower(TowerCombatBridge.TowerSnapshot snap)
    {
        // 내 lane은 로컬 실체가 있으니 리모트 월드에 만들지 않음
        if (snap.worldSlotId == MySlot) return;

        long key = Key(snap.worldSlotId, snap.towerId);

        string typeId = snap.towerTypeId.ToString();
        Vector2Int gridPos = new Vector2Int(snap.gx, snap.gy);

        // 이미 있으면 업데이트만
        if (_towers.TryGetValue(key, out var pt) && pt != null)
        {
            pt.ApplyLevel(snap.level);
            // 위치도 동기화하고 싶으면 여기서 이동 처리 가능
            // pt.transform.position = GridToWorld(snap.worldSlotId, snap.gx, snap.gy);
            return;
        }

        // 프리팹 매핑 확인
        if (!_prefabMap.TryGetValue(typeId, out var prefab) || prefab == null)
        {
            Debug.LogWarning($"[RemoteTowerWorld] Missing prefab for towerTypeId={typeId}");
            return;
        }

        Vector3 worldPos = GridToWorld(snap.worldSlotId, snap.gx, snap.gy);
        Transform parent = GetLaneRoot(snap.worldSlotId);

        var go = Instantiate(prefab, worldPos, Quaternion.identity, parent);

        // ✅ 관전용: 실제 타워 로직은 꺼야 함
        foreach (var tb in go.GetComponentsInChildren<TowerBase>(true))
            tb.enabled = false;

        pt = go.GetComponent<ProxyTower>();
        if (pt == null) pt = go.AddComponent<ProxyTower>();

        pt.Init(snap.worldSlotId, snap.towerId, typeId, gridPos, snap.level);
        _towers[key] = pt;
    }

    public void OnDespawnTower(int worldSlotId, int towerId)
    {
        if (worldSlotId == MySlot) return;

        long key = Key(worldSlotId, towerId);

        if (_towers.TryGetValue(key, out var pt))
        {
            _towers.Remove(key);
            if (pt != null) Destroy(pt.gameObject);
        }
    }

    // ✅ 권장: laneId를 명시적으로 받는 버전 (유령 타워 정리 정확)
    public void OnSyncTowers(int laneId, TowerCombatBridge.TowerSnapshot[] snaps)
    {
        if (laneId == MySlot) return;

        // 이번 sync에 포함된 타워 key
        HashSet<long> alive = new HashSet<long>();

        if (snaps != null)
        {
            for (int i = 0; i < snaps.Length; i++)
            {
                var s = snaps[i];

                // 안전: 혹시 laneId와 다른 스냅이 섞여오면 무시
                if (s.worldSlotId != laneId) continue;
                if (s.worldSlotId == MySlot) continue;

                long key = Key(s.worldSlotId, s.towerId);
                alive.Add(key);

                OnSpawnOrUpdateTower(s);
            }
        }

        // ✅ 유령 제거: 이번 sync 대상 laneId의 기존 타워 중 alive에 없는 것 제거
        List<long> toRemove = null;

        foreach (var kv in _towers)
        {
            long key = kv.Key;
            int worldSlotId = (int)(key >> 32);

            if (worldSlotId != laneId) continue;

            if (!alive.Contains(key))
            {
                toRemove ??= new List<long>();
                toRemove.Add(key);
            }
        }

        if (toRemove == null) return;

        for (int i = 0; i < toRemove.Count; i++)
        {
            long k = toRemove[i];
            if (_towers.TryGetValue(k, out var pt))
            {
                _towers.Remove(k);
                if (pt != null) Destroy(pt.gameObject);
            }
        }
    }

    // ✅ 기존 네 코드 호환용: snaps[0].worldSlotId로 laneId 추정 (가능하면 위 버전 쓰기)
    public void OnSyncTowers(TowerCombatBridge.TowerSnapshot[] snaps)
    {
        int laneId = (snaps != null && snaps.Length > 0) ? snaps[0].worldSlotId : -1;
        if (laneId < 0) return;
        OnSyncTowers(laneId, snaps);
    }
}
