using UnityEngine;
using UnityEngine.InputSystem;
using Cysharp.Threading.Tasks;

public class TowerManager : MonoBehaviour
{
    public static TowerManager Instance { get; private set; }

    [Header("Placement")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private LayerMask tileLayerMask;
    
    [Header("Random Build")]
    [SerializeField] private TowerGrade buildRollGrade = TowerGrade.Normal;
    [SerializeField] private TowerData[] buildPool;
    
    public enum CombineMode
    {
        Exact,
        Random
    }
    
    [Header("Combine")]
    [SerializeField] private CombineMode combineMode = CombineMode.Exact;
    
    private TowerBase _selectedTower;
    private bool _combineBusy;

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
        if (_combineBusy) 
            return;
        
        if (Mouse.current == null) 
            return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            HandleClick();
        }
        
        if (Keyboard.current != null && Keyboard.current.vKey.wasPressedThisFrame)
        {
            combineMode = (combineMode == CombineMode.Exact) ? CombineMode.Random : CombineMode.Exact;
            Debug.Log($"[CombineMode] {combineMode}");
        }
        
        if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame)
        {
            if (_combineBusy)
                return;
            
            TryCombineSelectedTowerAsync().Forget();
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

    private TowerData RollMergeResultByGrade(TowerGrade targetGrade)
    {
        if (buildPool == null || buildPool.Length == 0)
        {
            Debug.LogError("[TowerManager] buildPool is empty. Cannot roll merge result.");
            return null;
        }
        
        if (TowerDatabase.Instance != null)
        {
            return TowerDatabase.Instance.GetRandomByGrade(targetGrade);
        }
        
        int safety = 0;
        while (safety < 100)
        {
            int idx = Random.Range(0, buildPool.Length);
            TowerData d = buildPool[idx];
            if (d != null && d.grade == targetGrade)
                return d;
            safety++;
        }

        Debug.LogWarning("[TowerManager] No TowerData matched targetGrade for merge result.");
        return null;
    }
    
    private void FindExactTowers(TowerData grade, System.Collections.Generic.List<TowerBase> outList)
    {
        outList.Clear();

        TowerBase[] allTowers = FindObjectsOfType<TowerBase>();
        foreach (var t in allTowers)
        {
            TowerData d = t.GetData();
            if (d == null)
                continue;

            if (d.towerId == grade.towerId && d.grade == grade.grade)
                outList.Add(t);
        }
    }
    private void FindSameGradeTowers(TowerGrade grade, System.Collections.Generic.List<TowerBase> outList)
    {
        outList.Clear();

        TowerBase[] allTowers = FindObjectsOfType<TowerBase>();
        foreach (var t in allTowers)
        {
            TowerData d = t.GetData();
            if (d == null)
                continue;

            if (d.grade == grade)
                outList.Add(t);
        }
    }
    
    private async UniTaskVoid TryCombineSelectedTowerAsync()
    {
        if (_combineBusy)
            return;

        _combineBusy = true;
        
        try
        {
            if (_selectedTower == null)
            {
                Debug.Log("선택된 타워가 없습니다.");
                return;
            }
            
            TowerData selectedData = _selectedTower.GetData();
            if (selectedData == null)
            {
                Debug.Log("현재 타워 데이터가 없습니다.");
                return;
            }
            
            TowerGrade curGrade = selectedData.grade;
            if (!TowerGradeHelper.TryGetNextGrade(curGrade, out TowerGrade nextGrade))
            {
                Debug.Log("더 이상 합성할 수 없는 등급입니다.");
                return;
            }
            
            if (TowerDatabase.Instance == null)
            {
                Debug.LogError("TowerDatabase 인스턴스가 없습니다.");
                return;
            }
            
            // 1) 후보 수집은 모드에 따라 다름
            var candidates = new System.Collections.Generic.List<TowerBase>();
            
            if (combineMode == CombineMode.Exact)
                FindExactTowers(selectedData, candidates);
            else
                FindSameGradeTowers(curGrade, candidates);
            
            if (candidates.Count < 3)
            {
                if (combineMode == CombineMode.Exact)
                    Debug.Log($"[Exact Combine] 조건 미충족: {selectedData.towerId} ({candidates.Count}/3)");
                else
                    Debug.Log($"[Random Combine] 조건 미충족: grade={curGrade} ({candidates.Count}/3)");
                return;
            }
            
            // 2) 3개 구성(선택 타워 포함 + 나머지 2개)
            var mergeList = new System.Collections.Generic.List<TowerBase>(3);
            mergeList.Add(_selectedTower);
            
            for (int i = 0; i < candidates.Count && mergeList.Count < 3; i++)
            {
                if (candidates[i] == _selectedTower)
                    continue;
            
                mergeList.Add(candidates[i]);
            }
            
            if (mergeList.Count < 3)
            {
                Debug.Log("합성 대상 3개를 구성하지 못했습니다.");
                return;
            }
            
            // 3) 결과 TowerData 결정(모드에 따라 다름)
            TowerData resultData = null;
            
            if (combineMode == CombineMode.Exact)
            {
                string nextTowerId = GetNextTowerId(selectedData.towerId, nextGrade);
                resultData = TowerDatabase.Instance.Get(nextTowerId);
            
                if (resultData == null)
                {
                    Debug.LogError($"다음 TowerData를 찾을 수 없습니다: {nextTowerId}");
                    return;
                }
            }
            else
            {
                resultData = RollMergeResultByGrade(nextGrade);
                if (resultData == null)
                {
                    Debug.LogError($"혼합 합성 결과 TowerData를 뽑지 못했습니다. targetGrade={nextGrade}");
                    return;
                }
            }
            
            if (TowerMergeVFX.Instance == null)
            {
                Debug.LogError("TowerMergeVFX.Instance가 없습니다. 씬에 TowerMergeVFX 오브젝트가 있어야 합니다.");
                return;
            }
            
            Transform[] sources =
            {
                mergeList[0] != null ? mergeList[0].transform : null,
                mergeList[1] != null ? mergeList[1].transform : null,
                mergeList[2] != null ? mergeList[2].transform : null
            };
            
            Vector3 mergePoint = _selectedTower.transform.position;
            
            Vector3 keepScale = _selectedTower.transform.localScale;
            
            await TowerMergeVFX.Instance.PlayMergeAsync(
                sources,
                mergePoint,
                () =>
                {
                    for (int i = 0; i < mergeList.Count; i++)
                    {
                        if (mergeList[i] == null) continue;
                        if (mergeList[i] == _selectedTower) continue;
                        Destroy(mergeList[i].gameObject);
                    }
            
                    if (_selectedTower == null) return;
                    
                    _selectedTower.transform.localScale = keepScale;
            
                    _selectedTower.SetData(resultData);
                    _selectedTower.SetSelected(true);
            
                    _selectedTower.PlaySpawnFeedback();
                }
            );
        }
        finally
        {
            _combineBusy = false;
        }
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
