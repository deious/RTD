using System.Collections.Generic;
using UnityEngine;

public class DamageTextManager : MonoBehaviour
{
    public static DamageTextManager Instance { get; private set; }

    [Header("Prefab")]
    [SerializeField] private DamageText prefab;
    [SerializeField] private int prewarm = 100;

    [Header("Stacking (per monster)")]
    [SerializeField] private float baseHeight = 2.0f;
    [SerializeField] private float stackStep = 0.22f;
    [SerializeField] private int maxStack = 6;

    [Header("Size (managed here)")]
    [SerializeField] private float scaleMul = 1.8f;

    [Tooltip("scaleMul이 커질수록 떠 보이는 걸 보정.")]
    [SerializeField] private float heightFixPerExtraScale = -0.25f;

    [Tooltip("추가 미세 조정(+면 위, -면 아래)")]
    [SerializeField] private float heightFix = 0f;

    private readonly Queue<DamageText> _pool = new();
    private readonly Dictionary<int, int> _stackByTarget = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        Prewarm();
    }

    private void Prewarm()
    {
        if (prefab == null) return;

        for (int i = 0; i < prewarm; i++)
        {
            var t = Instantiate(prefab, transform);
            t.gameObject.SetActive(false);
            _pool.Enqueue(t);
        }
    }

    public void Spawn(int damage, Transform target)
    {
        if (prefab == null || target == null) return;

        int id = target.GetInstanceID();
        _stackByTarget.TryGetValue(id, out int stack);

        stack = Mathf.Min(stack + 1, maxStack);
        _stackByTarget[id] = stack;

        float mul = Mathf.Max(0.01f, scaleMul);

        float extra = Mathf.Max(0f, mul - 1f);
        float autoFix = extra * heightFixPerExtraScale;

        float h = baseHeight + heightFix + autoFix + (stack - 1) * stackStep;
        Vector3 pos = target.position + Vector3.up * h;

        DamageText t = _pool.Count > 0 ? _pool.Dequeue() : Instantiate(prefab, transform);

        // ✅ 월드에 떠 있게 분리
        t.transform.SetParent(null, true);
        t.transform.rotation = Quaternion.identity;

        // ✅ 중요: 먼저 활성화해서 OnEnable에서 초기화가 끝나게 만든다
        if (!t.gameObject.activeSelf)
            t.gameObject.SetActive(true);

        // ✅ 그 다음 스케일 적용 (OnEnable에서 덮어쓰는 문제 방지)
        t.SetScaleMul(mul);

        // ✅ 마지막으로 내용/위치 세팅
        t.Play(damage, pos);

        t.SetOnRelease(() =>
        {
            if (_stackByTarget.TryGetValue(id, out int cur))
            {
                cur = Mathf.Max(0, cur - 1);
                if (cur == 0) _stackByTarget.Remove(id);
                else _stackByTarget[id] = cur;
            }
        });
    }

    public void Release(DamageText t)
    {
        if (t == null) return;

        t.gameObject.SetActive(false);
        t.transform.SetParent(transform);
        _pool.Enqueue(t);
    }
}
