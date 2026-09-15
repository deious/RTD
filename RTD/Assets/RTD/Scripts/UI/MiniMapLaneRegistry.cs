using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class MiniMapLaneRegistry : MonoBehaviour
{
    public static MiniMapLaneRegistry Instance { get; private set; }

    [Header("Mode")]
    [SerializeField] private bool forceSoloMode = false;

    [Header("Solo")]
    [SerializeField] private MiniMapPathRenderer soloPathRenderer;
    [SerializeField] private MiniMapMonsterUIRenderer soloMonsterRenderer;

    [Header("Lane Renderers (0..3)")]
    [SerializeField] private List<MiniMapMonsterUIRenderer> monsterRenderers = new(4);
    [SerializeField] private List<MiniMapPathRenderer> pathRenderers = new(4);

    private readonly bool[] _visibleLane = new bool[4] { true, true, true, true };
    
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void SetForceSoloMode(bool solo)
    {
        forceSoloMode = solo;

        // ✅ 솔로 몬스터 렌더러가 솔로 pathRenderer를 보도록 보장(매우 중요)
        if (soloMonsterRenderer != null)
            soloMonsterRenderer.SetPathRenderer(soloPathRenderer);
    }

    private MiniMapPathRenderer GetPathRenderer(int laneId)
    {
        laneId = Mathf.Clamp(laneId, 0, 3);
        if (forceSoloMode) return soloPathRenderer;
        return (pathRenderers != null && laneId < pathRenderers.Count) ? pathRenderers[laneId] : null;
    }
    
    private bool IsLaneVisible(int laneId)
    {
        laneId = Mathf.Clamp(laneId, 0, 3);
        return _visibleLane[laneId];
    }

    public MiniMapMonsterUIRenderer GetMonsterRenderer(int laneId)
    {
        laneId = Mathf.Clamp(laneId, 0, 3);
        if (forceSoloMode) return soloMonsterRenderer;
        return (monsterRenderers != null && laneId < monsterRenderers.Count) ? monsterRenderers[laneId] : null;
    }

    /// <summary>
    /// ✅ 맵이 런타임에 재빌드/교체/플레이어수 변경될 때 "이것만" 호출하는 게 정답 루트.
    /// </summary>
    public void RebindAllAfterMapBuild(GameObject[] spawnedMaps, Transform[] laneAnchors)
    {
        if (spawnedMaps == null || spawnedMaps.Length < 4) return;

        if (forceSoloMode)
        {
            int lane = Mathf.Clamp(MultiplayerContext.MyLaneId, 0, 3);

            var pr = soloPathRenderer;
            var map = spawnedMaps[lane];
            if (!pr || !map) return;

            var wp = map.GetComponentInChildren<WaypointPath>(true);
            if (!wp) return;

            pr.Bind(wp, map.transform, rebuildNow: true);

            // ✅ 솔로 몬스터도 솔로 패스 기준으로 좌표 변환해야 함
            if (soloMonsterRenderer != null)
                soloMonsterRenderer.SetPathRenderer(pr);

            return;
        }

        for (int lane = 0; lane < 4; lane++)
        {
            var pr = GetPathRenderer(lane);
            var map = spawnedMaps[lane];
            if (!pr || !map) continue;

            var wp = map.GetComponentInChildren<WaypointPath>(true);
            if (!wp) continue;

            pr.Bind(wp, map.transform, rebuildNow: true);
        }

        // ✅ 멀티에서 lane별 monster renderer는 이미 lane별 pathRenderer를 참조해야 함
        for (int lane = 0; lane < 4; lane++)
        {
            var mr = GetMonsterRenderer(lane);
            var pr = GetPathRenderer(lane);
            if (mr != null) mr.SetPathRenderer(pr);
        }
    }

    public async UniTaskVoid RebindAllMonsterReportersAsync()
    {
        // ✅ UI slot on/off 직후는 Rect/레이아웃이 0인 프레임이 있어서 2프레임 대기 유지
        await UniTask.Yield(PlayerLoopTiming.PostLateUpdate);
        await UniTask.Yield(PlayerLoopTiming.PostLateUpdate);

        var reporters = Object.FindObjectsByType<MiniMapMonsterReporter>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        if (reporters == null || reporters.Length == 0)
            return;

        int myWorldSlot = Mathf.Clamp(MultiplayerContext.MyLaneId, 0, 3);
        
        for (int i = 0; i < reporters.Length; i++)
        {
            var rep = reporters[i];
            if (!rep) continue;

            var ai = rep.GetComponentInParent<MonsterAI>(true);
            if (!ai)
            {
                rep.SetRenderer(null);
                continue;
            }
            
            int worldLane = Mathf.Clamp(ai.WorldSlotId, 0, 3);

            if (forceSoloMode)
            {
                if (soloMonsterRenderer == null)
                {
                    rep.SetRenderer(null);
                    continue;
                }
                
                bool isProxy = (ai.GetComponent<ProxyMonster>() != null);

                if (isProxy)
                    rep.SetRenderer(null);
                else
                    rep.SetRenderer(soloMonsterRenderer);

                continue;
            }

            if (!IsLaneVisible(worldLane))
            {
                rep.SetRenderer(null);
                continue;
            }

            var target = GetMonsterRenderer(worldLane);
            
            rep.SetRenderer(target);
        }

        Debug.Log("[MiniMapLaneRegistry] RebindAllMonsterReportersAsync done.");
    }
    
    public void RebindAllMiniMapsAfterMapBuild()
    {
        var bootstrap = FindFirstObjectByType<LaneMapBootstrap>(FindObjectsInactive.Include);
        if (bootstrap == null) return;

        var maps = bootstrap.GetSpawnedMaps();
        var anchors = bootstrap.GetLaneAnchors();

        RebindAllAfterMapBuild(maps, anchors);
    }
    
    public void SetVisibleLanesForPlayerCount(int playerCount, bool twoPlayersTopRowOnly)
    {
        playerCount = Mathf.Clamp(playerCount, 1, 4);
        
        for (int i = 0; i < 4; i++)
            _visibleLane[i] = (i < playerCount);

        if (playerCount == 2 && twoPlayersTopRowOnly)
        {
            _visibleLane[0] = true;
            _visibleLane[1] = true;
            _visibleLane[2] = false;
            _visibleLane[3] = false;
        }
    }
    
    public void BindUISlotToLane(int uiSlotIndex, int laneId, GameObject[] spawnedMaps)
    {
        uiSlotIndex = Mathf.Clamp(uiSlotIndex, 0, 3);
        laneId = Mathf.Clamp(laneId, 0, 3);

        if (spawnedMaps == null || spawnedMaps.Length < 4)
            return;

        if (pathRenderers == null || uiSlotIndex >= pathRenderers.Count)
            return;

        if (monsterRenderers == null || uiSlotIndex >= monsterRenderers.Count)
            return;

        var slotPathRenderer = pathRenderers[uiSlotIndex];
        var slotMonsterRenderer = monsterRenderers[uiSlotIndex];
        var map = spawnedMaps[laneId];

        if (slotPathRenderer == null || slotMonsterRenderer == null || map == null)
            return;

        var wp = map.GetComponentInChildren<WaypointPath>(true);
        if (wp == null)
            return;
        
        slotPathRenderer.Bind(wp, map.transform, rebuildNow: true);
        slotMonsterRenderer.SetPathRenderer(slotPathRenderer);
    }
    
    public void SetLaneActive(int laneId, bool active)
    {
        laneId = Mathf.Clamp(laneId, 0, 3);
        _visibleLane[laneId] = active;
    }

    public void ClearLaneMonsterRenderer(int laneId)
    {
        laneId = Mathf.Clamp(laneId, 0, 3);

        if (forceSoloMode)
            return;

        if (monsterRenderers == null || laneId >= monsterRenderers.Count)
            return;

        var mr = monsterRenderers[laneId];
        if (mr != null)
            mr.ClearAllRegisteredMonsters(true);
    }
}
