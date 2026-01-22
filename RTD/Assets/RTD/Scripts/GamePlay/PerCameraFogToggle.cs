using UnityEngine;

[RequireComponent(typeof(Camera))]
public class PerCameraFogToggle : MonoBehaviour
{
    public bool enableFogForThisCamera = false;
    private bool _prevFog;

    private void OnPreRender()
    {
        _prevFog = RenderSettings.fog;
        RenderSettings.fog = enableFogForThisCamera;
    }

    private void OnPostRender()
    {
        RenderSettings.fog = _prevFog;
    }
}