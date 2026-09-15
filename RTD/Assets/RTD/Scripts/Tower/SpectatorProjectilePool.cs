using System.Collections.Generic;
using UnityEngine;

public class SpectatorProjectilePool : MonoBehaviour
{
    [System.Serializable]
    public class PrewarmEntry
    {
        public SpectatorProjectile prefab;
        public int count = 64;
    }

    public static SpectatorProjectilePool Instance { get; private set; }

    [SerializeField] private List<PrewarmEntry> prewarmList = new List<PrewarmEntry>();
    [SerializeField] private bool allowRuntimeExpand = true;

    private readonly Dictionary<SpectatorProjectile, Stack<SpectatorProjectile>> _poolByPrefab = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        PrewarmAll();
    }

    private void PrewarmAll()
    {
        for (int i = 0; i < prewarmList.Count; i++)
        {
            var e = prewarmList[i];
            if (e == null || e.prefab == null) continue;
            Prewarm(e.prefab, Mathf.Max(0, e.count));
        }
    }

    public void Prewarm(SpectatorProjectile prefab, int count)
    {
        if (prefab == null || count <= 0) return;

        if (!_poolByPrefab.TryGetValue(prefab, out var stack))
        {
            stack = new Stack<SpectatorProjectile>(count);
            _poolByPrefab.Add(prefab, stack);
        }

        for (int i = 0; i < count; i++)
        {
            var p = Instantiate(prefab, transform);
            p.SetPoolOwner(this, prefab);
            p.gameObject.SetActive(false);
            stack.Push(p);
        }
    }

    public SpectatorProjectile Get(SpectatorProjectile prefab, Vector3 pos, Quaternion rot)
    {
        if (prefab == null) return null;

        if (!_poolByPrefab.TryGetValue(prefab, out var stack))
        {
            stack = new Stack<SpectatorProjectile>();
            _poolByPrefab.Add(prefab, stack);
        }

        SpectatorProjectile p = null;

        if (stack.Count > 0)
        {
            p = stack.Pop();
            p.gameObject.SetActive(true);
        }
        else
        {
            if (!allowRuntimeExpand) return null;
            p = Instantiate(prefab, transform);
            p.SetPoolOwner(this, prefab);
            p.gameObject.SetActive(true);
        }

        p.transform.SetPositionAndRotation(pos, rot);
        return p;
    }

    public void Release(SpectatorProjectile p)
    {
        if (p == null) return;

        var key = p.PrefabKey;
        if (key == null) { Destroy(p.gameObject); return; }

        if (!_poolByPrefab.TryGetValue(key, out var stack))
        {
            stack = new Stack<SpectatorProjectile>();
            _poolByPrefab.Add(key, stack);
        }

        p.gameObject.SetActive(false);
        stack.Push(p);
    }
}
