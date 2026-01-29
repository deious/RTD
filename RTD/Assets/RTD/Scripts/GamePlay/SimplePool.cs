using System.Collections.Generic;
using UnityEngine;

public class SimplePool : MonoBehaviour
{
    public static SimplePool Instance { get; private set; }

    [SerializeField] private Transform poolRoot;

    private readonly Dictionary<GameObject, Stack<GameObject>> pool = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (poolRoot == null)
        {
            var go = new GameObject("PoolRoot");
            go.transform.SetParent(transform);
            poolRoot = go.transform;
        }
    }

    public GameObject Get(GameObject prefab)
    {
        if (prefab == null) return null;

        if (pool.TryGetValue(prefab, out var stack) && stack.Count > 0)
        {
            var obj = stack.Pop();
            if (obj == null) return Get(prefab);

            var po = obj.GetComponent<PooledObject>();
            if (po != null) po.InPool = false;

            obj.transform.SetParent(null);
            obj.SetActive(true);

            NotifySpawned(obj);
            return obj;
        }

        var created = Instantiate(prefab);
        var poNew = created.GetComponent<PooledObject>();
        if (poNew == null) poNew = created.AddComponent<PooledObject>();
        poNew.SetKey(prefab);
        poNew.InPool = false;

        created.SetActive(true);
        NotifySpawned(created);
        return created;
    }

    public void Release(GameObject obj)
    {
        if (obj == null) return;

        var po = obj.GetComponent<PooledObject>();
        if (po == null || po.PrefabKey == null)
        {
            Destroy(obj);
            return;
        }
        
#if UNITY_EDITOR
        string nowStack = System.Environment.StackTrace;
#else
        string nowStack = null;
#endif

        if (po.InPool)
        {
            Debug.LogError(
                $"[Pool] DOUBLE RELEASE detected: {obj.name} (id={obj.GetInstanceID()})\n" +
                $"--- First Release --- frame={po.LastReleaseFrame}\n{po.LastReleaseStack}\n" +
                $"--- Second Release --- frame={Time.frameCount}\n{nowStack}"
            );
            return; // 🔥 중복 push 차단
        }

        po.InPool = true;
        //
        po.LastReleaseFrame = Time.frameCount;
        po.LastReleaseStack = nowStack;
        //
        NotifyDespawned(obj);

        obj.SetActive(false);
        obj.transform.SetParent(poolRoot);

        if (!pool.TryGetValue(po.PrefabKey, out var stack))
        {
            stack = new Stack<GameObject>();
            pool.Add(po.PrefabKey, stack);
        }
        stack.Push(obj);
    }


    private static void NotifySpawned(GameObject obj)
    {
        var poolables = obj.GetComponentsInChildren<IPoolable>(true);
        for (int i = 0; i < poolables.Length; i++)
            poolables[i].OnSpawned();
    }

    private static void NotifyDespawned(GameObject obj)
    {
        var poolables = obj.GetComponentsInChildren<IPoolable>(true);
        for (int i = 0; i < poolables.Length; i++)
            poolables[i].OnDespawned();
    }
}
