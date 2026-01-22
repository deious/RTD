using UnityEngine;

public class Projectile : MonoBehaviour
{
    public enum MoveType
    {
        Straight,
        Homing,
        Ballistic
    }

    public enum ModelForwardAxis
    {
        Xp, Xm, Yp, Ym, Zp, Zm
    }

    [Header("Movement")]
    [SerializeField] private MoveType moveType = MoveType.Straight;

    [Tooltip("모델(화살촉)이 바라보는 로컬 축")]
    [SerializeField] private ModelForwardAxis modelForward = ModelForwardAxis.Yp;

    [Tooltip("유도 회전 속도. (Homing에 사용)")]
    [SerializeField] private float homingTurnSpeed = 20f;

    [Header("Ballistic")]
    [Tooltip("곡사 중력 가속도(양수). (Ballistic에 사용)")]
    [SerializeField] private float gravity = 25f;

    [Tooltip("곡사 비행시간(초). 짧을수록 포물선이 빠르고 낮아짐. (Ballistic에 사용)")]
    [SerializeField] private float ballisticFlightTime = 0.55f;
    
    [Header("Ballistic Arc")]
    [SerializeField] private float arcHeight = 2.0f;
    
    private Transform _target;
    private MonsterAI _targetMonster;

    private float _speed;
    private int _damage;

    private float _lifeTime;
    private float _lifeTimer;

    private TowerBase _sourceTower;
    private IProjectileHitListener _hitListener;

    private float _splashRadius;
    private float _splashRatio;
    
    private bool _ballisticInited;
    private Vector3 _ballisticStart;
    private Vector3 _ballisticEnd;
    private float _ballisticT;
    private Vector3 _ballisticVel;

    private ProjectilePool _poolOwner;
    public Projectile PrefabKey { get; private set; }

    public void SetPoolOwner(ProjectilePool owner, Projectile prefabKey)
    {
        _poolOwner = owner;
        PrefabKey = prefabKey;
    }

    private void OnEnable()
    {
        _lifeTimer = 0f;
        _ballisticInited = false;
        _ballisticT = 0f;
        _ballisticVel = Vector3.zero;
    }

    public void Init(
        MonsterAI target,
        float speed,
        int damage,
        float lifeTime,
        TowerBase sourceTower,
        IProjectileHitListener hitListener = null,
        float splashRadius = 0f,
        float splashRatio = 0f
    )
    {
        _targetMonster = target;
        _target = (target != null) ? target.transform : null;

        _speed = Mathf.Max(0.01f, speed);
        _damage = damage;

        _lifeTime = Mathf.Max(0.01f, lifeTime);
        _lifeTimer = 0f;

        _sourceTower = sourceTower;
        _hitListener = hitListener;

        _splashRadius = Mathf.Max(0f, splashRadius);
        _splashRatio = Mathf.Clamp01(splashRatio);
        
        _ballisticInited = false;
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
            if (moveType != MoveType.Ballistic)
            {
                ReleaseOrDestroy();
                return;
            }
        }

        switch (moveType)
        {
            case MoveType.Straight:
                UpdateStraight();
                break;
            case MoveType.Homing:
                UpdateHoming();
                break;
            case MoveType.Ballistic:
                UpdateBallistic();
                break;
        }
    }

    private void UpdateStraight()
    {
        Vector3 to = _target.position - transform.position;
        float distThisFrame = _speed * Time.deltaTime;

        if (to.sqrMagnitude <= distThisFrame * distThisFrame)
        {
            Hit(_target.position);
            return;
        }

        Vector3 dir = to.normalized;
        transform.position += dir * distThisFrame;

        SetRotationFacing(dir, instant: true);
    }

    private void UpdateHoming()
    {
        Vector3 to = _target.position - transform.position;
        float distThisFrame = _speed * Time.deltaTime;

        if (to.sqrMagnitude <= distThisFrame * distThisFrame)
        {
            Hit(_target.position);
            return;
        }

        Vector3 dir = to.normalized;
        transform.position += dir * distThisFrame;

        SetRotationFacing(dir, instant: false);
    }

    private void UpdateBallistic()
    {
        if (!_ballisticInited)
        {
            _ballisticInited = true;
            _ballisticStart = transform.position;
            _ballisticEnd = _target.position;
            
            float dist = Vector3.Distance(_ballisticStart, _ballisticEnd);
            float baseHeight = Mathf.Clamp(dist * 0.25f, 1.5f, 5.0f);
            float usedArcHeight = Mathf.Max(0.1f, baseHeight + arcHeight);

            float T = Mathf.Max(0.05f, ballisticFlightTime);
            _ballisticT = 0f;
            float g = Mathf.Abs(gravity);

            Vector3 delta = _ballisticEnd - _ballisticStart;
            
            float peakY = Mathf.Max(_ballisticStart.y, _ballisticEnd.y) + usedArcHeight;

            float vy = Mathf.Sqrt(2f * g * Mathf.Max(0.01f, peakY - _ballisticStart.y));
            float tUp = vy / g;
            float totalT = Mathf.Max(T, tUp * 2f);

            Vector3 deltaXZ = new Vector3(delta.x, 0f, delta.z);
            Vector3 vXZ = deltaXZ / totalT;

            _ballisticVel = new Vector3(vXZ.x, vy, vXZ.z);
            ballisticFlightTime = totalT;
        }

        float Tflight = Mathf.Max(0.05f, ballisticFlightTime);
        float dt = Time.deltaTime;

        _ballisticT += dt;
        float t = _ballisticT;

        Vector3 a2 = new Vector3(0f, -Mathf.Abs(gravity), 0f);
        Vector3 pos = _ballisticStart + _ballisticVel * t + 0.5f * a2 * t * t;

        Vector3 vel = _ballisticVel + a2 * t;
        transform.position = pos;

        if (vel.sqrMagnitude > 0.0001f)
            SetRotationFacing(vel.normalized, instant: true);

        if (t >= Tflight)
        {
            transform.position = _ballisticEnd;
            Hit(_ballisticEnd);
        }
    }

    private void SetRotationFacing(Vector3 dir, bool instant)
    {
        Quaternion dirRot = Quaternion.LookRotation(dir, Vector3.up);
        Quaternion fix = GetAxisFix(modelForward);

        Quaternion targetRot = dirRot * fix;

        if (instant)
        {
            transform.rotation = targetRot;
            return;
        }

        float k = Mathf.Max(0.01f, homingTurnSpeed);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, k * Time.deltaTime);
    }

    private static Quaternion GetAxisFix(ModelForwardAxis axis)
    {
        Vector3 modelAxis = axis switch
        {
            ModelForwardAxis.Xp => Vector3.right,
            ModelForwardAxis.Xm => Vector3.left,
            ModelForwardAxis.Yp => Vector3.up,
            ModelForwardAxis.Ym => Vector3.down,
            ModelForwardAxis.Zp => Vector3.forward,
            ModelForwardAxis.Zm => Vector3.back,
            _ => Vector3.up
        };

        return Quaternion.FromToRotation(modelAxis, Vector3.forward);
    }
    
    private void Hit(Vector3 hitPos)
    {
        int dealt = 0;

        if (_targetMonster != null)
        {
            if (_sourceTower != null)
                dealt = _sourceTower.ApplyHitAndReturnDamage(_targetMonster, _damage);
            else
            {
                _targetMonster.TakeDamage(_damage);
                dealt = _damage;
            }

            if (_splashRadius > 0.01f && _splashRatio > 0f)
            {
                int splashDmg = Mathf.Max(1, Mathf.RoundToInt(_damage * _splashRatio));
                TraitProcessor.ApplySplashDamage(_sourceTower, hitPos, _splashRadius, _targetMonster, splashDmg);
            }
        }
        
        _hitListener?.OnProjectileHit(_targetMonster, hitPos, dealt);

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
