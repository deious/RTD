using UnityEngine;
using System.Collections.Generic;

public class TowerDatabase : MonoBehaviour
{
    public static TowerDatabase Instance { get; private set; }

    [Header("All Tower Data")]
    [SerializeField] private TowerData[] allTowerData;

    private Dictionary<string, TowerData> _table;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        BuildTable();
    }

    private void BuildTable()
    {
        _table = new Dictionary<string, TowerData>();

        foreach (var data in allTowerData)
        {
            if (data == null)
                continue;

            if (string.IsNullOrEmpty(data.towerId))
            {
                Debug.LogWarning("TowerData has empty towerId", data);
                continue;
            }

            if (_table.ContainsKey(data.towerId))
            {
                Debug.LogWarning($"Duplicate towerId detected: {data.towerId}", data);
                continue;
            }

            _table.Add(data.towerId, data);
        }
    }

    public TowerData Get(string towerId)
    {
        if (string.IsNullOrEmpty(towerId))
            return null;

        return _table.TryGetValue(towerId, out var data) ? data : null;
    }
}