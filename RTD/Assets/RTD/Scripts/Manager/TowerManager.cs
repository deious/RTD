using UnityEngine;
using UnityEngine.InputSystem;

public class TowerManager : MonoBehaviour
{
    public static TowerManager Instance { get; private set; }

    [Header("Placement")]
    [SerializeField] private Camera mainCamera;
    //[SerializeField] private GameObject towerPrefab;
    //[SerializeField] private int towerCost = 10;
    [SerializeField] private LayerMask tileLayerMask;
    
    [Header("Random Build")]
    [SerializeField] private TowerGrade buildRollGrade = TowerGrade.Normal;
    [SerializeField] private TowerData[] buildPool;
    
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
        
        if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame)
        {
            TryCombineSelectedTower();
        }
    }

    private void HandleClick()
    {
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = mainCamera.ScreenPointToRay(mousePos);

        if (Physics.Raycast(ray, out RaycastHit hitTower, 1000f, LayerMask.GetMask("Tower")))
        {
            TowerBase tower = hitTower.collider.GetComponentInParent<TowerBase>();
            if (tower != null)
            {
                SelectTower(tower);
                return;
            }
        }
        
        if (Physics.Raycast(ray, out RaycastHit hitTile, 1000f, tileLayerMask))
        {
            GridTile tile = hitTile.collider.GetComponent<GridTile>();
            if (tile != null)
            {
                OnTileClicked(tile);
                return;
            }
        }

        ClearSelection();
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
        
        TowerData rolledData = RollBuildTowerData();
        if (rolledData == null)
            return;

        if (rolledData.towerPrefab == null)
        {
            Debug.LogError($"[TowerManager] towerPrefab is null in TowerData: {rolledData.name}");
            return;
        }

        int cost = rolledData.buildCost;
        
        if (GameManager.Instance.Gold < cost)
        {
            Debug.Log("골드가 부족합니다.");
            return;
        }
        
        Vector3 spawnPos = tile.transform.position;
        GameObject towerObj = Instantiate(rolledData.towerPrefab, spawnPos, Quaternion.identity);

        TowerBase tower = towerObj.GetComponent<TowerBase>();
        if (tower == null)
        {
            Debug.LogError("rolledData.towerPrefab에 TowerBase 컴포넌트가 없습니다.");
            Destroy(towerObj);
            return;
        }
        
        tower.SetData(rolledData);
        
        tile.SetTower(tower);
        tower.SetTile(tile);
        GameManager.Instance.AddGold(-cost);

        Debug.Log($"[Build] Rolled: {rolledData.towerId} ({rolledData.grade}), cost={cost}");
    }
    
    private TowerData RollBuildTowerData()
    {
        if (buildPool == null || buildPool.Length == 0)
        {
            Debug.LogError("[TowerManager] buildPool is empty. Assign TowerData assets in Inspector.");
            return null;
        }
        
        int safety = 0;
        while (safety < 50)
        {
            int idx = Random.Range(0, buildPool.Length);
            TowerData d = buildPool[idx];
            if (d != null && d.grade == buildRollGrade)
                return d;

            safety++;
        }

        Debug.LogWarning("[TowerManager] No TowerData matched buildRollGrade. Check buildPool contents.");
        return null;
    }

    
    private void FindSameTowers(TowerData 기준, System.Collections.Generic.List<TowerBase> outList)
    {
        outList.Clear();

        TowerBase[] allTowers = FindObjectsOfType<TowerBase>();
        foreach (var t in allTowers)
        {
            TowerData d = t.GetData();
            if (d == null)
                continue;

            if (d.towerId == 기준.towerId && d.grade == 기준.grade)
            {
                outList.Add(t);
            }
        }
    }
    
    private void TryCombineSelectedTower()
    {
        if (_selectedTower == null)
        {
            Debug.Log("선택된 타워가 없습니다.");
            return;
        }

        TowerData currentData = _selectedTower.GetData();
        if (currentData == null)
        {
            Debug.Log("현재 타워 데이터가 없습니다.");
            return;
        }
        
        if (!TowerGradeHelper.TryGetNextGrade(currentData.grade, out TowerGrade nextGrade))
        {
            Debug.Log("더 이상 합성할 수 없는 등급입니다.");
            return;
        }
        
        var sameTowers = new System.Collections.Generic.List<TowerBase>();
        FindSameTowers(currentData, sameTowers);

        if (sameTowers.Count < 3)
        {
            Debug.Log($"합성 조건 미충족: {currentData.towerId} ({sameTowers.Count}/3)");
            return;
        }
        
        string nextTowerId = GetNextTowerId(currentData.towerId, nextGrade);
        TowerData nextData = TowerDatabase.Instance.Get(nextTowerId);
        if (nextData == null)
        {
            Debug.LogError($"다음 TowerData를 찾을 수 없습니다: {nextTowerId}");
            return;
        }
        
        int removed = 0;
        for (int i = 0; i < sameTowers.Count && removed < 2; i++)
        {
            if (sameTowers[i] == _selectedTower)
                continue;

            Destroy(sameTowers[i].gameObject);
            removed++;
        }
        
        _selectedTower.SetData(nextData);
        _selectedTower.SetSelected(true);

        Debug.Log($"합성 성공: {currentData.towerId} → {nextTowerId}");
    }

    private string GetNextTowerId(string currentId, TowerGrade nextGrade)
    {
        int idx = currentId.LastIndexOf('_');
        if (idx < 0)
            return currentId;

        string baseId = currentId.Substring(0, idx);
        return $"{baseId}_{nextGrade.ToString().ToLower()}";
    }
}
