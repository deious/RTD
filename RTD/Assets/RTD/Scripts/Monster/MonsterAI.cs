using UnityEngine;

public class MonsterAI : MonoBehaviour, IPoolable
{
    [Header("Move Settings")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float stoppingDistance = 0.05f;
    
    [Header("Stats")]
    public float maxHp = 20;
    private float currentHp;
    [SerializeField] private float shieldHp;

    [SerializeField] private Transform shieldVfxPrefab;
    private Transform _shieldVfxInstance;
    
    [Header("Status: DOT")]
    [SerializeField] private float dotDefaultTickInterval = 0.5f; // 기본 틱 간격(초)
    [SerializeField] private bool dotStrongerOverrides = true;     // 강한 DOT가 약한 DOT를 덮는가
    [SerializeField] private bool dotFasterTickPreferred = true;   // 틱 간격은 더 빠른 것을 우선할지
    
    [Header("Trait Interaction")]
    [SerializeField] private bool immuneExecute;
    public bool ImmuneExecute => immuneExecute;
    
    public float Hp01 => (maxHp <= 0) ? 0f : Mathf.Clamp01((float)currentHp / maxHp);
    public float CurrentHp => currentHp;
    public float MaxHp => maxHp;

    [Header("Status: Stun")]
    [SerializeField] private bool showStunDebug = false;
    private float stunTimer;

    [Header("Status: Burn")]
    [SerializeField] private bool allowBurnStack = true;
    private int burnStacks;
    private int burnMaxStacks;
    private float burnTimer;
    private float burnTickTimer;
    private int burnDps; // 초당 데미지(정수)

    [Header("Status: Curse (Damage Taken Mul)")]
    private float curseTimer;
    private float curseExtraMul;
    
    [Header("Lane")]
    [SerializeField] private bool randomLane = true;
    [SerializeField] private int fixedLaneIndex = 1;
    private int laneIndex;
    private Vector3 currentTargetPos;
    
    public enum ModelForwardAxis
    {
        Xp, Xm, Yp, Ym, Zp, Zm
    }

    [Header("Facing")]
    [SerializeField] private ModelForwardAxis modelForward = ModelForwardAxis.Zp;
    [SerializeField] private Vector3 modelRotationOffsetEuler = Vector3.zero;

    private float dotTimer = 0f;       // 남은 DOT 시간
    private float dotTickTimer = 0f;   // 다음 틱까지 남은 시간
    private float dotTickInterval = 0.5f;
    private float dotDamagePerTick = 0;
    
    private int currentIndex = 0;
    private float baseMoveSpeed;
    private float slowMul = 1f;
    private float slowTimer = 0f;
    private Transform currentTarget;
    private MonsterSpawner spawner;
    private bool ended;
    private bool released;
    public bool IsEnded => ended;
    
    public void SetSpawner(MonsterSpawner spawner) => this.spawner = spawner;
    
    public bool IsBoss { get; private set; }
    public static System.Action OnBossDied;

    private void Awake()
    {
        baseMoveSpeed = moveSpeed;
        currentHp = maxHp;
        shieldHp = 0;
        ended = false;
        UpdateShieldVfx();
    }

    private void Update()
    {
        UpdateDot();
        
        if (stunTimer > 0f)
        {
            stunTimer -= Time.deltaTime;
            if (stunTimer < 0f) stunTimer = 0f;
        }
        
        if (burnTimer > 0f && burnDps > 0)
        {
            burnTimer -= Time.deltaTime;
            burnTickTimer += Time.deltaTime;

            if (burnTickTimer >= 1f)
            {
                burnTickTimer -= 1f;
                TakeDamage(burnDps);
            }

            if (burnTimer <= 0f)
            {
                burnTimer = 0f;
                burnTickTimer = 0f;
                burnDps = 0;
                burnStacks = 0;
                burnMaxStacks = 0;
            }
        }
        
        if (curseTimer > 0f)
        {
            curseTimer -= Time.deltaTime;
            if (curseTimer <= 0f)
            {
                curseTimer = 0f;
                curseExtraMul = 0f;
            }
        }
        
        if (stunTimer > 0f)
            return;
        
        if (slowTimer > 0f)
        {
            slowTimer -= Time.deltaTime;
            if (slowTimer <= 0f)
            {
                slowTimer = 0f;
                slowMul = 1f;
            }
        }

        MoveAlongPath();
    }
    
    private void MoveAlongPath()
    {
        Vector3 targetPos = currentTargetPos;
        Vector3 dir = targetPos - transform.position;
        dir.y = 0f;

        float distanceToTarget = dir.magnitude;
        float effectiveSpeed = moveSpeed * slowMul;
        float moveThisFrame = effectiveSpeed * Time.deltaTime;

        if (distanceToTarget <= moveThisFrame + stoppingDistance)
        {
            currentIndex++;
            SetNextTarget();
            return;
        }

        Vector3 move = dir.normalized * moveThisFrame;
        transform.position += move;

        if (dir.sqrMagnitude > 0.0001f)
        {
            //transform.rotation = Quaternion.LookRotation(dir);
            SetRotationFacing(dir);
        }
    }

    private void SetNextTarget()
    {
        if (currentIndex >= GridManager.Instance.WaypointCount)
        {
            ReachGoal();
            return;
        }

        currentTargetPos = GridManager.Instance.GetLaneTargetPos(currentIndex, laneIndex);
    }

    private void ReachGoal()
    {
        if (ended || released)
            return;
        ended = true;
        
        if (GameRuntime.Instance != null)
            GameRuntime.Instance.ChangeLife(-1);

        if (_shieldVfxInstance != null)
            _shieldVfxInstance.gameObject.SetActive(false);
        
        if (IsBoss)
            OnBossDied?.Invoke();

        spawner?.NotifyMonsterEscaped(this);
        ReleaseToPool();
    }
    
    private void UpdateShieldVfx()
    {
        bool shouldShow = shieldHp > 0;
        
        if (shieldVfxPrefab == null)
            return;

        if (shouldShow)
        {
            if (_shieldVfxInstance == null)
            {
                _shieldVfxInstance = Instantiate(shieldVfxPrefab, transform);
            }
            
            _shieldVfxInstance.gameObject.SetActive(true);
        }
        else
        {
            if (_shieldVfxInstance != null)
            {
                _shieldVfxInstance.gameObject.SetActive(false);
            }
        }
    }
    
    private void SetRotationFacing(Vector3 dir)
    {
        if (dir.sqrMagnitude < 0.0001f)
            return;

        Quaternion dirRot = Quaternion.LookRotation(dir, Vector3.up);
        Quaternion fix = GetAxisFix(modelForward);
        Quaternion offset = Quaternion.Euler(modelRotationOffsetEuler);

        transform.rotation = dirRot * fix * offset;
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
            _ => Vector3.forward
        };

        return Quaternion.FromToRotation(modelAxis, Vector3.forward);
    }

    private void Die()
    {
        if (ended || released)
            return;
        ended = true;
        
        if (GameRuntime.Instance != null)
        {
            GameRuntime.Instance.AddGold(5);
        }

        if (_shieldVfxInstance != null)
        {
            _shieldVfxInstance.gameObject.SetActive(false);
        }
        
        if (IsBoss)
        {
            OnBossDied?.Invoke();
        }
        
        spawner?.NotifyMonsterKilled(this);
        ReleaseToPool();
    }
    
    private void ResetState()
    {
        ended = false;
        released = false;
        spawner = null;
        
        dotTimer = 0f;
        dotTickTimer = 0f;
        dotTickInterval = dotDefaultTickInterval;
        dotDamagePerTick = 0f;
        
        stunTimer = 0f;
        
        slowMul = 1f;
        slowTimer = 0f;
        
        burnStacks = 0;
        burnMaxStacks = 0;
        burnTimer = 0f;
        burnTickTimer = 0f;
        burnDps = 0;
        
        curseTimer = 0f;
        curseExtraMul = 0f;
    }

    
    private void ShowDamageText(float damage)
    {
        if (damage <= 0)
            return;

        if (DamageTextManager.Instance != null)
        {
            DamageTextManager.Instance.Spawn((int)damage, transform);
        }
    }
    
    private void UpdateDot()
    {
        if (dotTimer <= 0f) 
            return;

        dotTimer -= Time.deltaTime;
        dotTickTimer -= Time.deltaTime;

        // 프레임이 길어졌을 때 틱이 여러 번 돌아야 하는 경우도 처리
        while (dotTickTimer <= 0f && dotTimer > 0f)
        {
            dotTickTimer += dotTickInterval;

            if (dotDamagePerTick > 0)
                TakeDamage(dotDamagePerTick);

            // 죽었으면 더 돌 필요 없음
            if (currentHp <= 0)
                return;
        }

        if (dotTimer <= 0f)
        {
            dotTimer = 0f;
            dotTickTimer = 0f;
            dotDamagePerTick = 0;
            dotTickInterval = dotDefaultTickInterval;
        }
    }
    
    private void ReleaseToPool()
    {
        if (released) 
            return;
        released = true;
        
        if (SimplePool.Instance != null)
            SimplePool.Instance.Release(gameObject);
        else
            Destroy(gameObject);
    }


    public void ApplyDot(float damagePerTick, float duration, float? tickIntervalOverride = null)
    {
        if (damagePerTick <= 0 || duration <= 0f)
            return;

        float newTick = tickIntervalOverride.HasValue 
            ? Mathf.Max(0.05f, tickIntervalOverride.Value) 
            : Mathf.Max(0.05f, dotDefaultTickInterval);
        
        if (dotTimer <= 0f)
        {
            dotDamagePerTick = damagePerTick;
            dotTimer = duration;
            dotTickInterval = newTick;
            dotTickTimer = dotTickInterval;
            return;
        }
        
        if (dotStrongerOverrides)
        {
            if (damagePerTick > dotDamagePerTick)
                dotDamagePerTick = damagePerTick;
        }
        else
        {
            dotDamagePerTick = damagePerTick;
        }

        dotTimer = Mathf.Max(dotTimer, duration);

        if (dotFasterTickPreferred)
            dotTickInterval = Mathf.Min(dotTickInterval, newTick);
        else
            dotTickInterval = Mathf.Max(dotTickInterval, newTick);
    }


    public void SetIsBoss(bool isBoss)
    {
        IsBoss = isBoss;
    }
    
    public void AddBaseHp(int add)
    {
        if (add <= 0)
            return;

        maxHp += add;
        if (maxHp < 1) maxHp = 1;

        currentHp = maxHp;
    }
    
    public void ApplyWaveModifiers(WaveModifiers mods)
    {
        if (mods.speedMul > 0.01f)
            moveSpeed *= mods.speedMul;
        
        if (mods.hpMul > 0.01f)
        {
            int newMaxHp = Mathf.RoundToInt(maxHp * mods.hpMul);
            if (newMaxHp < 1) newMaxHp = 1;

            maxHp = newMaxHp;
            currentHp = maxHp;
        }
        
        if (mods.shieldHp > 0)
        {
            shieldHp += mods.shieldHp;
        }

        if (GameRuntime.Instance != null)
        {
            float spMul = GameRuntime.Instance.EnemySpeedMul;
            if (spMul > 0.01f)
                moveSpeed *= spMul;

            float hpMul = GameRuntime.Instance.EnemyHpMul;
            if (hpMul > 0.01f)
            {
                int newMaxHp = Mathf.RoundToInt(maxHp * hpMul);
                if (newMaxHp < 1) newMaxHp = 1;

                maxHp = newMaxHp;
                currentHp = maxHp;
            }
        }
        
        UpdateShieldVfx();
    }
    
    public void ApplySlow(float slowRate, float duration)
    {
        float mul = Mathf.Clamp01(1f - slowRate);
        
        if (mul < slowMul) 
            slowMul = mul;
        
        slowTimer = Mathf.Max(slowTimer, duration);
    }
    
    public void TakeDamage(float amount)
    {
        if (ended || released) 
            return;
        
        if (amount <= 0) 
            return;
        
        if (curseExtraMul > 0f)
            amount = Mathf.Max(1, Mathf.RoundToInt(amount * (1f + curseExtraMul)));
        
        float effectiveDamage = 0;

        if (shieldHp > 0)
        {
            float absorbed = Mathf.Min(shieldHp, amount);
            shieldHp -= absorbed;
            amount -= absorbed;

            effectiveDamage += absorbed;

            if (shieldHp <= 0)
            {
                shieldHp = 0;
                UpdateShieldVfx();
            }

            if (amount <= 0)
            {
                ShowDamageText(effectiveDamage);
                return;
            }
        }

        currentHp -= amount;
        effectiveDamage += amount;

        ShowDamageText(effectiveDamage);

        if (currentHp <= 0)
            Die();
    }
    
    public void ApplyBaseStats(int newMaxHp, float newMoveSpeed, int newShieldHp, bool isBoss)
    {
        maxHp = Mathf.Max(1, newMaxHp);
        currentHp = maxHp;

        moveSpeed = Mathf.Max(0.01f, newMoveSpeed);

        shieldHp = Mathf.Max(0, newShieldHp);
        UpdateShieldVfx();

        SetIsBoss(isBoss);
    }
    
    public void ApplyStun(float duration)
    {
        stunTimer = Mathf.Max(stunTimer, duration);
        if (showStunDebug) Debug.Log($"[Stun] {duration:0.00}s");
    }
    
    public void ApplyBurn(int hitDamage, float dpsRatio, float duration, int maxStacks)
    {
        int dps = Mathf.Max(1, Mathf.RoundToInt(hitDamage * dpsRatio));

        if (!allowBurnStack)
        {
            burnDps = Mathf.Max(burnDps, dps);
            burnTimer = Mathf.Max(burnTimer, duration);
            burnMaxStacks = 1;
            burnStacks = 1;
            return;
        }

        burnMaxStacks = Mathf.Max(1, maxStacks);

        if (burnStacks < burnMaxStacks)
            burnStacks++;

        // 스택이 늘면 dps를 누적 강화 (단순 설계)
        burnDps = Mathf.Max(burnDps, 0) + dps;

        burnTimer = Mathf.Max(burnTimer, duration);
    }

    public void ApplyCurse(float extraMul, float duration)
    {
        // 더 강한 저주만 덮어쓰기
        curseExtraMul = Mathf.Max(curseExtraMul, extraMul);
        curseTimer = Mathf.Max(curseTimer, duration);
    }
    
    public void ApplyArchetype(MonsterArchetypeSO archetype, bool isBoss = false)
    {
        if (archetype == null) return;

        // 베이스 스탯 통일
        maxHp = Mathf.Max(1, archetype.baseHp);
        currentHp = maxHp;

        moveSpeed = Mathf.Max(0.01f, archetype.baseMoveSpeed);
        baseMoveSpeed = moveSpeed;

        shieldHp = Mathf.Max(0, archetype.baseShieldHp);
        UpdateShieldVfx();

        SetIsBoss(isBoss);
    }

    public void ApplyColor(MonsterColorSO color)
    {
        if (color == null) 
            return;

        var slot = GetComponentInChildren<MonsterMaterialSlot>();
        if (slot != null)
            slot.Apply(color.material);
    }

    public void BeginRun()
    {
        if (GridManager.Instance == null)
        {
            Debug.LogError("GridManager 인스턴스가 없습니다.");
            enabled = false;
            return;
        }

        if (GridManager.Instance.WaypointCount == 0)
        {
            Debug.LogError("Waypoint가 하나도 없습니다. pathTiles를 설정했는지 확인하세요.");
            enabled = false;
            return;
        }

        enabled = true;

        if (randomLane)
            laneIndex = Random.Range(0, GridManager.Instance.LaneCount);
        else
            laneIndex = Mathf.Clamp(fixedLaneIndex, 0, GridManager.Instance.LaneCount - 1);

        transform.position = GridManager.Instance.GetLaneTargetPos(0, laneIndex);

        currentIndex = 1;
        SetNextTarget();
    }
    
    public void OnSpawned()
    {
        ended = false;
        released = false;
    }

    public void OnDespawned()
    {
        ResetState();
        enabled = false;
        
        if (_shieldVfxInstance != null)
            _shieldVfxInstance.gameObject.SetActive(false);
        
        IsBoss = false;
    }

}
