using UnityEngine;

public class ProxyTower : MonoBehaviour
{
    public int LaneId { get; private set; }
    public int TowerId { get; private set; }
    public string TypeId { get; private set; }
    public Vector2Int GridPos { get; private set; }
    public int Level { get; private set; }

    [Header("Fallback (if no registry entry)")]
    [SerializeField] private float fallbackInterval = 0.8f;
    [SerializeField] private float fallbackRange = 6f;

    private ParticleSystem _fireVfxInstance;
    private ViewProjectile _viewProjectilePrefab;
    private float _projectileSpeed = 18f;
    private float _projectileLifeTime = 2.5f;

    private float _timer;
    private float _interval;
    private float _range;

    public void Init(int laneId, int towerId, string typeId, Vector2Int gridPos, int level)
    {
        LaneId = laneId;
        TowerId = towerId;
        TypeId = typeId;
        GridPos = gridPos;

        ApplyFromRegistryOrFallback(typeId);
        ApplyLevel(level);
    }

    private void ApplyFromRegistryOrFallback(string towerTypeId)
    {
        _interval = fallbackInterval;
        _range = fallbackRange;
        _viewProjectilePrefab = null;
        _projectileSpeed = 18f;
        _projectileLifeTime = 2.5f;

        if (SpectatorTowerVfxRegistry.Instance == null) return;

        if (!SpectatorTowerVfxRegistry.Instance.TryGet(towerTypeId, out var e) || e == null)
            return;

        _interval = Mathf.Max(0.1f, e.fireInterval);
        _range = Mathf.Max(0.5f, e.range);

        _viewProjectilePrefab = e.viewProjectilePrefab;
        _projectileSpeed = Mathf.Max(0.01f, e.projectileSpeed);
        _projectileLifeTime = Mathf.Max(0.05f, e.projectileLifeTime);

        if (e.fireVfxPrefab != null && _fireVfxInstance == null)
        {
            _fireVfxInstance = Instantiate(e.fireVfxPrefab, transform);
            _fireVfxInstance.transform.localPosition = Vector3.up * 0.8f; // 필요하면 조절
            _fireVfxInstance.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    public void ApplyLevel(int level)
    {
        Level = Mathf.Max(1, level);
        // 레벨에 따라 템포 변화 주고 싶으면 여기서 조절
        // _interval = Mathf.Max(0.15f, _interval - (Level - 1) * 0.05f);
    }

    private void Update()
    {
        _timer += Time.deltaTime;
        if (_timer < _interval) return;
        _timer = 0f;

        var target = FindNearestProxyMonster(transform.position, _range);
        if (target == null) return;

        if (ProjectilePool.Instance == null) return;
        if (SpectatorProjectileLibrary.Instance == null) return;

        if (!SpectatorProjectileLibrary.Instance.TryGet(TypeId, out var projPrefab, out var speed, out var lifeTime))
            return;

        Vector3 spawnPos = transform.position + Vector3.up * 0.8f;

        var proj = ProjectilePool.Instance.Get(projPrefab, spawnPos, Quaternion.identity);
        if (proj == null) return;
        
        var listener = proj.gameObject.GetComponent<SpectatorProjectileHitListener>();
        if (listener == null) listener = proj.gameObject.AddComponent<SpectatorProjectileHitListener>();
        listener.BindWorld(RemoteLaneWorld.Instance);
        listener.Configure(LaneId, 0f, 0f, -1, 0f, 0f, 0f, 0);

        proj.InitSpectate(target.transform, speed, lifeTime, listener);
    }

    private ProxyMonster FindNearestProxyMonster(Vector3 pos, float range)
    {
        var monsters = GameObject.FindObjectsOfType<ProxyMonster>();
        ProxyMonster best = null;
        float bestSqr = range * range;

        foreach (var m in monsters)
        {
            if (m == null || !m.isActiveAndEnabled) continue;
            float sqr = (m.transform.position - pos).sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; best = m; }
        }
        return best;
    }
}
