using System;
using System.Collections.Generic;
using UnityEngine;

public class SpectatorProjectileLibrary : MonoBehaviour
{
    public static SpectatorProjectileLibrary Instance { get; private set; }

    [Serializable]
    public class Entry
    {
        public string towerTypeId;
        public Projectile projectilePrefab;
        public float projectileSpeed = 18f;
        public float projectileLifeTime = 2.5f;
    }

    [SerializeField] private List<Entry> entries = new();
    private readonly Dictionary<string, Entry> _map = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _map.Clear();
        foreach (var e in entries)
        {
            if (e == null) continue;
            if (string.IsNullOrEmpty(e.towerTypeId)) continue;
            if (e.projectilePrefab == null) continue;
            _map[e.towerTypeId] = e;
        }
    }
    
    public bool TryGet(string towerTypeId, out Projectile prefab, out float speed, out float lifeTime)
    {
        prefab = null; speed = 0f; lifeTime = 0f;

        if (string.IsNullOrEmpty(towerTypeId)) return false;
        if (!_map.TryGetValue(towerTypeId, out var e)) return false;

        prefab = e.projectilePrefab;
        speed = Mathf.Max(0.01f, e.projectileSpeed);
        lifeTime = Mathf.Max(0.05f, e.projectileLifeTime);
        return true;
    }
    
    public bool TryGet(string towerTypeId, out Projectile prefab, out float speed)
    {
        if (TryGet(towerTypeId, out prefab, out speed, out var lt))
            return true;

        prefab = null; speed = 0f;
        return false;
    }
}