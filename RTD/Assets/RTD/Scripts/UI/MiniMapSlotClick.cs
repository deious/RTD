using UnityEngine;
using UnityEngine.EventSystems;

public class MiniMapSlotClick : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private int slotIndex; // 0~3
    [SerializeField] private SpectatorCameraController spectator;
    [SerializeField] private bool debugLog = true;

    private void Awake()
    {
        if (!spectator)
            spectator = FindFirstObjectByType<SpectatorCameraController>();
    }

    public void Setup(int index, SpectatorCameraController spec)
    {
        slotIndex = index;
        spectator = spec;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (debugLog)
            Debug.Log($"[MiniMapSlotClick] Click slotIndex={slotIndex} spectator={(spectator ? spectator.name : "null")}");

        if (!spectator) return;

        spectator.Spectate(slotIndex);
        // ✅ RequestSyncLane는 Spectate() 안에서 호출됨
    }
}