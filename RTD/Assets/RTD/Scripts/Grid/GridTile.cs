using UnityEngine;

public enum TileType
{
    Buildable,
    Path,
    Blocked
}

public class GridTile : MonoBehaviour
{
    [Header("Tile Info")]
    public Vector2Int GridPos { get; private set; }
    public TileType TileType { get; private set; }

    [Header("Visual")]
    public Color hoverColor = Color.yellow;

    private Renderer _renderer;
    private Color _originalColor;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        if (_renderer != null)
        {
            _originalColor = _renderer.material.color;
        }
    }
    
    public void Init(Vector2Int gridPos, TileType tileType)
    {
        GridPos = gridPos;
        TileType = tileType;
    }

    public void SetHighlight(bool active)
    {
        if (_renderer == null) return;

        _renderer.material.color = active ? hoverColor : _originalColor;
    }

    public void OnSelected()
    {
        Debug.Log($"Tile Selected - GridPos: {GridPos}, Type: {TileType}, WorldPos: {transform.position}");
    }
}