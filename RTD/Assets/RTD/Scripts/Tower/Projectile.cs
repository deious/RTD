using UnityEngine;

public class Projectile : MonoBehaviour
{
    private Transform _target;
    private MonsterAI _targetMonster;
    private float _speed;
    private int _damage;
    private float _lifeTime;
    private float _lifeTimer;
    private TowerBase _sourceTower;
    
    private float _splashRadius;
    private float _splashRatio;

    private ProjectilePool _poolOwner;
    public Projectile PrefabKey { get; private set; }

    public void SetPoolOwner(ProjectilePool owner, Projectile prefabKey)
    {
        _poolOwner = owner;
        PrefabKey = prefabKey;
    }

    public void Init(
        MonsterAI target,
        float speed,
        int damage,
        float lifeTime,
        TowerBase sourceTower,
        float splashRadius = 0f,
        float splashRatio = 0f
    )
    {
        _targetMonster = target;
        _target = (target != null) ? target.transform : null;
        _speed = speed;
        _damage = damage;
        _lifeTime = lifeTime;
        _lifeTimer = 0f;
        _sourceTower = sourceTower;

        _splashRadius = Mathf.Max(0f, splashRadius);
        _splashRatio = Mathf.Clamp01(splashRatio);
    }

    private void Update()
    {
        _lifeTimer += Time.deltaTime;
        if (_lifeTimer >= _lifeTime)
        {
            ReleaseOrDestroy();
            return;
        }

        if (_target == null)
        {
            ReleaseOrDestroy();
            return;
        }

        Vector3 to = _target.position - transform.position;
        float distThisFrame = _speed * Time.deltaTime;

        if (to.sqrMagnitude <= distThisFrame * distThisFrame)
        {
            Hit();
            return;
        }

        Vector3 dir = to.normalized;
        transform.position += dir * distThisFrame;
        transform.forward = dir;
    }

    private void Hit()
    {
        if (_targetMonster != null)
        {
            if (_sourceTower != null)
                _sourceTower.ApplyHitAndReturnDamage(_targetMonster, _damage);
            else
                _targetMonster.TakeDamage(_damage);
            
            if (_splashRadius > 0.01f && _splashRatio > 0f)
            {
                int splashDmg = Mathf.Max(1, Mathf.RoundToInt(_damage * _splashRatio));
                TraitProcessor.ApplySplashDamage(_sourceTower, _targetMonster.transform.position, _splashRadius, _targetMonster, splashDmg);
            }
        }

        ReleaseOrDestroy();
    }

    private void ReleaseOrDestroy()
    {
        if (_poolOwner != null && ProjectilePool.Instance != null)
        {
            _poolOwner.Release(this);
            return;
        }

        Destroy(gameObject);
    }
}
