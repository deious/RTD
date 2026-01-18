using UnityEngine;

public class MonsterAI : MonoBehaviour
{
    [Header("Move Settings")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float stoppingDistance = 0.05f;
    
    [Header("Stats")]
    public float maxHp = 20;
    private float _currentHp;
    [SerializeField] private float _shieldHp;

    [SerializeField] private Transform shieldVfxPrefab;
    private Transform _shieldVfxInstance;
    
    [Header("Status: DOT")]
    [SerializeField] private float dotDefaultTickInterval = 0.5f; // 기본 틱 간격(초)
    [SerializeField] private bool dotStrongerOverrides = true;     // 강한 DOT가 약한 DOT를 덮는가
    [SerializeField] private bool dotFasterTickPreferred = true;   // 틱 간격은 더 빠른 것을 우선할지
    
    [Header("Trait Interaction")]
    [SerializeField] private bool immuneExecute;
    public bool ImmuneExecute => immuneExecute;
    
    public float Hp01 => (maxHp <= 0) ? 0f : Mathf.Clamp01((float)_currentHp / maxHp);
    public float CurrentHp => _currentHp;
    public float MaxHp => maxHp;

    [Header("Status: Stun")]
    [SerializeField] private bool showStunDebug = false;
    private float _stunTimer;

    [Header("Status: Burn")]
    [SerializeField] private bool allowBurnStack = true;
    private int _burnStacks;
    private int _burnMaxStacks;
    private float _burnTimer;
    private float _burnTickTimer;
    private int _burnDps; // 초당 데미지(정수)

    [Header("Status: Curse (Damage Taken Mul)")]
    private float _curseTimer;
    private float _curseExtraMul;

    private float _dotTimer = 0f;       // 남은 DOT 시간
    private float _dotTickTimer = 0f;   // 다음 틱까지 남은 시간
    private float _dotTickInterval = 0.5f;
    private float _dotDamagePerTick = 0;
    
    private int _currentIndex = 0;
    private float _baseMoveSpeed;
    private float _slowMul = 1f;
    private float _slowTimer = 0f;
    private Transform _currentTarget;
    
    public bool IsBoss { get; private set; }
    public static System.Action OnBossDied;

    private void Awake()
    {
        _baseMoveSpeed = moveSpeed;
        _currentHp = maxHp;
        _shieldHp = 0;
        UpdateShieldVfx();
    }
    
    private void Start()
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
        
        Transform startPoint = GridManager.Instance.GetWaypoint(0);
        transform.position = startPoint.position;
        
        _currentIndex = 1;
        SetNextTarget();
    }

    private void Update()
    {
        if (_stunTimer > 0f)
        {
            _stunTimer -= Time.deltaTime;
            if (_stunTimer < 0f) _stunTimer = 0f;
        }
        
        if (_burnTimer > 0f && _burnDps > 0)
        {
            _burnTimer -= Time.deltaTime;
            _burnTickTimer += Time.deltaTime;

            if (_burnTickTimer >= 1f)
            {
                _burnTickTimer -= 1f;
                TakeDamage(_burnDps);
            }

            if (_burnTimer <= 0f)
            {
                _burnTimer = 0f;
                _burnTickTimer = 0f;
                _burnDps = 0;
                _burnStacks = 0;
                _burnMaxStacks = 0;
            }
        }
        
        if (_curseTimer > 0f)
        {
            _curseTimer -= Time.deltaTime;
            if (_curseTimer <= 0f)
            {
                _curseTimer = 0f;
                _curseExtraMul = 0f;
            }
        }
        
        if (_stunTimer > 0f)
            return;
        
        if (_slowTimer > 0f)
        {
            _slowTimer -= Time.deltaTime;
            if (_slowTimer <= 0f)
            {
                _slowTimer = 0f;
                _slowMul = 1f;
            }
        }

        MoveAlongPath();
    }
    
    private void MoveAlongPath()
    {
        if (_currentTarget == null)
            return;

        Vector3 targetPos = _currentTarget.position;
        Vector3 dir = targetPos - transform.position;
        
        dir.y = 0f;

        float distanceToTarget = dir.magnitude;
        float effectiveSpeed = moveSpeed * _slowMul;
        float moveThisFrame = effectiveSpeed * Time.deltaTime;
        
        if (distanceToTarget <= moveThisFrame + stoppingDistance)
        {
            _currentIndex++;
            SetNextTarget();
            return;
        }

        Vector3 move = dir.normalized * moveThisFrame;
        transform.position += move;
        
        if (dir.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(dir);
        }
    }

    private void SetNextTarget()
    {
        if (_currentIndex >= GridManager.Instance.WaypointCount)
        {
            ReachGoal();
            return;
        }

        _currentTarget = GridManager.Instance.GetWaypoint(_currentIndex);
    }

    private void ReachGoal()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.ChangeLife(-1);

        if (_shieldVfxInstance != null)
            Destroy(_shieldVfxInstance.gameObject);
        
        if (IsBoss)
            OnBossDied?.Invoke();

        Destroy(gameObject);
    }
    
    private void UpdateShieldVfx()
    {
        bool shouldShow = _shieldHp > 0;
        
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

    private void Die()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddGold(5);
        }

        if (_shieldVfxInstance != null)
        {
            Destroy(_shieldVfxInstance.gameObject);
        }
        
        if (IsBoss)
        {
            OnBossDied?.Invoke();
        }
        
        Destroy(gameObject);
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
        if (_dotTimer <= 0f) 
            return;

        _dotTimer -= Time.deltaTime;
        _dotTickTimer -= Time.deltaTime;

        // 프레임이 길어졌을 때 틱이 여러 번 돌아야 하는 경우도 처리
        while (_dotTickTimer <= 0f && _dotTimer > 0f)
        {
            _dotTickTimer += _dotTickInterval;

            if (_dotDamagePerTick > 0)
                TakeDamage(_dotDamagePerTick);

            // 죽었으면 더 돌 필요 없음
            if (_currentHp <= 0)
                return;
        }

        if (_dotTimer <= 0f)
        {
            _dotTimer = 0f;
            _dotTickTimer = 0f;
            _dotDamagePerTick = 0;
            _dotTickInterval = dotDefaultTickInterval;
        }
    }

    public void ApplyDot(float damagePerTick, float duration, float? tickIntervalOverride = null)
    {
        if (damagePerTick <= 0 || duration <= 0f)
            return;

        float newTick = tickIntervalOverride.HasValue 
            ? Mathf.Max(0.05f, tickIntervalOverride.Value) 
            : Mathf.Max(0.05f, dotDefaultTickInterval);
        
        if (_dotTimer <= 0f)
        {
            _dotDamagePerTick = damagePerTick;
            _dotTimer = duration;
            _dotTickInterval = newTick;
            _dotTickTimer = _dotTickInterval;
            return;
        }
        
        if (dotStrongerOverrides)
        {
            if (damagePerTick > _dotDamagePerTick)
                _dotDamagePerTick = damagePerTick;
        }
        else
        {
            _dotDamagePerTick = damagePerTick;
        }

        _dotTimer = Mathf.Max(_dotTimer, duration);

        if (dotFasterTickPreferred)
            _dotTickInterval = Mathf.Min(_dotTickInterval, newTick);
        else
            _dotTickInterval = Mathf.Max(_dotTickInterval, newTick);
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

        _currentHp = maxHp;
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
            _currentHp = maxHp;
        }
        
        if (mods.shieldHp > 0)
        {
            _shieldHp += mods.shieldHp;
        }

        if (GameManager.Instance != null)
        {
            float spMul = GameManager.Instance.EnemySpeedMul;
            if (spMul > 0.01f)
                moveSpeed *= spMul;

            float hpMul = GameManager.Instance.EnemyHpMul;
            if (hpMul > 0.01f)
            {
                int newMaxHp = Mathf.RoundToInt(maxHp * hpMul);
                if (newMaxHp < 1) newMaxHp = 1;

                maxHp = newMaxHp;
                _currentHp = maxHp;
            }
        }
        
        UpdateShieldVfx();
    }
    
    public void ApplySlow(float slowRate, float duration)
    {
        float mul = Mathf.Clamp01(1f - slowRate);
        
        if (mul < _slowMul) 
            _slowMul = mul;
        
        _slowTimer = Mathf.Max(_slowTimer, duration);
    }
    
    public void TakeDamage(float amount)
    {
        if (amount <= 0) return;
        
        if (_curseExtraMul > 0f)
            amount = Mathf.Max(1, Mathf.RoundToInt(amount * (1f + _curseExtraMul)));
        
        float effectiveDamage = 0;

        if (_shieldHp > 0)
        {
            float absorbed = Mathf.Min(_shieldHp, amount);
            _shieldHp -= absorbed;
            amount -= absorbed;

            effectiveDamage += absorbed;

            if (_shieldHp <= 0)
            {
                _shieldHp = 0;
                UpdateShieldVfx();
            }

            if (amount <= 0)
            {
                ShowDamageText(effectiveDamage);
                return;
            }
        }

        _currentHp -= amount;
        effectiveDamage += amount;

        ShowDamageText(effectiveDamage);

        if (_currentHp <= 0)
            Die();
    }
    
    public void ApplyBaseStats(int newMaxHp, float newMoveSpeed, int newShieldHp, bool isBoss)
    {
        maxHp = Mathf.Max(1, newMaxHp);
        _currentHp = maxHp;

        moveSpeed = Mathf.Max(0.01f, newMoveSpeed);

        _shieldHp = Mathf.Max(0, newShieldHp);
        UpdateShieldVfx();

        SetIsBoss(isBoss);
    }
    
    public void ApplyStun(float duration)
    {
        _stunTimer = Mathf.Max(_stunTimer, duration);
        if (showStunDebug) Debug.Log($"[Stun] {duration:0.00}s");
    }
    
    public void ApplyBurn(int hitDamage, float dpsRatio, float duration, int maxStacks)
    {
        int dps = Mathf.Max(1, Mathf.RoundToInt(hitDamage * dpsRatio));

        if (!allowBurnStack)
        {
            _burnDps = Mathf.Max(_burnDps, dps);
            _burnTimer = Mathf.Max(_burnTimer, duration);
            _burnMaxStacks = 1;
            _burnStacks = 1;
            return;
        }

        _burnMaxStacks = Mathf.Max(1, maxStacks);

        if (_burnStacks < _burnMaxStacks)
            _burnStacks++;

        // 스택이 늘면 dps를 누적 강화 (단순 설계)
        _burnDps = Mathf.Max(_burnDps, 0) + dps;

        _burnTimer = Mathf.Max(_burnTimer, duration);
    }

    public void ApplyCurse(float extraMul, float duration)
    {
        // 더 강한 저주만 덮어쓰기
        _curseExtraMul = Mathf.Max(_curseExtraMul, extraMul);
        _curseTimer = Mathf.Max(_curseTimer, duration);
    }
}
