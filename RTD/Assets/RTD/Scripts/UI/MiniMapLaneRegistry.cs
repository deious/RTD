using System.Collections.Generic;
using UnityEngine;

public class MiniMapLaneRegistry : MonoBehaviour
{
    public static MiniMapLaneRegistry Instance { get; private set; }
    
    [Header("Mode")]
    [Tooltip("true면 솔로 미니맵(큰 1개), false면 멀티(분할)")]
    [SerializeField] private bool forceSoloMode = false;

    [Header("Solo (1 big minimap)")]
    [SerializeField] private MiniMapPathRenderer soloPathRenderer;
    [SerializeField] private MiniMapMonsterUIRenderer soloMonsterRenderer;

    [Header("Lane Renderers (index 0..3 => P1..P4)")]
    [SerializeField] private List<MiniMapMonsterUIRenderer> monsterRenderers = new List<MiniMapMonsterUIRenderer>(4);
    [SerializeField] private List<MiniMapPathRenderer> pathRenderers = new List<MiniMapPathRenderer>(4);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    
    private MiniMapPathRenderer GetPathRenderer(int laneId)
    {
        laneId = Mathf.Clamp(laneId, 0, 3);

        if (forceSoloMode)
            return soloPathRenderer;

        if (pathRenderers != null && laneId < pathRenderers.Count)
            return pathRenderers[laneId];

        return null;
    }
    
    private MiniMapPathRenderer FindMiniMapPathRendererForLane(int lane)
    {
        // 예: 미니맵 슬롯 오브젝트에 laneId로 달려있다 가정
        return FindFirstObjectByType<MiniMapPathRenderer>(FindObjectsInactive.Include);
        // TODO: 실제로는 lane별로 정확히 찾도록 구현 필요
    }

    private WaypointPath FindWaypointPathForLane(int lane)
    {
        // 예: LaneRoot_P1..P4 아래에 WaypointPath가 있다 가정
        Transform root = FindLaneRoot(lane);
        return root ? root.GetComponentInChildren<WaypointPath>(true) : null;
    }

    private Transform FindLaneRoot(int lane)
    {
        // LaneRoot_P1..P4 네이밍이면:
        string name = $"LaneRoot_P{lane + 1}";
        GameObject go = GameObject.Find(name);
        return go ? go.transform : null;
    }

    public MiniMapMonsterUIRenderer GetMonsterRenderer(int laneId)
    {
        laneId = Mathf.Clamp(laneId, 0, 3);

        if (forceSoloMode)
            return soloMonsterRenderer;

        if (monsterRenderers != null && laneId < monsterRenderers.Count)
            return monsterRenderers[laneId];

        return null;
    }
    
    public void RebindAllMiniMapsAfterMapBuild()
    {
        for (int lane = 0; lane < 4; lane++)
        {
            MiniMapPathRenderer pr = FindMiniMapPathRendererForLane(lane);
            WaypointPath wp = FindWaypointPathForLane(lane);
            Transform root = FindLaneRoot(lane);

            if (pr && wp && root)
                pr.Bind(wp, root, rebuildNow: true);
        }
    }
    
    public void RebindAllAfterMapBuild(GameObject[] spawnedMaps, Transform[] laneAnchors)
    {
        if (spawnedMaps == null || spawnedMaps.Length < 4) return;
        if (laneAnchors == null || laneAnchors.Length < 4) return;

        for (int lane = 0; lane < 4; lane++)
        {
            var pr = GetPathRenderer(lane);
            var map = spawnedMaps[lane];
            var anchor = laneAnchors[lane];

            if (!pr || !map || !anchor)
                continue;
            
            var wp = map.GetComponentInChildren<WaypointPath>(true);
            if (!wp)
            {
                Debug.LogWarning($"[MiniMapLaneRegistry] lane={lane} WaypointPath not found in {map.name}");
                continue;
            }
            
            pr.Bind(wp, anchor, rebuildNow: true);
        }
    }
    
    public void SetForceSoloMode(bool solo)
    {
        forceSoloMode = solo;
    }
}