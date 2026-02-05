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
    [SerializeField] private ModelForwardAxis modelForward = ModelForwardAxis.Yp;
    [SerializeField] private float homingTurnSpeed = 20f;

    [Header("Ballistic")]
    [SerializeField] private float gravity = 25f;
    [SerializeField] private float ballisticFlightTime = 0.55f;
    [SerializeField] private float arcHeight = 2.0f;

    private Transform target;
    private MonsterAI targetMonster;

    private float speed;
    private int damage;
    private float lifeTime;
    private float lifeTimer;

    private TowerBase sourceTower;
    private IProjectileHitListener hitListener;

    private float splashRadius;
    private float splashRatio;

    private bool ballisticInited;
    private Vector3 ballisticStart;
    private Vector3 ballisticEnd;
    private float ballisticT;
    private Vector3 ballisticVel;

    private ProjectilePool poolOwner;
    public Projectile PrefabKey { get; private set; }

    public void SetPoolOwner(ProjectilePool owner, Projectile prefabKey)
    {
        poolOwner = owner;
        PrefabKey = prefabKey;
    }

    private void OnEnable()
    {
        lifeTimer = 0f;
        ballisticInited = false;
        ballisticT = 0f;
        ballisticVel = Vector3.zero;
    }

    public void Init(
        MonsterAI target,
        float speed,
        int damage,
        float lifeTime,
        TowerBase sourceTower,
        IProjectileHitListener hitListener = null,
        float splashRadius = 0f,
        float splashRatio = 0f)
    {
        targetMonster = target;
        this.target = target != null ? target.transform : null;

        this.speed = Mathf.Max(0.01f, speed);
        this.damage = damage;
        this.lifeTime = Mathf.Max(0.01f, lifeTime);
        this.lifeTimer = 0f;

        this.sourceTower = sourceTower;
        this.hitListener = hitListener;

        this.splashRadius = Mathf.Max(0f, splashRadius);
        this.splashRatio = Mathf.Clamp01(splashRatio);

        ballisticInited = false;
    }

    private void Update()
    {
        lifeTimer += Time.deltaTime;
        if (lifeTimer >= lifeTime)
        {
            ReleaseOrDestroy();
            return;
        }

        if (target == null && moveType != MoveType.Ballistic)
        {
            ReleaseOrDestroy();
            return;
        }

        switch (moveType)
        {
            case MoveType.Straight:   UpdateStraight(); break;
            case MoveType.Homing:     UpdateHoming(); break;
            case MoveType.Ballistic:  UpdateBallistic(); break;
        }
    }

    private void UpdateStraight()
    {
        Vector3 to = target.position - transform.position;
        float dist = speed * Time.deltaTime;

        if (to.sqrMagnitude <= dist * dist)
        {
            Hit(target.position);
            return;
        }

        Vector3 dir = to.normalized;
        transform.position += dir * dist;
        SetRotationFacing(dir, true);
    }

    private void UpdateHoming()
    {
        Vector3 to = target.position - transform.position;
        float dist = speed * Time.deltaTime;

        if (to.sqrMagnitude <= dist * dist)
        {
            Hit(target.position);
            return;
        }

        Vector3 dir = to.normalized;
        transform.position += dir * dist;
        SetRotationFacing(dir, false);
    }

    private void UpdateBallistic()
    {
        if (!ballisticInited)
        {
            ballisticInited = true;
            ballisticStart = transform.position;
            ballisticEnd = target.position;

            float dist = Vector3.Distance(ballisticStart, ballisticEnd);
            float baseHeight = Mathf.Clamp(dist * 0.25f, 1.5f, 5f);
            float usedArc = Mathf.Max(0.1f, baseHeight + arcHeight);

            float g = Mathf.Abs(gravity);
            float peakY = Mathf.Max(ballisticStart.y, ballisticEnd.y) + usedArc;
            float vy = Mathf.Sqrt(2f * g * Mathf.Max(0.01f, peakY - ballisticStart.y));

            float tUp = vy / g;
            float totalT = Mathf.Max(ballisticFlightTime, tUp * 2f);

            Vector3 deltaXZ = new Vector3(ballisticEnd.x - ballisticStart.x, 0f, ballisticEnd.z - ballisticStart.z);
            Vector3 vXZ = deltaXZ / totalT;

            ballisticVel = new Vector3(vXZ.x, vy, vXZ.z);
            ballisticFlightTime = totalT;
        }

        ballisticT += Time.deltaTime;
        Vector3 acc = new Vector3(0f, -Mathf.Abs(gravity), 0f);

        Vector3 pos = ballisticStart + ballisticVel * ballisticT + 0.5f * acc * ballisticT * ballisticT;
        Vector3 vel = ballisticVel + acc * ballisticT;

        transform.position = pos;
        if (vel.sqrMagnitude > 0.001f)
            SetRotationFacing(vel.normalized, true);

        if (ballisticT >= ballisticFlightTime)
        {
            transform.position = ballisticEnd;
            Hit(ballisticEnd);
        }
    }

    private void Hit(Vector3 hitPos)
    {
        int dealt = 0;

        bool canHitTarget =
            targetMonster != null &&
            !targetMonster.IsEnded &&
            targetMonster.gameObject.activeInHierarchy;
        
        if (canHitTarget)
        {
            if (sourceTower != null)
                dealt = sourceTower.ApplyHitAndReturnDamage(targetMonster, damage);
            else
                targetMonster.TakeDamage(damage);
        }
        
        if (splashRadius > 0.01f && splashRatio > 0f)
        {
            int splashDmg = Mathf.Max(1, Mathf.RoundToInt(damage * splashRatio));
            TraitProcessor.ApplySplashDamage(sourceTower, hitPos, splashRadius, targetMonster, splashDmg);
        }

        hitListener?.OnProjectileHit(targetMonster, hitPos, dealt);
        ReleaseOrDestroy();
    }

    private void ReleaseOrDestroy()
    {
        if (poolOwner != null && ProjectilePool.Instance != null)
        {
            poolOwner.Release(this);
            return;
        }

        Destroy(gameObject);
    }

    private void SetRotationFacing(Vector3 dir, bool instant)
    {
        Quaternion dirRot = Quaternion.LookRotation(dir, Vector3.up);
        Quaternion fix = GetAxisFix(modelForward);
        Quaternion targetRot = dirRot * fix;

        if (instant)
            transform.rotation = targetRot;
        else
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, homingTurnSpeed * Time.deltaTime);
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
    
    public void InitSpectate(
        Transform targetTransform,
        float speed,
        float lifeTime,
        IProjectileHitListener hitListener = null)
    {
        targetMonster = null;
        target = targetTransform;

        this.speed = Mathf.Max(0.01f, speed);
        this.damage = 0;
        this.lifeTime = Mathf.Max(0.01f, lifeTime);
        this.lifeTimer = 0f;

        this.sourceTower = null;
        this.hitListener = hitListener;

        this.splashRadius = 0f;
        this.splashRatio = 0f;

        ballisticInited = false;
    }
}
