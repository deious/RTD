using System.Collections.Generic;
using UnityEngine;

public class MonsterTypeRegistry : MonoBehaviour
{
    public static MonsterTypeRegistry Instance { get; private set; }

    [Header("typeId = index")]
    [SerializeField] private GameObject[] prefabs;
    
    private Dictionary<GameObject, int> _prefabToTypeId;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    
    private void BuildReverseMap()
    {
        _prefabToTypeId = new Dictionary<GameObject, int>(prefabs != null ? prefabs.Length : 0);

        if (prefabs == null) return;
        for (int i = 0; i < prefabs.Length; i++)
        {
            var p = prefabs[i];
            if (!p) continue;
            _prefabToTypeId[p] = i;
        }
    }

    public static bool TryGetTypeId(GameObject prefab, out int typeId)
    {
        typeId = -1;

        if (prefab == null) return false;

        if (Instance == null)
        {
            Debug.LogError("[MonsterTypeRegistry] Instance is null. Add MonsterTypeRegistry to scene.");
            return false;
        }

        if (Instance._prefabToTypeId == null)
            Instance.BuildReverseMap();

        return Instance._prefabToTypeId != null && Instance._prefabToTypeId.TryGetValue(prefab, out typeId);
    }

    public static GameObject GetPrefab(int typeId)
    {
        if (Instance == null)
        {
            Debug.LogError("[MonsterTypeRegistry] Instance is null. Add MonsterTypeRegistry to scene.");
            return null;
        }

        if (Instance.prefabs == null || Instance.prefabs.Length == 0)
        {
            Debug.LogError("[MonsterTypeRegistry] prefabs is empty.");
            return null;
        }

        if (typeId < 0 || typeId >= Instance.prefabs.Length)
        {
            Debug.LogError($"[MonsterTypeRegistry] typeId out of range: {typeId}");
            return null;
        }

        return Instance.prefabs[typeId];
    }
}