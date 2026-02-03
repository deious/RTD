using UnityEngine;

public class MonsterTypeRegistry : MonoBehaviour
{
    public static MonsterTypeRegistry Instance { get; private set; }

    [Header("typeId = index")]
    [SerializeField] private GameObject[] prefabs;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
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