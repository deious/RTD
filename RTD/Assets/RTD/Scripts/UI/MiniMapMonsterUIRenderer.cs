using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class MiniMapMonsterUIRenderer : MonoBehaviour
{
    [Header("World Bounds Source")]
    [SerializeField] private MiniMapPathRenderer pathRenderer;

    [Header("UI Target")]
    [SerializeField] private RectTransform drawArea;

    [Header("Blip Style")]
    [SerializeField] private float blipSize = 6f;
    [SerializeField] private Color normalColor = new Color(1f, 0.25f, 0.25f, 1f);
    [SerializeField] private Color bossColor   = new Color(1f, 0.85f, 0.25f, 1f);
    [SerializeField] private Sprite blipSprite;

    [Header("Options")]
    [SerializeField] private bool clampToRect = true;
    [SerializeField] private bool pixelSnap = true;

    private readonly List<Transform> _monsters = new List<Transform>(128);
    private readonly List<Image> _pool = new List<Image>(128);

    private Bounds _worldBounds;

    private void Awake()
    {
        if (!drawArea) drawArea = GetComponent<RectTransform>();
    }

    private void LateUpdate()
    {
        if (!pathRenderer || !drawArea)
        {
            HideAll();
            return;
        }

        _worldBounds = pathRenderer.CurrentWorldBounds;
        if (!IsValidBounds(_worldBounds))
        {
            HideAll();
            return;
        }
        
        for (int i = _monsters.Count - 1; i >= 0; i--)
            if (!_monsters[i]) _monsters.RemoveAt(i);

        EnsurePool(_monsters.Count);

        float w = drawArea.rect.width;
        float h = drawArea.rect.height;

        float minX = -w * drawArea.pivot.x;
        float maxX =  w * (1f - drawArea.pivot.x);
        float minY = -h * drawArea.pivot.y;
        float maxY =  h * (1f - drawArea.pivot.y);

        for (int i = 0; i < _pool.Count; i++)
        {
            bool active = i < _monsters.Count;
            _pool[i].gameObject.SetActive(active);
            if (!active) continue;

            Transform m = _monsters[i];
            Vector3 wp = m.position;

            Vector2 p = WorldToUI(wp, w, h);

            if (clampToRect)
            {
                p.x = Mathf.Clamp(p.x, minX, maxX);
                p.y = Mathf.Clamp(p.y, minY, maxY);
            }

            if (pixelSnap)
            {
                p.x = Mathf.Round(p.x);
                p.y = Mathf.Round(p.y);
            }

            RectTransform rt = (RectTransform)_pool[i].transform;
            rt.anchoredPosition = p;

            var ai = m.GetComponent<MonsterAI>();
            _pool[i].color = (ai != null && ai.IsBoss) ? bossColor : normalColor;
        }
    }

    public void Register(Transform monsterRoot)
    {
        if (!monsterRoot) return;
        if (_monsters.Contains(monsterRoot)) return;
        _monsters.Add(monsterRoot);
    }

    public void Unregister(Transform monsterRoot)
    {
        if (!monsterRoot) return;
        _monsters.Remove(monsterRoot);
    }

    private Vector2 WorldToUI(Vector3 worldPos, float uiW, float uiH)
    {
        float nx = Mathf.InverseLerp(_worldBounds.min.x, _worldBounds.max.x, worldPos.x);
        float nz = Mathf.InverseLerp(_worldBounds.min.z, _worldBounds.max.z, worldPos.z);

        float px = nx * uiW - uiW * drawArea.pivot.x;
        float py = nz * uiH - uiH * drawArea.pivot.y;

        return new Vector2(px, py);
    }

    private void EnsurePool(int count)
    {
        while (_pool.Count < count)
            _pool.Add(CreateBlip());
        
        for (int i = 0; i < count; i++)
        {
            RectTransform rt = (RectTransform)_pool[i].transform;
            rt.sizeDelta = new Vector2(blipSize, blipSize);
        }
    }

    private Image CreateBlip()
    {
        GameObject go = new GameObject("MonsterBlip", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(drawArea, false);

        RectTransform rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localRotation = Quaternion.identity;
        rt.sizeDelta = new Vector2(blipSize, blipSize);

        Image img = go.GetComponent<Image>();
        img.raycastTarget = false;
        img.sprite = blipSprite;
        img.type = Image.Type.Simple;
        img.color = normalColor;

        go.SetActive(false);
        return img;
    }

    private void HideAll()
    {
        for (int i = 0; i < _pool.Count; i++)
            if (_pool[i]) _pool[i].gameObject.SetActive(false);
    }

    private static bool IsValidBounds(Bounds b)
    {
        return b.size.x > 0.001f && b.size.z > 0.001f;
    }
}
