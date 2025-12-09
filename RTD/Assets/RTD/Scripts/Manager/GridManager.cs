using UnityEngine;
using System.Collections.Generic;

public class GridManager : MonoBehaviour
{
    public int width = 16;
    public int height = 12;
    public float cellSize = 1f;

    public GameObject buildablePrefab;
    public GameObject pathPrefab;

    public Transform tileParent;

    [Header("Path Tiles")]
    public List<Vector2Int> pathTiles = new List<Vector2Int>();

    private GridTile[,] tiles;

    private void Awake()
    {
        GenerateGrid();
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
}