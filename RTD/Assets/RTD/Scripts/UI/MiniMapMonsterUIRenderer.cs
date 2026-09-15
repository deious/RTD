using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class MiniMapMonsterUIRenderer : MonoBehaviour
{
    [Header("Lane Id (0..3) - IMPORTANT")]
    [SerializeField] private int laneId = 0;

    [Header("World Bounds Source")]
    [SerializeField] private MiniMapPathRenderer pathRenderer;

    [Header("UI Target")]
    [SerializeField] private RectTransform drawArea;

    [Header("Blip Style")]
    [SerializeField] private float blipSize = 6f;
    [SerializeField] private Color normalColor = new Color(1f, 0.25f, 0.25f, 1f);
    [SerializeField] private Color bossColor = new Color(1f, 0.85f, 0.25f, 1f);
    [SerializeField] private Sprite blipSprite;

    [Header("Options")]
    [SerializeField] private bool clampToRect = true;
    [SerializeField] private bool pixelSnap = true;

    private readonly List<Transform> _monsters = new List<Transform>(128);
    private readonly List<Image> _pool = new List<Image>(128);

    private static readonly Dictionary<int, MiniMapMonsterUIRenderer> _byLane = new();

    public int LaneId => Mathf.Clamp(laneId, 0, 3);

    private void Awake()
    {
        if (!drawArea) drawArea = GetComponent<RectTransform>();
    }

    private void OnEnable()
    {
        _byLane[LaneId] = this;
    }

    private void OnDisable()
    {
        if (_byLane.TryGetValue(LaneId, out var r) && r == this)
            _byLane.Remove(LaneId);
    }

    public static bool TryGetByLane(int laneId, out MiniMapMonsterUIRenderer r)
        => _byLane.TryGetValue(Mathf.Clamp(laneId, 0, 3), out r);

    private void LateUpdate()
    {
        if (!pathRenderer || !drawArea || !pathRenderer.SpaceRoot)
        {
            HideAll();
            return;
        }

        Vector2 min = pathRenderer.LocalMinXZ;
        Vector2 max = pathRenderer.LocalMaxXZ;

        if (!IsValidBounds(min, max))
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
        float maxX = w * (1f - drawArea.pivot.x);
        float minY = -h * drawArea.pivot.y;
        float maxY = h * (1f - drawArea.pivot.y);

        Transform root = pathRenderer.SpaceRoot;

        for (int i = 0; i < _pool.Count; i++)
        {
            bool active = i < _monsters.Count;
            _pool[i].gameObject.SetActive(active);
            if (!active) continue;

            Transform m = _monsters[i];

            Vector3 lp = root.InverseTransformPoint(m.position);
            Vector2 localXZ = new Vector2(lp.x, lp.z);

            Vector2 p = LocalToUI(localXZ, min, max, w, h, drawArea.pivot);

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

            ((RectTransform)_pool[i].transform).anchoredPosition = p;

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

    private static Vector2 LocalToUI(Vector2 localXZ, Vector2 min, Vector2 max, float uiW, float uiH, Vector2 pivot)
    {
        float nx = Mathf.InverseLerp(min.x, max.x, localXZ.x);
        float nz = Mathf.InverseLerp(min.y, max.y, localXZ.y);

        float px = nx * uiW - uiW * pivot.x;
        float py = nz * uiH - uiH * pivot.y;

        return new Vector2(px, py);
    }

    private void EnsurePool(int count)
    {
        while (_pool.Count < count)
            _pool.Add(CreateBlip());

        for (int i = 0; i < count; i++)
            ((RectTransform)_pool[i].transform).sizeDelta = new Vector2(blipSize, blipSize);
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

    private static bool IsValidBounds(Vector2 min, Vector2 max)
    {
        Vector2 size = max - min;
        return size.x > 0.001f && size.y > 0.001f;
    }
    
    public void SetPathRenderer(MiniMapPathRenderer pr)
    {
        pathRenderer = pr;
    }
    
    public void ClearAllRegisteredMonsters(bool hideBlips = true)
    {
        _monsters.Clear();
        if (hideBlips) HideAll();
    }
}


