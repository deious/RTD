using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MiniMapClickBinder : MonoBehaviour
{
    [SerializeField] private SpectatorCameraController spectatorController;

    [Header("Slot RawImages (P1~P4 order)")]
    [SerializeField] private List<RawImage> slotRaws = new List<RawImage>(4);

    private void Awake()
    {
        if (!spectatorController)
            spectatorController = FindFirstObjectByType<SpectatorCameraController>();

        if (!spectatorController)
        {
            Debug.LogError("[MiniMapClickBinder] SpectatorCameraController not found.");
            return;
        }

        for (int i = 0; i < slotRaws.Count; i++)
        {
            var raw = slotRaws[i];
            if (!raw) continue;

            var click = raw.GetComponent<MiniMapSlotClick>();
            if (!click)
                click = raw.gameObject.AddComponent<MiniMapSlotClick>();

            click.Setup(i, spectatorController);
        }
    }
}