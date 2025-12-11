using UnityEngine;
using System.Collections.Generic;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    public int width = 16;
    public int height = 12;
    public float cellSize = 1f;

    public GameObject buildablePrefab;
    public GameObject pathPrefab;

    public Transform tileParent;

    [Header("Path Tiles")] public List<Vector2Int> pathTiles = new List<Vector2Int>();

    [Header("Waypoints")] public List<Transform> waypoints = new List<Transform>();

    private GridTile[,] tiles;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("여러 개의 GridManager가 씬에 있습니다.");
        }

        Instance = this;

        GenerateGrid();
        GenerateWaypoints();
    }

    private void GenerateGrid()
    {
        tiles = new GridTile[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector2Int pos = new Vector2Int(x, y);
                bool isPath = pathTiles.Contains(pos);

                GameObject prefab = isPath ? pathPrefab : buildablePrefab;
                TileType tileType = isPath ? TileType.Path : TileType.Buildable;

                CreateTile(pos, prefab, tileType);
            }
        }
    }

    private void CreateTile(Vector2Int gridPos, GameObject prefab, TileType tileType)
    {
        Vector3 worldPos = new Vector3(gridPos.x * cellSize, 0f, gridPos.y * cellSize);

        GameObject obj = Instantiate(prefab, worldPos, Quaternion.identity, tileParent);

        GridTile tile = obj.GetComponent<GridTile>();
        if (tile == null)
        {
            tile = obj.AddComponent<GridTile>();
        }

        tile.Init(gridPos, tileType);

        tiles[gridPos.x, gridPos.y] = tile;
    }

    private void GenerateWaypoints()
    {
        waypoints.Clear();

        if (tiles == null)
        {
            Debug.LogError("타일이 아직 생성되지 않았습니다. GenerateGrid 이후에 호출해야 합니다.");
            return;
        }

        foreach (Vector2Int gridPos in pathTiles)
        {
            if (gridPos.x < 0 || gridPos.x >= width || gridPos.y < 0 || gridPos.y >= height)
            {
                Debug.LogWarning($"PathTile {gridPos} 좌표가 Grid 범위를 벗어났습니다.");
                continue;
            }

            GridTile tile = tiles[gridPos.x, gridPos.y];
            if (tile == null)
            {
                Debug.LogWarning($"PathTile {gridPos} 위치에 타일이 없습니다.");
                continue;
            }

            waypoints.Add(tile.transform);
        }

        if (waypoints.Count == 0)
        {
            Debug.LogWarning("Waypoint가 하나도 생성되지 않았습니다. pathTiles를 확인하세요.");
        }
    }

    public Transform GetWaypoint(int index)
    {
        if (index < 0 || index >= waypoints.Count)
            return null;

        return waypoints[index];
    }
    
    public int WaypointCount => waypoints.Count;
    
    #if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (pathTiles == null || pathTiles.Count == 0)
                return;
    
            Gizmos.color = Color.yellow;
    
            Vector3 prevPos = Vector3.zero;
            bool hasPrev = false;
    
            foreach (Vector2Int gridPos in pathTiles)
            {
                Vector3 worldPos = new Vector3(gridPos.x * cellSize, 0f, gridPos.y * cellSize);
                Vector3 drawPos = worldPos + Vector3.up * 0.3f;
    
                Gizmos.DrawSphere(drawPos, 0.1f);
    
                if (hasPrev)
                {
                    Gizmos.DrawLine(prevPos, drawPos);
                }
    
                prevPos = drawPos;
                hasPrev = true;
            }
        }
    #endif
}