using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class MiniMapUIController : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private RectTransform miniMapRoot;
    [SerializeField] private GameObject soloView;
    [SerializeField] private GameObject gridView;

    [Header("Grid Settings (base size)")]
    [SerializeField] private GridLayoutGroup gridLayout;
    [SerializeField] private Vector2 rootSize = new Vector2(240, 240);
    [SerializeField] private int padding = 10;
    [SerializeField] private int spacing = 6;

    [Header("Grid Slots (order: 1,2,3,4)")]
    [SerializeField] private List<GameObject> slots = new List<GameObject>(4);

    [Header("2 Players Layout")]
    [Tooltip("2명일 때 상단 2칸만 사용 (Slot_1, Slot_2)")]
    [SerializeField] private bool twoPlayersUseTopRowOnly = true;

    [Header("Binder")]
    [SerializeField] private MiniMapRTBinder rtBinder;

    [Header("Startup")]
    [SerializeField, Range(1, 4)] private int initialPlayerCount = 1;

    private int _rev;
    private int _fixedSlotCount = 1;
    private bool _startedAsMulti = false;
    
    private void Awake()
    {
        if (!miniMapRoot) 
            miniMapRoot = GetComponent<RectTransform>();
        if (!rtBinder)
            rtBinder = GetComponent<MiniMapRTBinder>();

        ApplyRootSize();
        ApplyGridLayoutFor2x2();

        /*_fixedSlotCount = Mathf.Clamp(initialPlayerCount, 1, 4);
        _startedAsMulti = (_fixedSlotCount >= 2);
        SetPlayerCount(initialPlayerCount);*/
    }
    
    private void Start()
    {
        InitPlayerCountFromNetworkAsync().Forget();
    }
    
    private void Reset()
    {
        miniMapRoot = GetComponent<RectTransform>();
    }
    
    private async UniTaskVoid InitPlayerCountFromNetworkAsync()
    {
        float end = Time.realtimeSinceStartup + 2.0f;

        int detected = 0;

        while (Time.realtimeSinceStartup < end)
        {
            MultiplayerContext.SyncFromSessionState();
            detected = Mathf.Clamp(MultiplayerContext.PlayersCount, 1, 4);
            
            if (detected >= 2)
                break;

            await UniTask.Delay(50, ignoreTimeScale: true, cancellationToken: this.GetCancellationTokenOnDestroy());
        }
        
        int initial = Mathf.Clamp(initialPlayerCount, 1, 4);

        int startCount = Mathf.Max(initial, detected);

        _fixedSlotCount = startCount;
        _startedAsMulti = (_fixedSlotCount >= 2);

        Debug.Log($"[MiniMapUI] detected PlayersCount={MultiplayerContext.PlayersCount}, initial={initialPlayerCount}, startCount={startCount}, startedAsMulti={_startedAsMulti}");
        SetPlayerCount(startCount);
    }
    
    public void SetPlayerCount(int playerCount)
    {
        playerCount = Mathf.Clamp(playerCount, 1, 4);
        _rev++;
        
        int uiCount = Mathf.Clamp(_fixedSlotCount, 1, 4);
        
        List<int> lanes = MultiplayerContext.GetActiveLaneIds();
        if (lanes == null || lanes.Count == 0)
        {
            lanes = new List<int>(uiCount);
            for (int i = 0; i < uiCount; i++)
                lanes.Add(i);
        }
        
        if (MiniMapLaneRegistry.Instance != null)
        {
            bool forceSolo = (!_startedAsMulti && uiCount == 1);
            
            MiniMapLaneRegistry.Instance.SetForceSoloMode(forceSolo);
            MiniMapLaneRegistry.Instance.SetVisibleLanesForPlayerCount(uiCount, twoPlayersUseTopRowOnly);
        }
    
        var bootstrap = FindFirstObjectByType<LaneMapBootstrap>(FindObjectsInactive.Include);
        GameObject[] spawnedMaps = null;
        Transform[] laneAnchors = null;
    
        if (bootstrap != null)
        {
            spawnedMaps = bootstrap.GetSpawnedMaps();
            laneAnchors = bootstrap.GetLaneAnchors();
            MiniMapLaneRegistry.Instance?.RebindAllAfterMapBuild(spawnedMaps, laneAnchors);
        }
    
        ApplyRootSize();
        ApplyGridLayoutFor2x2();
        
        if (!_startedAsMulti && uiCount == 1)
        {
            SetSolo(true);
            SetSlotsActive(0);
            rtBinder?.Bind(1);
            FinalizeRebindAsync(_rev).Forget();
            return;
        }

        SetSolo(false);
    
        if (twoPlayersUseTopRowOnly && uiCount == 2)
        {
            SetSlot(0, true);
            SetSlot(1, true);
            SetSlot(2, false);
            SetSlot(3, false);
        }
        else
        {
            for (int i = 0; i < 4; i++)
                SetSlot(i, i < uiCount);
        }
        
        if (MiniMapLaneRegistry.Instance != null && spawnedMaps != null)
        {
            for (int uiSlot = 0; uiSlot < uiCount; uiSlot++)
            {
                int laneId = Mathf.Clamp(lanes[uiSlot], 0, 3);
                MiniMapLaneRegistry.Instance.BindUISlotToLane(uiSlot, laneId, spawnedMaps);
            }
        }
    
        rtBinder?.Bind(uiCount);
        FinalizeRebindAsync(_rev).Forget();
    }
    
    private async UniTaskVoid FinalizeRebindAsync(int rev)
    {
        await UniTask.NextFrame();
        await UniTask.NextFrame();

        if (rev != _rev) return;

        MiniMapLaneRegistry.Instance?.RebindAllMiniMapsAfterMapBuild();
        MiniMapLaneRegistry.Instance?.RebindAllMonsterReportersAsync().Forget();
    }

    private void SetSolo(bool isSolo)
    {
        if (soloView) soloView.SetActive(isSolo);
        if (gridView) gridView.SetActive(!isSolo);
    }

    private void ApplyRootSize()
    {
        if (!miniMapRoot) return;
        
        miniMapRoot.sizeDelta = rootSize;
    }

    private void ApplyGridLayoutFor2x2()
    {
        if (!gridLayout) return;

        gridLayout.padding.left = padding;
        gridLayout.padding.right = padding;
        gridLayout.padding.top = padding;
        gridLayout.padding.bottom = padding;

        gridLayout.spacing = new Vector2(spacing, spacing);
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        gridLayout.constraintCount = 2;
        
        float cell = (rootSize.x - (padding * 2) - spacing) * 0.5f;
        
        float cellInt = Mathf.Floor(cell);

        gridLayout.cellSize = new Vector2(cellInt, cellInt);
    }

    private void SetSlotsActive(int activeCount)
    {
        for (int i = 0; i < 4; i++)
            SetSlot(i, i < activeCount);
    }

    private void SetSlot(int index, bool active)
    {
        if (index < 0 || index >= slots.Count) return;
        if (slots[index]) slots[index].SetActive(active);
    }
}
