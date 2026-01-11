using UnityEngine;
using UnityEngine.InputSystem;
using RTD.Scripts.GamePlay.Wave;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private int startGold = 100;
    [SerializeField] private int startLife = 10;
    [SerializeField] private int startWave = 1;
    [SerializeField] private int maxWave = 20;
    
    [Header("Wave Patterns")]
    [SerializeField] private WavePatternSO[] wavePatterns;
    
    [Header("Systems")]
    [SerializeField] private AugmentSystem augmentSystem;

    private int gold;
    private int life;
    private int currentWave;
    
    private WaveModifiers _currentWaveMods;

    public float TowerDamageMul => (augmentSystem != null) ? augmentSystem.TowerDamageMul : 1f;
    public float TowerAttackSpeedMul => (augmentSystem != null) ? augmentSystem.TowerAttackSpeedMul : 1f;
    public float TowerRangeAdd => (augmentSystem != null) ? augmentSystem.TowerRangeAdd : 0f;

    public float EnemySpeedMul => (augmentSystem != null) ? augmentSystem.EnemySpeedMul : 1f;
    public float EnemyHpMul => (augmentSystem != null) ? augmentSystem.EnemyHpMul : 1f;
    // 외부에서 골드 읽을 수 있게 프로퍼티
    public int Gold => gold;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        if (augmentSystem == null)
            augmentSystem = FindFirstObjectByType<AugmentSystem>();
        
        Instance = this;
        
        TraitProcessor.MonsterLayerMask = LayerMask.GetMask("Monster");
        
        gold = startGold;
        life = startLife;
        currentWave = startWave;
    }

    private void Start()
    {
        UIManager.Instance.UpdateGold(gold);
        UIManager.Instance.UpdateLife(life);
        UIManager.Instance.UpdateWave(currentWave, maxWave);
        StartWave(currentWave);
    }

    private void Update()
    {
        if (augmentSystem != null && augmentSystem.IsChoosing)
            return;
        
        var keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard.kKey.wasPressedThisFrame)
        {
            if (augmentSystem != null)
                augmentSystem.BeginChoice(null);
        }
        
        // 디버그용 키
        if (keyboard.gKey.wasPressedThisFrame)
        {
            AddGold(10);
        }

        if (keyboard.hKey.wasPressedThisFrame)
        {
            ChangeLife(-1);
        }

        if (keyboard.jKey.wasPressedThisFrame)
        {
            NextWave();
        }
    }
    
    public void AddGold(int amount)
    {
        gold += amount;
        if (gold < 0) 
            gold = 0;

        UIManager.Instance.UpdateGold(gold);
    }

    public void ChangeLife(int amount)
    {
        life += amount;
        UIManager.Instance.UpdateLife(life);
    }

    public void NextWave()
    {
        currentWave++;
        if (currentWave > maxWave)
            currentWave = maxWave;

        UIManager.Instance.UpdateWave(currentWave, maxWave);

        StartWave(currentWave);
    }
    
    private void StartWave(int waveIndex)
    {
        WavePatternSO pattern = FindWavePattern(waveIndex);

        if (pattern != null)
        {
            // 패턴에 들어있는 modifier type을 기존 WaveModifiers로 변환
            _currentWaveMods = WaveModifierUtil.ToWaveModifiers(pattern.modifiers);

            Debug.Log($"[Wave {waveIndex}] Pattern={pattern.name} Modifiers={_currentWaveMods.label}");

            if (UIManager.Instance != null)
                UIManager.Instance.UpdateWave(waveIndex, maxWave, _currentWaveMods.label);

            if (MonsterSpawner.Instance != null)
                MonsterSpawner.Instance.SpawnPattern(pattern, _currentWaveMods);
            else
                Debug.LogWarning("MonsterSpawner.Instance is null. Add MonsterSpawner to scene.");

            MonsterAI.OnBossDied -= HandleBossDied;

            if (pattern.isBossWave)
            {
                MonsterAI.OnBossDied += HandleBossDied;
            }
            
            return;
        }

        // 패턴이 없으면 기존 랜덤 웨이브 유지 (기존 동작 보존)
        _currentWaveMods = WaveModifierRoller.Roll(0, 2);

        Debug.Log($"[Wave {waveIndex}] Modifiers = {_currentWaveMods.label} (speedMul={_currentWaveMods.speedMul}, hpMul={_currentWaveMods.hpMul}, shieldHp={_currentWaveMods.shieldHp})");

        if (UIManager.Instance != null)
            UIManager.Instance.UpdateWave(waveIndex, maxWave, _currentWaveMods.label);

        if (MonsterSpawner.Instance != null)
            MonsterSpawner.Instance.SpawnWave(waveIndex, _currentWaveMods);
        else
            Debug.LogWarning("MonsterSpawner.Instance is null. Add MonsterSpawner to scene.");
    }
    
    private void HandleBossDied()
    {
        MonsterAI.OnBossDied -= HandleBossDied;
        
        if (augmentSystem == null)
        {
            Debug.LogWarning("[GameManager] augmentSystem is null. NextWave immediately.");
            NextWave();
            return;
        }

        augmentSystem.BeginChoice(NextWave);
    }
    
    private void OnDestroy()
    {
        MonsterAI.OnBossDied -= HandleBossDied;
    }

    private WavePatternSO FindWavePattern(int waveIndex)
    {
        if (wavePatterns == null || wavePatterns.Length == 0)
            return null;

        for (int i = 0; i < wavePatterns.Length; i++)
        {
            var p = wavePatterns[i];
            if (p == null) continue;
            if (p.waveIndex == waveIndex)
                return p;
        }

        return null;
    }

}