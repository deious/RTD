using System;
using System.Collections.Generic;
using UnityEngine;

public class SpectatorProjectileLibrary : MonoBehaviour
{
    public static SpectatorProjectileLibrary Instance { get; private set; }

    [Serializable]
    public class Entry
    {
        public string towerTypeId;                 // TowerData.towerId
        public SpectatorProjectile projectilePrefab;
        public float projectileSpeed = 14f;
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    private readonly Dictionary<string, Entry> _map = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _map.Clear();
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e == null) continue;
            if (string.IsNullOrEmpty(e.towerTypeId)) continue;
            if (e.projectilePrefab == null) continue;
            _map[e.towerTypeId] = e;
        }
    }

    public bool TryGet(string towerTypeId, out SpectatorProjectile prefab, out float speed)
    {
        prefab = null;
        speed = 0f;

        if (string.IsNullOrEmpty(towerTypeId)) return false;
        if (!_map.TryGetValue(towerTypeId, out var e)) return false;

        prefab = e.projectilePrefab;
        speed = Mathf.Max(0.01f, e.projectileSpeed);
        return true;
    }
}