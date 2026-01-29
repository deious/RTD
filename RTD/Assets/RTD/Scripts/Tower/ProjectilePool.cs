using System.Collections.Generic;
using UnityEngine;

public class ProjectilePool : MonoBehaviour
{
    [System.Serializable]
    public class PrewarmEntry
    {
        public Projectile prefab;
        public int count = 120;
    }

    public static ProjectilePool Instance { get; private set; }

    [Header("Prewarm")]
    [SerializeField] private List<PrewarmEntry> prewarmList = new List<PrewarmEntry>();

    [Header("Options")]
    [SerializeField] private bool allowRuntimeExpand = true;

    private readonly Dictionary<Projectile, Stack<Projectile>> _poolByPrefab = new Dictionary<Projectile, Stack<Projectile>>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        PrewarmAll();
    }

    private void PrewarmAll()
    {
        for (int i = 0; i < prewarmList.Count; i++)
        {
            var e = prewarmList[i];
            if (e == null || e.prefab == null)
                continue;

            int n = Mathf.Max(0, e.count);
            Prewarm(e.prefab, n);
        }
    }

    public void Prewarm(Projectile prefab, int count)
    {
        if (prefab == null || count <= 0)
            return;

        if (!_poolByPrefab.TryGetValue(prefab, out var stack))
        {
            stack = new Stack<Projectile>(count);
            _poolByPrefab.Add(prefab, stack);
        }

        Transform parent = transform;

        for (int i = 0; i < count; i++)
        {
            Projectile p = Instantiate(prefab, parent);
            p.SetPoolOwner(this, prefab);
            p.gameObject.SetActive(false);
            stack.Push(p);
        }
    }

    public Projectile Get(Projectile prefab, Vector3 pos, Quaternion rot)
    {
        if (prefab == null)
            return null;

        if (!_poolByPrefab.TryGetValue(prefab, out var stack))
        {
            stack = new Stack<Projectile>();
            _poolByPrefab.Add(prefab, stack);
        }

        Projectile p = null;

        if (stack.Count > 0)
        {
            p = stack.Pop();
            p.gameObject.SetActive(true);
        }
        else
        {
            if (!allowRuntimeExpand)
                return null;

            p = Instantiate(prefab, transform);
            p.SetPoolOwner(this, prefab);
            p.gameObject.SetActive(true);
        }

        p.transform.SetPositionAndRotation(pos, rot);
        return p;
    }

    public void Release(Projectile p)
    {
        if (p == null)
            return;

        Projectile prefabKey = p.PrefabKey;
        if (prefabKey == null)
        {
            Destroy(p.gameObject);
            return;
        }

        if (!_poolByPrefab.TryGetValue(prefabKey, out var stack))
        {
            stack = new Stack<Projectile>();
            _poolByPrefab.Add(prefabKey, stack);
        }

        p.gameObject.SetActive(false);
        stack.Push(p);
    }
}
