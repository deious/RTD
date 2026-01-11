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
        int stack = 0;
        _stackByTarget.TryGetValue(id, out stack);

        stack = Mathf.Min(stack + 1, maxStack);
        _stackByTarget[id] = stack;

        Vector3 pos = target.position + Vector3.up * (baseHeight + (stack - 1) * stackStep);

        DamageText t = _pool.Count > 0 ? _pool.Dequeue() : Instantiate(prefab, transform);
        t.transform.SetParent(null, true);
        t.transform.rotation = Quaternion.identity;
        t.transform.localScale = Vector3.one;
        
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
