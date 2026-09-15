using UnityEngine;
using UnityEngine.InputSystem;
using Cysharp.Threading.Tasks;
using UnityEngine.EventSystems;

public class TowerManager : MonoBehaviour
{
    public static TowerManager Instance { get; private set; }

    [Header("Placement")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private LayerMask tileLayerMask;
    
    [Header("Random Build")]
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
    [SerializeField] private int baseUpgradeCost = 100;
    [SerializeField] private int upgradeCostStep = 100;
    [SerializeField] private int maxBuildLevel = 10;

    [Header("Build Costs")] 
    [SerializeField] private int buildCost = 10;

    [Header("GhostTower")]
    [SerializeField] private GameObject ghostPreviewPrefab;
    [SerializeField] private float previewAlpha = 0.35f;
    [SerializeField] private float previewYOffset = 0.02f;
    [SerializeField] private bool showInvalidGhost = true;
    [SerializeField] private Color validTint = new Color(0.2f, 1f, 0.2f, 1f);
    [SerializeField] private Color invalidTint = new Color(1f, 0.2f, 0.2f, 1f);
    
    [Header("Spawn Offset")]
    [SerializeField] private float towerSpawnYOffset = 0.1f;

    [Header("Context UI")]
    [SerializeField] private ContextUIController contextUI;
    
    [System.Serializable]
    public struct GradeChance
    {
        public TowerGrade grade;
        [Range(0, 100)] public int percent;
    }
    
    private GameObject _ghostGO;
    private readonly System.Collections.Generic.List<Renderer> _ghostRenderers = new();
    private MaterialPropertyBlock _mpb;
    
    private PlaceState _placeState = PlaceState.None;
    
    private TowerBase _selectedTower;
    private bool _combineBusy;
    
    public int BuildLevel => buildLevel;
    public bool IsPlacing => _placeState == PlaceState.Placing;
    public bool IsBuildLevelMax => buildLevel >= maxBuildLevel;
    public int MaxBuildLevel => maxBuildLevel;
    public event System.Action<bool> OnPlacingChanged;
    public int BuildCost => buildCost;

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
        if (UIState.BlockWorldInput) 
            return;
        
        if (_combineBusy) 
            return;
        
        if (_placeState == PlaceState.Placing)
        {
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                CancelPlaceMode();
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.wasPressedThisFrame)
            {
                CancelPlaceMode();
                return;
            }
        }
        
        if (Mouse.current == null) 
            return;
        
        if (_placeState == PlaceState.Placing)
        {
            UpdatePlacementPreview();
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            HandleClick();
        }
    }

    private void HandleClick()
    {
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = mainCamera.ScreenPointToRay(mousePos);

        if (_placeState != PlaceState.Placing)
        {
            if (Physics.Raycast(ray, out RaycastHit hitTower, 1000f, LayerMask.GetMask("Tower")))
            {
                TowerBase tower = hitTower.collider.GetComponentInParent<TowerBase>();
                if (tower != null)
                {
                    SelectTower(tower);
                    return;
                }
            }
        }
        
        if (Physics.Raycast(ray, out RaycastHit hitTile, 1000f, tileLayerMask))
        {
            GridTile tile = hitTile.collider.GetComponent<GridTile>();
            if (tile != null)
            {
                if (_placeState == PlaceState.Placing)
                {
                    OnTileClicked(tile);
                }
                else
                {
                    ClearSelection();
                }
                
                return;
            }
        }

        ClearSelection();
    }
    
    private void SelectTower(TowerBase tower)
    {
        if (_selectedTower == tower) return;

        if (_selectedTower != null)
            _selectedTower.SetSelected(false);

        _selectedTower = tower;
        _selectedTower.SetSelected(true);

        if (contextUI != null)
            contextUI.ShowTower(_selectedTower);
    }

    private void ClearSelection()
    {
        if (_selectedTower != null)
            _selectedTower.SetSelected(false);

        _selectedTower = null;

        if (contextUI == null) return;

        if (_placeState == PlaceState.Placing)
        {
            contextUI.ShowBuild(GetMinBuildCostForCurrentLevel(), buildLevel);
        }
        else
        {
            contextUI.Hide();
        }
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
                    if (resultData == null || _selectedTower == null)
                        return;
                    
                    TowerTraitSO baseTrait = _selectedTower.RuntimeTrait;
                    GridTile tile = _selectedTower.CurrentTile;

                    Vector3 spawnPos = tile != null ? tile.transform.position : mergePoint;
                    Quaternion rot = _selectedTower.transform.rotation;
                    
                    TowerTraitSO resultTrait = null;
                    if (resultData.grade != TowerGrade.Normal && traitDatabase != null)
                    {
                        if (combineMode == CombineMode.Exact)
                        {
                            if (baseTrait != null)
                                resultTrait = traitDatabase.UpgradeTrait(baseTrait, resultData.grade);
                            else
                                resultTrait = traitDatabase.RollTrait(resultData.towerId, resultData.grade);
                        }
                        else
                        {
                            resultTrait = traitDatabase.RollTrait(resultData.towerId, resultData.grade);
                        }
                    }
                    
                    int ownerLane = MultiplayerContext.MyLaneId;

                    for (int i = 0; i < mergeList.Count; i++)
                    {
                        var t = mergeList[i];
                        if (t != null && t.CurrentTile != null && TowerCombatBridge.Instance != null && ShouldSendTowerNetEvent())
                        {
                            Vector2Int gp = t.CurrentTile.GridPos;
                            int towerId = MakeTowerId(gp);
                            TowerCombatBridge.Instance.SendRemove(ownerLane, towerId);
                        }
                    }
                    
                    for (int i = 0; i < mergeList.Count; i++)
                        RemoveTowerSafe(mergeList[i]);
                    
                    TowerBase newTower = SpawnTowerFromData(
                        resultData,
                        spawnPos,
                        rot,
                        tile,
                        resultTrait
                    );

                    if (newTower == null)
                        return;

                    if (newTower != null && tile != null && TowerCombatBridge.Instance != null)
                    {
                        Vector2Int gp = tile.GridPos;
                        int towerId = MakeTowerId(gp);

                        var snap = new TowerCombatBridge.TowerSnapshot
                        {
                            worldSlotId = ownerLane,
                            towerId = towerId,
                            towerTypeId = resultData.towerId,
                            gx = gp.x,
                            gy = gp.y,
                            level = 1
                        };
                        TowerCombatBridge.Instance.SendSpawnOrUpdate(snap);
                    }
                    
                    _selectedTower = newTower;
                    _selectedTower.SetSelected(true);
                    _selectedTower.PlaySpawnFeedback();

                    if (contextUI != null)
                        contextUI.ShowTower(_selectedTower);
                    
                    UniTask.NextFrame().ContinueWith(() =>
                    {
                        ClearSelection();
                    });
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
        TowerGrade rolled = RollGradeByBuildLevel();
        return ResolveGradeIfMissing(rolled);
    }
    
    private bool TryPlaceTower_ReturnSuccess(GridTile tile)
    {
        if (!tile.IsEmpty)
        {
            Debug.Log("설치 불가 타일이거나 이미 타워가 있습니다.");
            return false;
        }

        if (GameRuntime.Instance == null)
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

        int cost = buildCost;

        if (GameRuntime.Instance.Gold < cost)
        {
            Debug.Log("골드가 부족합니다.");
            return false;
        }

        Vector3 spawnPos = tile.transform.position + Vector3.up * towerSpawnYOffset;
        GameObject towerObj = Instantiate(rolledData.towerPrefab, spawnPos, Quaternion.identity);

        TowerBase tower = towerObj.GetComponent<TowerBase>();
        if (tower == null)
        {
            Debug.LogError("rolledData.towerPrefab에 TowerBase 컴포넌트가 없습니다.");
            Destroy(towerObj);
            return false;
        }
        
        var own = towerObj.GetComponent<TowerOwnership>();
        if (own == null) own = towerObj.AddComponent<TowerOwnership>();
        own.ownerLane = MultiplayerContext.MyLaneId;

        tower.SetData(rolledData);
        AssignTraitIfNeeded(tower);

        tile.SetTower(tower);
        tower.SetTile(tile);

        AudioManager.Instance?.PlayTowerBuild(spawnPos);
        GameRuntime.Instance.AddGold(-cost);

        int ownerLane = MultiplayerContext.MyLaneId;
        Vector2Int gp = tile.GridPos;
        int towerId = MakeTowerId(gp);

        if (TowerCombatBridge.Instance != null && ShouldSendTowerNetEvent())
        {
            var snap = new TowerCombatBridge.TowerSnapshot
            {
                worldSlotId = ownerLane,
                towerId = towerId,
                towerTypeId = rolledData.towerId,
                gx = gp.x,
                gy = gp.y,
                level = 1
            };
            TowerCombatBridge.Instance.SendSpawnOrUpdate(snap);
        }
        
        return true;
    }

    private int MakeTowerIdFromTile(GridTile tile)
    {
        Vector3 p = tile.transform.position;
        int gx = Mathf.RoundToInt(p.x);
        int gy = Mathf.RoundToInt(p.z);
        return gx * 1000 + gy;
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

        OnPlacingChanged?.Invoke(false);
        
        if (contextUI != null)
            contextUI.Hide();
        
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
        if (GameRuntime.Instance == null) return false;

        if (!GameRuntime.Instance.TrySpendGold(cost))
        {
            Debug.Log("[Build] Not enough gold for build level up");
            return false;
        }

        buildLevel++;
        Debug.Log($"[Build] Level UP => {buildLevel}");
        
        if (IsPlacing && contextUI != null)
            contextUI.ShowBuild(GetMinBuildCostForCurrentLevel(), buildLevel);
        
        return true;
    }
    
    public void BeginPlaceMode()
    {
        if (_placeState == PlaceState.Placing)
            return;
        
        if (GameRuntime.Instance != null)
        {
            int minCost = GetMinBuildCostForCurrentLevel();
            if (GameRuntime.Instance.Gold < minCost)
            {
                Debug.Log("[Build] 골드 부족: 빌드 모드 진입 불가");
                return;
            }
        }

        _placeState = PlaceState.Placing;
        ClearSelection();

        EnsureGhost(ghostPreviewPrefab);
        UpdateGhostVisible(false);
        
        OnPlacingChanged?.Invoke(true);
        
        if (contextUI != null)
            contextUI.ShowBuild(GetMinBuildCostForCurrentLevel(), buildLevel);

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

        //ApplyGhostAlpha(previewAlpha);
        ApplyGhostTint(true);
    }
    
    private void ApplyGhostTint(bool canPlace)
    {
        if (_ghostRenderers == null || _ghostRenderers.Count == 0) return;
        if (_mpb == null) _mpb = new MaterialPropertyBlock();

        Color tint = canPlace ? validTint : invalidTint;
        tint.a = previewAlpha;

        for (int i = 0; i < _ghostRenderers.Count; i++)
        {
            var r = _ghostRenderers[i];
            if (r == null) continue;

            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            r.GetPropertyBlock(_mpb);
            
            _mpb.SetColor("_BaseColor", tint);
            _mpb.SetColor("_Color", tint);

            r.SetPropertyBlock(_mpb);
        }
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
        
        Vector3 pos = tile.transform.position;
        pos.y += previewYOffset;
        _ghostGO.transform.position = pos;
        
        if (!canPlace && !showInvalidGhost)
        {
            UpdateGhostVisible(false);
            return;
        }

        UpdateGhostVisible(true);
        ApplyGhostTint(canPlace);
    }
    
    private int CalculateSellRefund(TowerBase tower)
    {
        if (tower == null) return 0;

        TowerData d = tower.GetData();
        if (d == null) return 0;
        
        const float rate = 0.5f;
        return Mathf.Max(0, Mathf.RoundToInt(d.buildCost * rate));
    }
    
    private GradeChance[] GetChancesForLevel(int level) 
    {
        int lv = Mathf.Clamp(level, 1, maxBuildLevel);
        
        switch (lv)
        {
            case 1:  return new[] { new GradeChance{ grade = TowerGrade.Normal, percent = 100 } };
            case 2:  return new[] { new GradeChance{ grade = TowerGrade.Normal, percent = 90 }, new GradeChance{ grade = TowerGrade.Rare, percent = 10 } };
            case 3:  return new[] { new GradeChance{ grade = TowerGrade.Normal, percent = 75 }, new GradeChance{ grade = TowerGrade.Rare, percent = 22 }, new GradeChance{ grade = TowerGrade.Epic, percent = 3 } };
            case 4:  return new[] { new GradeChance{ grade = TowerGrade.Normal, percent = 60 }, new GradeChance{ grade = TowerGrade.Rare, percent = 33 }, new GradeChance{ grade = TowerGrade.Epic, percent = 7 } };
            case 5:  return new[] { new GradeChance{ grade = TowerGrade.Normal, percent = 50 }, new GradeChance{ grade = TowerGrade.Rare, percent = 35 }, new GradeChance{ grade = TowerGrade.Epic, percent = 13 }, new GradeChance{ grade = TowerGrade.Legendary, percent = 2 } };
            case 6:  return new[] { new GradeChance{ grade = TowerGrade.Normal, percent = 40 }, new GradeChance{ grade = TowerGrade.Rare, percent = 35 }, new GradeChance{ grade = TowerGrade.Epic, percent = 20 }, new GradeChance{ grade = TowerGrade.Legendary, percent = 5 } };
            case 7:  return new[] { new GradeChance{ grade = TowerGrade.Normal, percent = 30 }, new GradeChance{ grade = TowerGrade.Rare, percent = 35 }, new GradeChance{ grade = TowerGrade.Epic, percent = 25 }, new GradeChance{ grade = TowerGrade.Legendary, percent = 10 } };
            case 8:  return new[] { new GradeChance{ grade = TowerGrade.Normal, percent = 20 }, new GradeChance{ grade = TowerGrade.Rare, percent = 35 }, new GradeChance{ grade = TowerGrade.Epic, percent = 30 }, new GradeChance{ grade = TowerGrade.Legendary, percent = 15 } };
            case 9:  return new[] { new GradeChance{ grade = TowerGrade.Normal, percent = 10 }, new GradeChance{ grade = TowerGrade.Rare, percent = 35 }, new GradeChance{ grade = TowerGrade.Epic, percent = 35 }, new GradeChance{ grade = TowerGrade.Legendary, percent = 20 } };
            default: return new[] { new GradeChance{ grade = TowerGrade.Normal, percent = 5 },  new GradeChance{ grade = TowerGrade.Rare, percent = 30 }, new GradeChance{ grade = TowerGrade.Epic, percent = 40 }, new GradeChance{ grade = TowerGrade.Legendary, percent = 25 } };
        }
    }
    
    private TowerGrade RollGradeByBuildLevel()
    {
        GradeChance[] chances = GetChancesForLevel(buildLevel);

        int roll = Random.Range(0, 100);
        int acc = 0;

        for (int i = 0; i < chances.Length; i++)
        {
            acc += chances[i].percent;
            if (roll < acc)
                return chances[i].grade;
        }
        
        return chances[chances.Length - 1].grade;
    }
    
    private bool HasAnyTowerOfGrade(TowerGrade grade)
    {
        if (buildPool == null) return false;

        for (int i = 0; i < buildPool.Length; i++)
        {
            var d = buildPool[i];
            if (d != null && d.grade == grade)
                return true;
        }
        return false;
    }

    private TowerGrade ResolveGradeIfMissing(TowerGrade rolled)
    {
        TowerGrade g = rolled;

        while (!HasAnyTowerOfGrade(g))
        {
            if (g == TowerGrade.Normal)
                return TowerGrade.Normal;

            g = (TowerGrade)((int)g - 1); // Legendary->Epic->Rare->Normal
        }

        return g;
    }
    
    private int MakeTowerId(Vector2Int gp)
    {
        unchecked
        {
            return (gp.x << 16) ^ (gp.y & 0xFFFF);
        }
    }
    
    public bool TrySellTower(TowerBase tower)
    {
        if (tower == null)
            return false;
        
        GridTile tile = tower.CurrentTile;

        if (_selectedTower == tower)
        {
            _selectedTower.SetSelected(false);
            _selectedTower = null;
        }

        int refund = CalculateSellRefund(tower);
        AudioManager.Instance?.PlayTowerSell(tower.transform.position);
        
        if (tile != null && TowerCombatBridge.Instance != null && ShouldSendTowerNetEvent())
        {
            int ownerLane = MultiplayerContext.MyLaneId;
            Vector2Int gp = tile.GridPos;
            int towerId = MakeTowerId(gp);

            TowerCombatBridge.Instance.SendRemove(ownerLane, towerId);
        }
        
        tower.SetTile(null);

        if (GameRuntime.Instance != null && refund > 0)
            GameRuntime.Instance.AddGold(refund);

        Destroy(tower.gameObject);
        return true;
    }
    
    private TowerBase SpawnTowerFromData(
        TowerData data,
        Vector3 position,
        Quaternion rotation,
        GridTile tile,
        TowerTraitSO trait)
    {
        if (data == null || data.towerPrefab == null)
        {
            Debug.LogError("[TowerManager] SpawnTowerFromData failed");
            return null;
        }

        GameObject go = Instantiate(data.towerPrefab, position, rotation);
        TowerBase tower = go.GetComponent<TowerBase>();

        if (tower == null)
        {
            Debug.LogError("[TowerManager] Prefab has no TowerBase");
            Destroy(go);
            return null;
        }
        
        var own = go.GetComponent<TowerOwnership>();
        if (own == null) own = go.AddComponent<TowerOwnership>();
        own.ownerLane = MultiplayerContext.MyLaneId;

        tower.SetData(data);
        tower.SetTrait(trait);

        if (tile != null)
        {
            tile.SetTower(tower);
            tower.SetTile(tile);
        }

        return tower;
    }

    private void RemoveTowerSafe(TowerBase tower)
    {
        if (tower == null) return;
        tower.SetTile(null);
        Destroy(tower.gameObject);
    }
    
    private bool ShouldSendTowerNetEvent()
    {
        if (AppFlowManager.Instance == null || !AppFlowManager.Instance.IsMultiMode)
            return false;

        if (Unity.Netcode.NetworkManager.Singleton == null ||
            !Unity.Netcode.NetworkManager.Singleton.IsListening)
            return false;

        return true;
    }

    
    public string GetBuildLevelChanceLabel()
    {
        var chances = GetChancesForLevel(buildLevel);

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"Build Lv. {buildLevel}");

        for (int i = 0; i < chances.Length; i++)
            sb.AppendLine($"{chances[i].grade}: {chances[i].percent}%");

        return sb.ToString();
    }
    
    public int GetMinBuildCostForCurrentLevel()
    {
        var chances = GetChancesForLevel(buildLevel);
        if (chances == null || chances.Length == 0) 
            return int.MaxValue;

        int min = int.MaxValue;

        for (int i = 0; i < chances.Length; i++)
        {
            if (chances[i].percent <= 0) 
                continue;

            int c = GetMinBuildCostForGrade(chances[i].grade);
            if (c < min) 
                min = c;
        }

        return min;
    }
    
    public void RequestCombineExact()
    {
        combineMode = CombineMode.Exact;
        if (_combineBusy) return;
        TryCombineSelectedTowerAsync().Forget();
    }

    public void RequestCombineRandom()
    {
        combineMode = CombineMode.Random;
        if (_combineBusy) return;
        TryCombineSelectedTowerAsync().Forget();
    }

    public bool TryRerollTrait(TowerBase tower, int cost)
    {
        if (tower == null) return false;

        TowerData d = tower.GetData();
        if (d == null) return false;

        if (d.grade == TowerGrade.Normal)
            return false;

        if (GameRuntime.Instance == null)
            return false;

        if (!GameRuntime.Instance.TrySpendGold(cost))
            return false;

        if (traitDatabase == null)
            return false;

        TowerTraitSO newTrait = traitDatabase.RollTraitExclude(d.towerId, d.grade, tower.RuntimeTrait);
        if (newTrait == null)
            return false;

        tower.SetTrait(newTrait);
        AudioManager.Instance?.PlayTraitChange(tower.transform.position);

        return true;
    }
    
    public int GetSellRefund(TowerBase tower)
    {
        return CalculateSellRefund(tower);
    }
}
