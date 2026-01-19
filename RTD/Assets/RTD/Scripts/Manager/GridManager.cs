using UnityEngine;
using System.Collections.Generic;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

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

    private GridTile[,] tiles;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("여러 개의 GridManager가 씬에 있습니다.");
            Destroy(gameObject);
            return;
        }

        Instance = this;

        BuildTilesFromScene();
        
        if (!LoadWaypointsFromPath())
        {
            Debug.LogError("WaypointPath가 지정되지 않았습니다. MapRoot/WaypointPath를 할당하세요.");
        }
    }

    private void BuildTilesFromScene()
    {
        tiles = new GridTile[width, height];

        if (tileParent == null)
        {
            Debug.LogError("tileParent가 비어 있습니다. MapRoot/Tiles를 할당하세요.");
            return;
        }

        var found = tileParent.GetComponentsInChildren<GridTile>(true);

        foreach (var t in found)
        {
            Vector2Int p = t.GridPos;

            if (p.x < 0 || p.x >= width || p.y < 0 || p.y >= height)
            {
                Debug.LogWarning($"GridPos out of range: {t.name} pos={p}");
                continue;
            }

            if (tiles[p.x, p.y] != null && tiles[p.x, p.y] != t)
            {
                Debug.LogWarning($"Duplicate tile at {p}: {tiles[p.x, p.y].name} and {t.name}");
                continue;
            }

            tiles[p.x, p.y] = t;
        }

        // 누락 체크(선택)
        for (int x = 0; x < width; x++)
        for (int y = 0; y < height; y++)
        {
            if (tiles[x, y] == null)
                Debug.LogWarning($"Missing GridTile at ({x},{y}) under tileParent={tileParent.name}");
        }
    }

    private bool LoadWaypointsFromPath()
    {
        waypoints.Clear();

        if (waypointPath == null)
            return false;

        if (waypointPath.points == null || waypointPath.points.Count == 0)
        {
            Debug.LogError("WaypointPath가 비어있습니다. points를 채워주세요.");
            return true;
        }

        for (int i = 0; i < waypointPath.points.Count; i++)
        {
            Transform p = waypointPath.points[i];
            if (p == null)
            {
                Debug.LogWarning($"WaypointPath points[{i}]가 null 입니다.");
                continue;
            }
            waypoints.Add(p);
        }

        if (waypoints.Count == 0)
            Debug.LogError("WaypointPath에서 유효한 웨이포인트를 하나도 가져오지 못했습니다.");

        return true;
    }

    public Transform GetWaypoint(int index)
    {
        if (index < 0 || index >= waypoints.Count)
            return null;
        return waypoints[index];
    }

    public int WaypointCount => waypoints.Count;

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

    public int LaneCount => laneCount;
}
