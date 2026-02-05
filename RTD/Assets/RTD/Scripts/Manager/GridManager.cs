using System;
using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }
    public static event Action OnMapBuilt;
    
    public int width = 16;
    public int height = 12;
    public float cellSize = 10f;

    [Header("Scene Tiles Root (MapRoot/Tiles)")]
    public Transform tileParent;

    [Header("Waypoints")]
    public List<Transform> waypoints = new List<Transform>();

    [Header("Waypoint Source")]
    [SerializeField] private WaypointPath waypointPath;

    [Header("Lanes")]
    [SerializeField] private int laneCount = 3;
    [SerializeField] private float laneOffset = 0.35f;

    [Header("Debug")]
    [SerializeField] private bool log = true;

    private GridTile[,] tiles;

    public int LaneCount => laneCount;
    public int WaypointCount => waypoints.Count;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("여러 개의 GridManager가 씬에 있습니다.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        // ✅ 싱글/기존 구조도 살리기: 인스펙터에 tileParent/waypointPath가 잡혀 있으면 즉시 로드
        if (tileParent != null && waypointPath != null)
        {
            BuildTilesFromScene();
            LoadWaypointsFromPath();

            if (log)
                Debug.Log($"[GridManager] Start auto-load ok. tileParent={tileParent.name}, waypoints={waypoints.Count}");
        }
        else
        {
            if (log)
                Debug.Log($"[GridManager] Start auto-load skipped. tileParent={(tileParent ? tileParent.name : "null")}, waypointPath={(waypointPath ? waypointPath.name : "null")}");
        }
    }

    /// <summary>
    /// ✅ A-1 핵심: 현재 사용할 MapRoot(내 lane의 MapRoot)로 GridManager 데이터 소스를 바인딩한다.
    /// mapRoot 하위에 "Tiles", "WaypointPath"가 있어야 함.
    /// </summary>
    public void BindToMapRoot(Transform mapRoot)
    {
        if (mapRoot == null)
        {
            Debug.LogError("[GridManager] BindToMapRoot failed: mapRoot is null");
            return;
        }

        // 1) Tiles 찾기
        var tilesTr = mapRoot.Find("Tiles");
        if (tilesTr == null)
            tilesTr = mapRoot.Find("MapRoot/Tiles"); // 혹시 구조가 이렇게 되어 있으면

        // 2) WaypointPath 찾기
        var wpObj = mapRoot.GetComponentInChildren<WaypointPath>(true);

        if (tilesTr == null)
        {
            Debug.LogError($"[GridManager] BindToMapRoot failed: Tiles not found under {mapRoot.name}");
            return;
        }

        if (wpObj == null)
        {
            Debug.LogError($"[GridManager] BindToMapRoot failed: WaypointPath not found under {mapRoot.name}");
            return;
        }

        tileParent = tilesTr;
        waypointPath = wpObj;

        BuildTilesFromScene();
        bool ok = LoadWaypointsFromPath();

        MiniMapLaneRegistry.Instance?.RebindAllMiniMapsAfterMapBuild();
        Debug.Log($"[GridManager] Bound to mapRoot={mapRoot.name} tiles={tileParent.name} waypointPath={waypointPath.name} waypoints={waypoints.Count} ok={ok}");
        //OnMapBuilt?.Invoke();
    }

    private void BuildTilesFromScene()
    {
        tiles = new GridTile[width, height];

        if (tileParent == null)
        {
            Debug.LogError("[GridManager] tileParent is null. MapRoot/Tiles를 할당하세요.");
            return;
        }

        var found = tileParent.GetComponentsInChildren<GridTile>(true);

        foreach (var t in found)
        {
            Vector2Int p = t.GridPos;

            if (p.x < 0 || p.x >= width || p.y < 0 || p.y >= height)
            {
                Debug.LogWarning($"[GridManager] GridPos out of range: {t.name} pos={p}");
                continue;
            }

            if (tiles[p.x, p.y] != null && tiles[p.x, p.y] != t)
            {
                Debug.LogWarning($"[GridManager] Duplicate tile at {p}: {tiles[p.x, p.y].name} and {t.name}");
                continue;
            }

            tiles[p.x, p.y] = t;
        }
    }

    private bool LoadWaypointsFromPath()
    {
        waypoints.Clear();

        if (waypointPath == null)
            return false;

        if (waypointPath.points == null || waypointPath.points.Count == 0)
        {
            Debug.LogError("[GridManager] WaypointPath가 비어있습니다. points를 채워주세요.");
            return true; // waypointPath는 있는데 비어있음
        }

        for (int i = 0; i < waypointPath.points.Count; i++)
        {
            Transform p = waypointPath.points[i];
            if (p == null)
            {
                Debug.LogWarning($"[GridManager] WaypointPath points[{i}] is null");
                continue;
            }
            waypoints.Add(p);
        }

        if (waypoints.Count == 0)
            Debug.LogError("[GridManager] WaypointPath에서 유효한 웨이포인트를 하나도 가져오지 못했습니다.");

        return true;
    }

    public Transform GetWaypoint(int index)
    {
        if (index < 0 || index >= waypoints.Count)
            return null;
        return waypoints[index];
    }

    public Vector3 GetLaneTargetPos(int waypointIndex, int laneIndex)
    {
        if (waypoints == null || waypoints.Count == 0)
            return Vector3.zero;

        waypointIndex = Mathf.Clamp(waypointIndex, 0, waypoints.Count - 1);
        laneIndex = Mathf.Clamp(laneIndex, 0, Mathf.Max(0, laneCount - 1));

        Transform wp = waypoints[waypointIndex];
        Vector3 wpPos = wp.position;

        Vector3 forward;
        if (waypointIndex < waypoints.Count - 1)
            forward = (waypoints[waypointIndex + 1].position - wpPos);
        else if (waypointIndex > 0)
            forward = (wpPos - waypoints[waypointIndex - 1].position);
        else
            forward = Vector3.forward;

        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;

        forward.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

        float center = (laneCount - 1) * 0.5f;
        float laneSigned = laneIndex - center;

        return wpPos + right * (laneSigned * laneOffset);
    }
}
