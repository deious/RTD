using UnityEngine;
using UnityEngine.InputSystem;

public class TowerManager : MonoBehaviour
{
    public static TowerManager Instance { get; private set; }

    [Header("Placement")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private GameObject towerPrefab;
    [SerializeField] private int towerCost = 10;
    [SerializeField] private LayerMask tileLayerMask;
    
    private TowerBase _selectedTower;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
    }

    private void Update()
    {
        if (Mouse.current == null) 
            return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            HandleClick();
        }
    }

    private void HandleClick()
    {
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = mainCamera.ScreenPointToRay(mousePos);

        if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
        {
            TowerBase tower = hit.collider.GetComponentInParent<TowerBase>();
            if (tower != null)
            {
                SelectTower(tower);
                return;
            }
            
            GridTile tile = hit.collider.GetComponent<GridTile>();
            if (tile != null)
            {
                OnTileClicked(tile);
                return;
            }
            
            ClearSelection();
        }
        else
        {
            ClearSelection();
        }
    }
    
    private void SelectTower(TowerBase tower)
    {
        if (_selectedTower == tower)
            return;

        if (_selectedTower != null)
            _selectedTower.SetSelected(false);

        _selectedTower = tower;
        _selectedTower.SetSelected(true);
    }

    private void ClearSelection()
    {
        if (_selectedTower != null)
            _selectedTower.SetSelected(false);

        _selectedTower = null;
    }
    
    public void OnTileClicked(GridTile tile)
    {
        TryPlaceTower(tile);
    }

    private void TryPlaceTower(GridTile tile)
    {
        if (!tile.IsEmpty)
        {
            Debug.Log("설치 불가 타일이거나 이미 타워가 있습니다.");
            return;
        }

        if (GameManager.Instance == null)
        {
            Debug.LogError("GameManager 인스턴스가 없습니다.");
            return;
        }

        if (GameManager.Instance.Gold < towerCost)
        {
            Debug.Log("골드가 부족합니다.");
            return;
        }

        Vector3 spawnPos = tile.transform.position;
        GameObject towerObj = Instantiate(towerPrefab, spawnPos, Quaternion.identity);

        TowerBase tower = towerObj.GetComponent<TowerBase>();
        if (tower == null)
        {
            Debug.LogError("towerPrefab에 Tower 컴포넌트가 없습니다.");
            Destroy(towerObj);
            return;
        }

        tile.SetTower(tower);
        GameManager.Instance.AddGold(-towerCost);
    }
}
