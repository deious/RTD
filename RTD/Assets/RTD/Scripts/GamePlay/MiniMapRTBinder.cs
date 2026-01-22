using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class MiniMapRTBinder : MonoBehaviour
{
    [Header("Cameras (P1~P4 order)")]
    [SerializeField] private List<Camera> miniMapCameras = new List<Camera>(4);

    [Header("RenderTextures (P1~P4 order) - ASSIGN ASSETS")]
    [SerializeField] private List<RenderTexture> renderTextures = new List<RenderTexture>(4);

    [Header("RawImages")]
    [SerializeField] private RawImage soloRaw;
    [SerializeField] private List<RawImage> slotRaws = new List<RawImage>(4);

    [Header("Safety")]
    [Tooltip("항상 카메라 targetTexture를 에셋 RT로 고정합니다.")]
    [SerializeField] private bool forceCameraTargets = true;

    private Texture _soloOriginal;
    private readonly Texture[] _slotOriginal = new Texture[4];
    private readonly RenderTexture[] _cameraOriginalRT = new RenderTexture[4];

    private void Awake()
    {
        CacheOriginals();
        ApplyCameraTargets();
    }

    private void OnEnable()
    {
        CacheOriginals();
        ApplyCameraTargets();
    }

    private void OnDisable()
    {
        RestoreOriginals();
    }

    private void OnDestroy()
    {
        RestoreOriginals();
    }

    private void CacheOriginals()
    {
        if (soloRaw && _soloOriginal == null)
            _soloOriginal = soloRaw.texture;

        for (int i = 0; i < _slotOriginal.Length; i++)
        {
            if (i < slotRaws.Count && slotRaws[i] && _slotOriginal[i] == null)
                _slotOriginal[i] = slotRaws[i].texture;
        }
        
        for (int i = 0; i < 4; i++)
        {
            if (i >= miniMapCameras.Count) break;
            var cam = miniMapCameras[i];
            if (!cam) continue;

            if (_cameraOriginalRT[i] == null)
                _cameraOriginalRT[i] = cam.targetTexture;
        }
    }

    private void RestoreOriginals()
    {
        if (soloRaw) soloRaw.texture = _soloOriginal;

        for (int i = 0; i < _slotOriginal.Length; i++)
            if (i < slotRaws.Count && slotRaws[i])
                slotRaws[i].texture = _slotOriginal[i];
        
        for (int i = 0; i < 4; i++)
        {
            if (i >= miniMapCameras.Count) break;
            var cam = miniMapCameras[i];
            if (!cam) continue;

            cam.targetTexture = _cameraOriginalRT[i];
        }
    }
    
    public void ApplyCameraTargets()
    {
        if (!forceCameraTargets) return;

        int count = Mathf.Min(miniMapCameras.Count, renderTextures.Count, 4);
        for (int i = 0; i < count; i++)
        {
            var cam = miniMapCameras[i];
            var rt = renderTextures[i];
            if (!cam || !rt) continue;

            cam.targetTexture = rt;
            cam.enabled = true;
        }
    }
    
    public void Bind(int playerCount)
    {
        playerCount = Mathf.Clamp(playerCount, 1, 4);
        ApplyCameraTargets();

        if (playerCount == 1)
        {
            if (soloRaw) soloRaw.texture = GetRT(0);
            
            for (int i = 0; i < slotRaws.Count; i++)
                if (slotRaws[i]) slotRaws[i].texture = null;

            return;
        }
        
        if (soloRaw) soloRaw.texture = null;

        for (int i = 0; i < 4; i++)
        {
            if (i < playerCount)
            {
                if (i < slotRaws.Count && slotRaws[i])
                    slotRaws[i].texture = GetRT(i);
            }
            else
            {
                if (i < slotRaws.Count && slotRaws[i])
                    slotRaws[i].texture = null;
            }
        }
    }

    private RenderTexture GetRT(int index)
    {
        if (index < 0 || index >= renderTextures.Count) return null;
        return renderTextures[index];
    }
}
