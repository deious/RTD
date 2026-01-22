using UnityEngine;

public class FogDistanceDriver : MonoBehaviour
{
    public enum Preset { Safe, Natural }

    [SerializeField] private OrbitCamera orbit;
    [SerializeField] private Preset preset = Preset.Safe;

    [Header("Fog Color")]
    [SerializeField] private Color fogColor = new Color(0.10f, 0.14f, 0.18f, 1f);

    private void LateUpdate()
    {
        if (orbit == null) return;

        float d = orbit.Distance;

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = fogColor;

        float start, end;

        if (preset == Preset.Safe)
        {
            start = 100f + 1.25f * d;
            end   = start + 700f;
        }
        else
        {
            start = 80f + 1.15f * d;
            end   = start + 550f;
        }

        RenderSettings.fogStartDistance = start;
        RenderSettings.fogEndDistance   = end;
    }
}
