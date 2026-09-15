using UnityEngine;

public class MiniMapSpectator : MonoBehaviour
{
    [SerializeField] private SpectatorCameraController spectator;

    public void OnMiniMapClicked(int slotIndex)
    {
        if (!spectator) return;
        spectator.Spectate(slotIndex);
    }
}