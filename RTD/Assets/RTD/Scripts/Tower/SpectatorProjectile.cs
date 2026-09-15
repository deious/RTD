using UnityEngine;

public class SpectatorProjectile : MonoBehaviour
{
    public enum MoveType { Straight, Homing }

    [Header("Movement")]
    [SerializeField] private MoveType moveType = MoveType.Straight;
    [SerializeField] private float homingTurnSpeed = 20f;

    [Header("Life")]
    [SerializeField] private float lifeTime = 1.5f;

    private Transform _target;
    private float _speed;
    private float _life;

    private SpectatorProjectilePool _pool;
    public SpectatorProjectile PrefabKey { get; private set; }

    public void SetPoolOwner(SpectatorProjectilePool owner, SpectatorProjectile prefabKey)
    {
        _pool = owner;
        PrefabKey = prefabKey;
    }

    public void Init(Transform target, float speed)
    {
        _target = target;
        _speed = Mathf.Max(0.01f, speed);
        _life = 0f;
    }

    private void OnEnable()
    {
        _life = 0f;
    }

    private void Update()
    {
        _life += Time.deltaTime;
        if (_life >= lifeTime)
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
        float dist = _speed * Time.deltaTime;

        if (to.sqrMagnitude <= dist * dist)
        {
            // 관전용: 데미지/상태이상 없음. 그냥 도착하면 소멸
            ReleaseOrDestroy();
            return;
        }

        Vector3 dir = to.normalized;
        transform.position += dir * dist;

        if (moveType == MoveType.Homing)
        {
            Quaternion look = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, homingTurnSpeed * Time.deltaTime);
        }
        else
        {
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }
    }

    private void ReleaseOrDestroy()
    {
        if (_pool != null && SpectatorProjectilePool.Instance != null)
        {
            _pool.Release(this);
            return;
        }
        Destroy(gameObject);
    }
}
