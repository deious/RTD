using UnityEngine;

public class MonsterAI : MonoBehaviour
{
    [Header("Move Settings")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float stoppingDistance = 0.05f;
    
    [Header("Stats")]
    public int maxHp = 20;
    private int _currentHp;
    [SerializeField] private int _shieldHp;

    [SerializeField] private Transform shieldVfxPrefab;
    private Transform _shieldVfxInstance;
    
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
        if (_shieldVfxInstance != null)
        {
            Destroy(_shieldVfxInstance.gameObject);
        }
        
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
    
    private void ShowDamageText(int damage)
    {
        if (damage <= 0)
            return;

        if (DamageTextManager.Instance != null)
        {
            DamageTextManager.Instance.Spawn(damage, transform);
        }
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
    
    public void TakeDamage(int amount)
    {
        if (amount <= 0)
            return;

        int effectiveDamage = 0;

        // 1) 실드 처리
        if (_shieldHp > 0)
        {
            int absorbed = Mathf.Min(_shieldHp, amount);
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
        {
            Die();
        }
    }
}
