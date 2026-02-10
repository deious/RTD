using System.Collections.Generic;
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

    private void Reset()
    {
        miniMapRoot = GetComponent<RectTransform>();
    }

    private void Awake()
    {
        if (!miniMapRoot) miniMapRoot = GetComponent<RectTransform>();
        if (!rtBinder) rtBinder = GetComponent<MiniMapRTBinder>();

        ApplyRootSize();
        ApplyGridLayoutFor2x2();

        SetPlayerCount(initialPlayerCount);
    }
    
    public void SetPlayerCount(int playerCount)
    {
        playerCount = Mathf.Clamp(playerCount, 1, 4);
        
        if (MiniMapLaneRegistry.Instance != null)
            MiniMapLaneRegistry.Instance.SetForceSoloMode(playerCount == 1);
        
        var bootstrap = FindFirstObjectByType<LaneMapBootstrap>(FindObjectsInactive.Include);
        if (bootstrap != null)
            MiniMapLaneRegistry.Instance.RebindAllAfterMapBuild(bootstrap.GetSpawnedMaps(), bootstrap.GetLaneAnchors());

        ApplyRootSize();
        ApplyGridLayoutFor2x2();

        if (playerCount == 1)
        {
            SetSolo(true);
            SetSlotsActive(0);
            rtBinder?.Bind(1);
            MiniMapLaneRegistry.Instance?.RebindAllMiniMapsAfterMapBuild();
            return;
        }

        SetSolo(false);

        if (twoPlayersUseTopRowOnly && playerCount == 2)
        {
            SetSlot(0, true);
            SetSlot(1, true);
            SetSlot(2, false);
            SetSlot(3, false);

            rtBinder?.Bind(2);
            return;
        }

        for (int i = 0; i < 4; i++)
            SetSlot(i, i < playerCount);

        rtBinder?.Bind(playerCount);
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
        
        float cell = (rootSize.x - (padding * 2) - spacing) / 2f;
        
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

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!miniMapRoot) miniMapRoot = GetComponent<RectTransform>();
        ApplyRootSize();
        ApplyGridLayoutFor2x2();
    }
#endif
}
