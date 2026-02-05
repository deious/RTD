using System;
using System.Collections.Generic;
using UnityEngine;

public class SpectatorTowerVfxRegistry : MonoBehaviour
{
    public static SpectatorTowerVfxRegistry Instance { get; private set; }

    [Serializable]
    public class Entry
    {
        public string towerTypeId;                 // TowerData.towerId
        public ParticleSystem fireVfxPrefab;       // 발사 이펙트(선택)
        public ViewProjectile viewProjectilePrefab; // 관전용 투사체(선택)
        public float projectileSpeed = 18f;
        public float projectileLifeTime = 2.5f;
        public float range = 6f;                   // 관전용 탐색 범위(선택)
        public float fireInterval = 0.8f;          // 관전용 발사 템포(선택)
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    private readonly Dictionary<string, Entry> _map = new Dictionary<string, Entry>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _map.Clear();
        foreach (var e in entries)
        {
            if (e == null) continue;
            if (string.IsNullOrEmpty(e.towerTypeId)) continue;
            _map[e.towerTypeId] = e;
        }
    }

    public bool TryGet(string towerTypeId, out Entry entry)
        => _map.TryGetValue(towerTypeId, out entry);
}