using UnityEngine;

public enum TileType
{
    Buildable,
    Path,
    Blocked
}

public class GridTile : MonoBehaviour
{
    public Vector2Int GridPos;
    public TileType TileType;
}