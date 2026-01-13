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
    
    [Header("Trait Data")]
    [SerializeField] private TowerTraitDatabase traitDatabase;
    
    public enum CombineMode
    {
        Exact,
        Random
    }
    
    [Header("Combine")]
    [SerializeField] private CombineMode combineMode = CombineMode.Exact;
    
    private enum PlaceState
    {
        None,
        Placing
    }

    [Header("Build Level (future grade roll)")]
    [SerializeField] private int buildLevel = 1;

    [Header("Build Level Costs")]
    [SerializeField] private int baseUpgradeCost = 50;
    [SerializeField] private int upgradeCostStep = 50;
    [SerializeField] private int maxBuildLevel = 10;

    [Header("GhostTower")]
    [SerializeField] private GameObject ghostPreviewPrefab;
    [SerializeField] private float previewAlpha = 0.35f;
    [SerializeField] private float previewYOffset = 0.02f;

    private GameObject _ghostGO;
    private readonly System.Collections.Generic.List<Renderer> _ghostRenderers = new();
    private MaterialPropertyBlock _mpb;
    
    private PlaceState _placeState = PlaceState.None;
    
    private TowerBase _selectedTower;
    private bool _combineBusy;
    
    
    public int BuildLevel => buildLevel;

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
        
        _mpb = new MaterialPropertyBlock();
    }

    private void Update()
    {
        if (_combineBusy) 
            return;
        
        if (Mouse.current == null) 
            return;
        
        if (_placeState == PlaceState.Placing)
        {
            UpdatePlacementPreview();
        }

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
        
        TowerGrade gradeToRoll = GetBuildRollGrade();
        TowerData rolledData = RollBuildTowerData(gradeToRoll);
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
        AssignTraitIfNeeded(tower);
        
        tile.SetTower(tower);
        tower.SetTile(tile);
        GameManager.Instance.AddGold(-cost);

        Debug.Log($"[Build] Rolled: {rolledData.towerId} ({rolledData.grade}), cost={cost}");
    }
    
    private TowerData RollBuildTowerData(TowerGrade grade)
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
            if (d != null && d.grade == grade)
                return d;

            safety++;
        }

        Debug.LogWarning($"[TowerManager] No TowerData matched grade={grade}. Check buildPool contents.");
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
    
    private void AssignTraitIfNeeded(TowerBase tower)
    {
        if (tower == null) return;

        TowerData d = tower.GetData();
        if (d == null) return;

        if (d.grade == TowerGrade.Normal)
        {
            tower.SetTrait(null);
            return;
        }

        if (traitDatabase == null)
        {
            Debug.LogWarning("[TowerManager] traitDatabase is null.");
            tower.SetTrait(null);
            return;
        }

        TowerTraitSO rolled = traitDatabase.RollTrait(d.towerId, d.grade);
        tower.SetTrait(rolled);

        if (rolled != null)
            Debug.Log($"[Trait] {d.towerId}({d.grade}) => {rolled.type} {rolled.tier}");
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
                    AssignTraitIfNeeded(_selectedTower);
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
    
    private int GetMinBuildCostForGrade(TowerGrade grade)
    {
        if (buildPool == null) 
            return int.MaxValue;

        int min = int.MaxValue;
        for (int i = 0; i < buildPool.Length; i++)
        {
            var d = buildPool[i];
            if (d == null) continue;
            if (d.grade != grade) continue;

            if (d.buildCost < min) min = d.buildCost;
        }

        return min;
    }


    private string GetNextTowerId(string currentId, TowerGrade nextGrade)
    {
        int idx = currentId.LastIndexOf('_');
        if (idx < 0)
            return currentId;

        string baseId = currentId.Substring(0, idx);
        return $"{baseId}_{nextGrade.ToString().ToLower()}";
    }
    
    private TowerGrade GetBuildRollGrade()
    {
        return buildRollGrade;
    }
    
    private bool TryPlaceTower_ReturnSuccess(GridTile tile)
    {
        if (!tile.IsEmpty)
        {
            Debug.Log("설치 불가 타일이거나 이미 타워가 있습니다.");
            return false;
        }

        if (GameManager.Instance == null)
        {
            Debug.LogError("GameManager 인스턴스가 없습니다.");
            return false;
        }

        TowerGrade gradeToRoll = GetBuildRollGrade();
        TowerData rolledData = RollBuildTowerData(gradeToRoll);
        if (rolledData == null) return false;

        if (rolledData.towerPrefab == null)
        {
            Debug.LogError($"[TowerManager] towerPrefab is null in TowerData: {rolledData.name}");
            return false;
        }

        int cost = rolledData.buildCost;

        if (GameManager.Instance.Gold < cost)
        {
            Debug.Log("골드가 부족합니다.");
            return false;
        }

        Vector3 spawnPos = tile.transform.position;
        GameObject towerObj = Instantiate(rolledData.towerPrefab, spawnPos, Quaternion.identity);

        TowerBase tower = towerObj.GetComponent<TowerBase>();
        if (tower == null)
        {
            Debug.LogError("rolledData.towerPrefab에 TowerBase 컴포넌트가 없습니다.");
            Destroy(towerObj);
            return false;
        }

        tower.SetData(rolledData);
        AssignTraitIfNeeded(tower);

        tile.SetTower(tower);
        tower.SetTile(tile);

        GameManager.Instance.AddGold(-cost);

        Debug.Log($"[Build] Rolled: {rolledData.towerId} ({rolledData.grade}), cost={cost}");
        return true;
    }

    
    public void OnTileClicked(GridTile tile)
    {
        if (_placeState != PlaceState.Placing)
            return;
        
        bool placed = TryPlaceTower_ReturnSuccess(tile);
        if (placed)
            CancelPlaceMode();
    }

    public void CancelPlaceMode()
    {
        _placeState = PlaceState.None;

        if (_ghostGO != null)
            Destroy(_ghostGO);

        _ghostGO = null;
        _ghostRenderers.Clear();

        Debug.Log("[Build] Place mode OFF");
    }
    
    public int GetBuildUpgradeCost()
    {
        return baseUpgradeCost + (buildLevel - 1) * upgradeCostStep;
    }

    public bool TryUpgradeBuildLevel()
    {
        if (buildLevel >= maxBuildLevel)
        {
            Debug.Log("[Build] already max level");
            return false;
        }

        int cost = GetBuildUpgradeCost();
        if (GameManager.Instance == null) return false;

        if (!GameManager.Instance.TrySpendGold(cost))
        {
            Debug.Log("[Build] Not enough gold for build level up");
            return false;
        }

        buildLevel++;
        Debug.Log($"[Build] Level UP => {buildLevel}");
        return true;
    }
    
    public void BeginPlaceMode()
    {
        if (_placeState == PlaceState.Placing)
            return;
        
        if (GameManager.Instance != null)
        {
            int minCost = GetMinBuildCostForGrade(GetBuildRollGrade());
            if (GameManager.Instance.Gold < minCost)
            {
                Debug.Log("[Build] 골드 부족: 빌드 모드 진입 불가");
                return;
            }
        }

        _placeState = PlaceState.Placing;
        ClearSelection();

        EnsureGhost(ghostPreviewPrefab);
        UpdateGhostVisible(false);

        Debug.Log("[Build] Place mode ON");
    }

    private void EnsureGhost(GameObject prefab)
    {
        if (_ghostGO != null)
            Destroy(_ghostGO);

        if (prefab == null)
        {
            Debug.LogWarning("[Build] ghostPreviewPrefab is null");
            return;
        }

        _ghostGO = Instantiate(prefab);
        _ghostGO.name = "[GhostPreview]";

        foreach (var col in _ghostGO.GetComponentsInChildren<Collider>(true))
            col.enabled = false;

        // Ignore Raycast
        int layer = LayerMask.NameToLayer("Ignore Raycast");
        if (layer >= 0)
        {
            _ghostGO.layer = layer;
            foreach (Transform t in _ghostGO.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = layer;
        }

        _ghostRenderers.Clear();
        _ghostGO.GetComponentsInChildren(true, _ghostRenderers);

        ApplyGhostAlpha(previewAlpha);
    }

    private void UpdateGhostVisible(bool visible)
    {
        if (_ghostGO == null) return;
        _ghostGO.SetActive(visible);
    }

    private void ApplyGhostAlpha(float a)
    {
        a = Mathf.Clamp01(a);

        for (int i = 0; i < _ghostRenderers.Count; i++)
        {
            var r = _ghostRenderers[i];
            if (r == null) continue;

            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            r.GetPropertyBlock(_mpb);

            if (r.sharedMaterial != null)
            {
                if (r.sharedMaterial.HasProperty("_BaseColor"))
                {
                    Color c = r.sharedMaterial.GetColor("_BaseColor");
                    c.a = a;
                    _mpb.SetColor("_BaseColor", c);
                }
                if (r.sharedMaterial.HasProperty("_Color"))
                {
                    Color c = r.sharedMaterial.GetColor("_Color");
                    c.a = a;
                    _mpb.SetColor("_Color", c);
                }
            }

            r.SetPropertyBlock(_mpb);
        }
    }

    private void UpdatePlacementPreview()
    {
        if (_ghostGO == null)
            return;

        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = mainCamera.ScreenPointToRay(mousePos);

        if (!Physics.Raycast(ray, out RaycastHit hitTile, 1000f, tileLayerMask))
        {
            UpdateGhostVisible(false);
            return;
        }

        GridTile tile = hitTile.collider.GetComponent<GridTile>();
        if (tile == null)
        {
            UpdateGhostVisible(false);
            return;
        }
        
        bool canPlace = tile.IsEmpty && tile.TileType == TileType.Buildable;

        if (!canPlace)
        {
            UpdateGhostVisible(false);
            return;
        }

        Vector3 pos = tile.transform.position;
        pos.y += previewYOffset;

        _ghostGO.transform.position = pos;
        UpdateGhostVisible(true);
    }

}
