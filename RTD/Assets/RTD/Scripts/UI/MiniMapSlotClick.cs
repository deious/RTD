using UnityEngine;
using UnityEngine.EventSystems;

public class MiniMapSlotClick : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private int slotIndex; // 0~3
    [SerializeField] private SpectatorCameraController spectator;

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
        if (!spectator) return;
        spectator.Spectate(slotIndex);
    }
}