using UnityEngine;

public class Projectile : MonoBehaviour
{
    private Transform _target;
    private MonsterAI _targetMonster;
    private float _speed;
    private int _damage;
    private float _lifeTime;
    private float _lifeTimer;

    private ProjectilePool _poolOwner;
    public Projectile PrefabKey { get; private set; }

    public void SetPoolOwner(ProjectilePool owner, Projectile prefabKey)
    {
        _poolOwner = owner;
        PrefabKey = prefabKey;
    }

    public void Init(MonsterAI target, float speed, int damage, float lifeTime = 3f)
    {
        _targetMonster = target;
        _target = (target != null) ? target.transform : null;
        _speed = speed;
        _damage = damage;
        _lifeTime = lifeTime;
        _lifeTimer = 0f;
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
            _targetMonster.TakeDamage(_damage);

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