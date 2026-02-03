using System;
using System.Collections.Generic;
using UnityEngine;

public class TowerTypeRegistry : MonoBehaviour
{
    [Serializable]
    public struct Entry
    {
        public string towerTypeId;   // 예: "basic_01"
        public GameObject proxyPrefab; // 관전용 프리팹(없으면 실제 타워 프리팹도 가능)
    }

    public static TowerTypeRegistry Instance { get; private set; }

    [SerializeField] private Entry[] entries;

    private readonly Dictionary<string, GameObject> _map = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _map.Clear();
        foreach (var e in entries)
        {
            if (string.IsNullOrWhiteSpace(e.towerTypeId) || e.proxyPrefab == null) continue;
            _map[e.towerTypeId] = e.proxyPrefab;
        }
    }

    public GameObject GetPrefab(string towerTypeId)
    {
        if (string.IsNullOrWhiteSpace(towerTypeId)) return null;
        return _map.TryGetValue(towerTypeId, out var prefab) ? prefab : null;
    }
}
