
/*using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class MiniMapPathRenderer : MonoBehaviour
{
    [Header("Source Path")]
    [SerializeField] private WaypointPath waypointPath;

    [Header("World Bounds")]
    [SerializeField] private Bounds worldBounds;
    
    [SerializeField] private Renderer boundsRenderer;

    [Header("Auto Bounds From Waypoints")]
    [SerializeField] private bool autoBoundsFromWaypoints = true;
    [SerializeField] private float waypointBoundsPadding = 2f;

    [Header("UI Target")]
    [SerializeField] private RectTransform drawArea;

    [Header("Style")]
    [SerializeField] private float lineThickness = 6f;
    [SerializeField] private Color lineColor = new Color(0.12f, 0.50f, 1.0f, 1.0f);
    [SerializeField] private Sprite lineSprite;

    [Header("Overlap Fix")]
    [Tooltip("코너/교차점에서 겹침을 줄여 두께가 일정하게 보이도록, 선분 끝을 일부 잘라냅니다.")]
    [SerializeField] private bool trimEndsToAvoidOverlap = true;

    [Header("Corner Caps")]
    [Tooltip("선분 끝(방향이 바뀌는 지점/끝점)에 캡(정사각형)을 추가하여 끊김을 메웁니다.")]
    [SerializeField] private bool addCornerCaps = true;

    [Tooltip("캡 크기 = lineThickness * capSizeMultiplier")]
    [SerializeField] private float capSizeMultiplier = 1.0f;

    [Header("Rebuild")]
    [SerializeField] private bool rebuildOnEnable = true;

    private readonly List<GameObject> _spawned = new List<GameObject>();
    public Bounds CurrentWorldBounds => worldBounds;

    private void Awake()
    {
        if (!drawArea) drawArea = GetComponent<RectTransform>();
    }

    private void OnEnable()
    {
        if (rebuildOnEnable)
            Rebuild();
    }

    public void Rebuild()
    {
        ClearSpawned();

        if (!waypointPath || waypointPath.Count < 2)
            return;

        if (!drawArea)
        {
            Debug.LogError("[MiniMapPathRenderer] drawArea가 비었습니다.", this);
            return;
        }

        ResolveWorldBounds();
        if (!IsValidBounds(worldBounds))
        {
            Debug.LogError("[MiniMapPathRenderer] worldBounds가 유효하지 않습니다. boundsRenderer/worldBounds/autoBoundsFromWaypoints를 확인하세요.", this);
            return;
        }

        float w = drawArea.rect.width;
        float h = drawArea.rect.height;

        Vector2 prev = WorldToUI(waypointPath.Get(0).position, w, h);
        
        bool hasRun = false;
        bool runHorizontal = true;
        Vector2 runStart = prev;
        Vector2 runEnd = prev;
        
        if (addCornerCaps)
            CreateCap(prev);

        for (int i = 1; i < waypointPath.Count; i++)
        {
            Transform t = waypointPath.Get(i);
            if (!t) continue;

            Vector2 cur = WorldToUI(t.position, w, h);
            Vector2 snapped = SnapAxis(prev, cur);

            bool stepHorizontal = Mathf.Abs(snapped.x - prev.x) >= Mathf.Abs(snapped.y - prev.y);

            if (!hasRun)
            {
                hasRun = true;
                runHorizontal = stepHorizontal;
                runStart = prev;
                runEnd = snapped;
            }
            else
            {
                if (stepHorizontal == runHorizontal)
                {
                    runEnd = snapped;
                }
                else
                {
                    CreateSegment(runStart, runEnd);
                    
                    if (addCornerCaps)
                        CreateCap(runEnd);

                    runHorizontal = stepHorizontal;
                    runStart = runEnd;
                    runEnd = snapped;
                }
            }

            prev = snapped;
        }
        
        if (hasRun)
        {
            CreateSegment(runStart, runEnd);

            if (addCornerCaps)
                CreateCap(runEnd);
        }
    }

    private void ResolveWorldBounds()
    {
        if (boundsRenderer)
        {
            worldBounds = boundsRenderer.bounds;
            return;
        }

        if (IsValidBounds(worldBounds))
            return;

        if (autoBoundsFromWaypoints)
            worldBounds = CalcBoundsFromWaypoints(waypointPath, waypointBoundsPadding);
    }

    private static bool IsValidBounds(Bounds b)
    {
        return b.size.x > 0.001f && b.size.z > 0.001f;
    }

    private static Bounds CalcBoundsFromWaypoints(WaypointPath path, float padding)
    {
        Vector3 min = new Vector3(float.PositiveInfinity, 0f, float.PositiveInfinity);
        Vector3 max = new Vector3(float.NegativeInfinity, 0f, float.NegativeInfinity);

        for (int i = 0; i < path.Count; i++)
        {
            var t = path.Get(i);
            if (!t) continue;

            Vector3 p = t.position;
            min.x = Mathf.Min(min.x, p.x);
            min.z = Mathf.Min(min.z, p.z);
            max.x = Mathf.Max(max.x, p.x);
            max.z = Mathf.Max(max.z, p.z);
        }

        if (float.IsInfinity(min.x) || float.IsInfinity(min.z))
            return new Bounds();

        Vector3 center = new Vector3((min.x + max.x) * 0.5f, 0f, (min.z + max.z) * 0.5f);
        Vector3 size = new Vector3(
            Mathf.Max(1f, (max.x - min.x) + padding * 2f),
            0f,
            Mathf.Max(1f, (max.z - min.z) + padding * 2f)
        );

        return new Bounds(center, size);
    }

    private Vector2 WorldToUI(Vector3 worldPos, float uiW, float uiH)
    {
        float nx = Mathf.InverseLerp(worldBounds.min.x, worldBounds.max.x, worldPos.x);
        float nz = Mathf.InverseLerp(worldBounds.min.z, worldBounds.max.z, worldPos.z);

        float px = nx * uiW - uiW * drawArea.pivot.x;
        float py = nz * uiH - uiH * drawArea.pivot.y;

        return new Vector2(Mathf.Round(px), Mathf.Round(py));
    }

    private Vector2 SnapAxis(Vector2 a, Vector2 b)
    {
        float dx = b.x - a.x;
        float dy = b.y - a.y;

        if (Mathf.Abs(dx) >= Mathf.Abs(dy))
            return new Vector2(b.x, a.y); // 수평
        else
            return new Vector2(a.x, b.y); // 수직
    }

    private void CreateSegment(Vector2 a, Vector2 b)
    {
        Vector2 delta = b - a;
        bool horizontal = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y);
        
        float trim = trimEndsToAvoidOverlap ? (lineThickness * 0.5f) : 0f;

        Vector2 pos;
        Vector2 size;

        if (horizontal)
        {
            float length = Mathf.Abs(delta.x);
            if (length < 0.01f) return;

            float finalLength = Mathf.Max(0f, length - trim);
            if (finalLength < 1f) return;

            float sign = Mathf.Sign(delta.x);
            float midX = (a.x + b.x) * 0.5f + sign * (trim * 0.25f);

            pos = new Vector2(midX, a.y);
            size = new Vector2(finalLength, lineThickness);
        }
        else
        {
            float length = Mathf.Abs(delta.y);
            if (length < 0.01f) return;

            float finalLength = Mathf.Max(0f, length - trim);
            if (finalLength < 1f) return;

            float sign = Mathf.Sign(delta.y);
            float midY = (a.y + b.y) * 0.5f + sign * (trim * 0.25f);

            pos = new Vector2(a.x, midY);
            size = new Vector2(lineThickness, finalLength);
        }

        pos = new Vector2(Mathf.Round(pos.x), Mathf.Round(pos.y));
        size = new Vector2(Mathf.Round(size.x), Mathf.Round(size.y));

        if (size.x < 1f || size.y < 1f)
            return;

        var go = new GameObject("PathSeg", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(drawArea, false);

        var rt = (RectTransform)go.transform;
        var img = go.GetComponent<Image>();

        img.color = lineColor;
        img.sprite = lineSprite;
        img.type = Image.Type.Simple;

        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localRotation = Quaternion.identity;

        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        _spawned.Add(go);
    }

    private void CreateCap(Vector2 at)
    {
        float size = Mathf.Max(1f, lineThickness * capSizeMultiplier);

        var go = new GameObject("PathCap", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(drawArea, false);

        var rt = (RectTransform)go.transform;
        var img = go.GetComponent<Image>();

        img.color = lineColor;
        img.sprite = lineSprite;
        img.type = Image.Type.Simple;

        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localRotation = Quaternion.identity;

        rt.anchoredPosition = new Vector2(Mathf.Round(at.x), Mathf.Round(at.y));
        rt.sizeDelta = new Vector2(Mathf.Round(size), Mathf.Round(size));

        _spawned.Add(go);
    }

    private void ClearSpawned()
    {
        for (int i = 0; i < _spawned.Count; i++)
        {
            if (_spawned[i])
                Destroy(_spawned[i]);
        }
        _spawned.Clear();
    }
}*/


using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class MiniMapPathRenderer : MonoBehaviour
{
    [Header("Source Path (runtime assigned)")]
    [SerializeField] private WaypointPath waypointPath;

    [Header("Space Root (IMPORTANT)")]
    [Tooltip("이 미니맵이 기준으로 삼을 좌표계(해당 Lane의 MapRoot 권장)")]
    [SerializeField] private Transform spaceRoot;

    [Header("Local Bounds (x,z in spaceRoot)")]
    [SerializeField] private Vector2 localMinXZ;
    [SerializeField] private Vector2 localMaxXZ;

    [Header("Auto Bounds From Waypoints")]
    [SerializeField] private bool autoBoundsFromWaypoints = true;
    [SerializeField] private float padding = 2f;

    [Header("UI Target")]
    [SerializeField] private RectTransform drawArea;

    [Header("Style")]
    [SerializeField] private float lineThickness = 6f;
    [SerializeField] private Color lineColor = new Color(0.12f, 0.50f, 1.0f, 1.0f);
    [SerializeField] private Sprite lineSprite;

    [Header("Overlap Fix")]
    [SerializeField] private bool trimEndsToAvoidOverlap = true;

    [Header("Corner Caps")]
    [SerializeField] private bool addCornerCaps = true;
    [SerializeField] private float capSizeMultiplier = 1.0f;

    [Header("Rebuild")]
    [SerializeField] private bool rebuildOnEnable = true;
    
    [Header("Lane Id (0..3)")]
    [SerializeField] private int laneId = 0;
    public int LaneId => Mathf.Clamp(laneId, 0, 3);

    private readonly List<GameObject> _spawned = new List<GameObject>();

    public Transform SpaceRoot => spaceRoot ? spaceRoot : (waypointPath ? waypointPath.transform : null);
    public Vector2 LocalMinXZ => localMinXZ;
    public Vector2 LocalMaxXZ => localMaxXZ;

    private void Awake()
    {
        if (!drawArea) drawArea = GetComponent<RectTransform>();
    }

    private void OnEnable()
    {
        if (rebuildOnEnable)
            Rebuild();
    }

    /// <summary>
    /// ✅ 맵이 런타임에 생성/교체될 때 반드시 호출.
    /// </summary>
    public void Bind(WaypointPath path, Transform newSpaceRoot = null, bool rebuildNow = true)
    {
        waypointPath = path;
        if (newSpaceRoot) spaceRoot = newSpaceRoot;

        if (autoBoundsFromWaypoints && waypointPath && SpaceRoot)
            AutoComputeLocalBounds();

        if (rebuildNow)
            Rebuild();
    }

    public void Rebuild()
    {
        ClearSpawned();

        if (!drawArea)
            return;

        if (!waypointPath || waypointPath.Count < 2)
            return;

        if (!SpaceRoot)
        {
            Debug.LogError("[MiniMapPathRenderer] SpaceRoot가 null 입니다. Bind()로 spaceRoot를 넣어주세요.", this);
            return;
        }

        if (autoBoundsFromWaypoints)
            AutoComputeLocalBounds();

        if (!IsValidLocalBounds())
        {
            Debug.LogError($"[MiniMapPathRenderer] local bounds invalid. min={localMinXZ}, max={localMaxXZ}", this);
            return;
        }

        float w = drawArea.rect.width;
        float h = drawArea.rect.height;
        if (w <= 1f || h <= 1f)
            return;

        Vector2 prev = LocalToUI(ToLocalXZ(waypointPath.Get(0).position), w, h);

        if (addCornerCaps)
            CreateCap(prev);

        bool hasRun = false;
        bool runHorizontal = true;
        Vector2 runStart = prev;
        Vector2 runEnd = prev;

        for (int i = 1; i < waypointPath.Count; i++)
        {
            Transform t = waypointPath.Get(i);
            if (!t) continue;

            Vector2 cur = LocalToUI(ToLocalXZ(t.position), w, h);
            Vector2 snapped = SnapAxis(prev, cur);

            bool stepHorizontal = Mathf.Abs(snapped.x - prev.x) >= Mathf.Abs(snapped.y - prev.y);

            if (!hasRun)
            {
                hasRun = true;
                runHorizontal = stepHorizontal;
                runStart = prev;
                runEnd = snapped;
            }
            else
            {
                if (stepHorizontal == runHorizontal)
                {
                    runEnd = snapped;
                }
                else
                {
                    CreateSegment(runStart, runEnd);
                    if (addCornerCaps) CreateCap(runEnd);

                    runHorizontal = stepHorizontal;
                    runStart = runEnd;
                    runEnd = snapped;
                }
            }

            prev = snapped;
        }

        if (hasRun)
        {
            CreateSegment(runStart, runEnd);
            if (addCornerCaps) CreateCap(runEnd);
        }
    }

    private Vector2 ToLocalXZ(Vector3 worldPos)
    {
        Vector3 lp = SpaceRoot.InverseTransformPoint(worldPos);
        return new Vector2(lp.x, lp.z);
    }

    public void AutoComputeLocalBounds()
    {
        if (!SpaceRoot || !waypointPath || waypointPath.Count == 0)
            return;

        float minX = float.PositiveInfinity, minZ = float.PositiveInfinity;
        float maxX = float.NegativeInfinity, maxZ = float.NegativeInfinity;

        for (int i = 0; i < waypointPath.Count; i++)
        {
            var t = waypointPath.Get(i);
            if (!t) continue;

            Vector3 lp = SpaceRoot.InverseTransformPoint(t.position);
            minX = Mathf.Min(minX, lp.x);
            minZ = Mathf.Min(minZ, lp.z);
            maxX = Mathf.Max(maxX, lp.x);
            maxZ = Mathf.Max(maxZ, lp.z);
        }

        if (float.IsInfinity(minX) || float.IsInfinity(minZ))
            return;

        localMinXZ = new Vector2(minX - padding, minZ - padding);
        localMaxXZ = new Vector2(maxX + padding, maxZ + padding);
    }

    private bool IsValidLocalBounds()
    {
        Vector2 size = localMaxXZ - localMinXZ;
        return size.x > 0.001f && size.y > 0.001f;
    }

    private Vector2 LocalToUI(Vector2 localXZ, float uiW, float uiH)
    {
        float nx = Mathf.InverseLerp(localMinXZ.x, localMaxXZ.x, localXZ.x);
        float nz = Mathf.InverseLerp(localMinXZ.y, localMaxXZ.y, localXZ.y);

        float px = nx * uiW - uiW * drawArea.pivot.x;
        float py = nz * uiH - uiH * drawArea.pivot.y;

        return new Vector2(Mathf.Round(px), Mathf.Round(py));
    }

    private Vector2 SnapAxis(Vector2 a, Vector2 b)
    {
        float dx = b.x - a.x;
        float dy = b.y - a.y;

        return (Mathf.Abs(dx) >= Mathf.Abs(dy))
            ? new Vector2(b.x, a.y)
            : new Vector2(a.x, b.y);
    }

    private void CreateSegment(Vector2 a, Vector2 b)
    {
        Vector2 delta = b - a;
        bool horizontal = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y);

        float trim = trimEndsToAvoidOverlap ? (lineThickness * 0.5f) : 0f;

        Vector2 pos;
        Vector2 size;

        if (horizontal)
        {
            float length = Mathf.Abs(delta.x);
            if (length < 0.01f) return;

            float finalLength = Mathf.Max(0f, length - trim);
            if (finalLength < 1f) return;

            float sign = Mathf.Sign(delta.x);
            float midX = (a.x + b.x) * 0.5f + sign * (trim * 0.25f);

            pos = new Vector2(midX, a.y);
            size = new Vector2(finalLength, lineThickness);
        }
        else
        {
            float length = Mathf.Abs(delta.y);
            if (length < 0.01f) return;

            float finalLength = Mathf.Max(0f, length - trim);
            if (finalLength < 1f) return;

            float sign = Mathf.Sign(delta.y);
            float midY = (a.y + b.y) * 0.5f + sign * (trim * 0.25f);

            pos = new Vector2(a.x, midY);
            size = new Vector2(lineThickness, finalLength);
        }

        pos = new Vector2(Mathf.Round(pos.x), Mathf.Round(pos.y));
        size = new Vector2(Mathf.Round(size.x), Mathf.Round(size.y));

        if (size.x < 1f || size.y < 1f)
            return;

        var go = new GameObject("PathSeg", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(drawArea, false);

        var rt = (RectTransform)go.transform;
        var img = go.GetComponent<Image>();

        img.color = lineColor;
        img.sprite = lineSprite;
        img.type = Image.Type.Simple;

        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localRotation = Quaternion.identity;

        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        _spawned.Add(go);
    }

    private void CreateCap(Vector2 at)
    {
        float size = Mathf.Max(1f, lineThickness * capSizeMultiplier);

        var go = new GameObject("PathCap", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(drawArea, false);

        var rt = (RectTransform)go.transform;
        var img = go.GetComponent<Image>();

        img.color = lineColor;
        img.sprite = lineSprite;
        img.type = Image.Type.Simple;

        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localRotation = Quaternion.identity;

        rt.anchoredPosition = new Vector2(Mathf.Round(at.x), Mathf.Round(at.y));
        rt.sizeDelta = new Vector2(Mathf.Round(size), Mathf.Round(size));

        _spawned.Add(go);
    }

    private void ClearSpawned()
    {
        for (int i = 0; i < _spawned.Count; i++)
            if (_spawned[i]) Destroy(_spawned[i]);
        _spawned.Clear();
    }
}


