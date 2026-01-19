using UnityEngine;
using UnityEngine.InputSystem;
using RTD.Scripts.GamePlay.Wave;
using Cysharp.Threading.Tasks;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private int startGold = 100;
    [SerializeField] private int startLife = 10;
    [SerializeField] private int startWave = 1;
    [SerializeField] private int maxWave = 20;
    [SerializeField] private float intermissionSeconds = 30f;
    
    [Header("Wave Patterns")]
    [SerializeField] private WavePatternSO[] wavePatterns;
    
    [Header("Systems")]
    [SerializeField] private AugmentSystem augmentSystem;
    [SerializeField] private OrbitCamera orbitCamera;
    [SerializeField] private GridManager grid;

    private int gold;
    private int life;
    private int currentWave;
    private bool _waveRunning;
    private bool _waitingIntermission;
    private bool _waitingAugment;
    
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
        PlayCameraIntro().Forget();
        
        if (orbitCamera != null && grid != null)
        {
            float cell = grid.cellSize;
            int w = grid.width;
            int h = grid.height;
            
            Vector3 topLeft = new Vector3(0f, 0f, 0f);
            
            float diag = Mathf.Sqrt(
                (w * cell) * (w * cell) +
                (h * cell) * (h * cell)
            );

            orbitCamera.SetInitialView(
                targetPos: topLeft,
                yaw: 225f,
                pitch: 55f,
                dist: diag * 0.9f
            );
        }
        
        UIManager.Instance.UpdateGold(gold);
        UIManager.Instance.UpdateLife(life);
        UIManager.Instance.UpdateWave(currentWave, maxWave);
        
        if (MonsterSpawner.Instance != null)
        {
            MonsterSpawner.Instance.OnWaveCleared += HandleWaveCleared;
            MonsterSpawner.Instance.OnWaveMonsterCountChanged += HandleWaveMonsterCountChanged;
        }
        
        StartWaveLoopAsync().Forget();
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
    
    private async UniTaskVoid PlayCameraIntro()
    {
        if (orbitCamera == null) return;

        Vector3 startTarget = new Vector3(8f, 0f, 6f);
        float startYaw = 45f;
        float startPitch = 45f;
        float startDist = 159f;

        Vector3 endTarget = new Vector3(75.99f, 0f, 42.16f);
        float endYaw = 0.40f;
        float endPitch = 66.38f;
        float endDist = 157.50f;

        await orbitCamera.PlayIntroToView(
            startTarget, startYaw, startPitch, startDist,
            endTarget,   endYaw,   endPitch,   endDist,
            duration: 0.8f
        );

        orbitCamera.SetInputLock(false);
    }

    private async UniTaskVoid StartWaveLoopAsync()
    {
        await RunIntermissionAsync(intermissionSeconds);
        BeginWave();
    }

    private void BeginWave()
    {
        _waveRunning = true;
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
    
    private void HandleWaveCleared()
    {
        if (!_waveRunning) return;
        _waveRunning = false;
        
        var pattern = FindWavePattern(currentWave);
        bool isBossWave = (pattern != null && pattern.isBossWave);

        if (isBossWave)
        {
            _waitingAugment = true;
            StartBossIntermission();
            return;
        }

        AutoNextWaveAsync().Forget();
    }
    
    private void StartBossIntermission()
    {
        if (_waitingIntermission) return;

        _waitingIntermission = true;
        
        RunIntermissionAsync(intermissionSeconds).ContinueWith(() =>
        {
            _waitingIntermission = false;
            TryStartNextWave();
        }).Forget();
        
        if (augmentSystem != null)
        {
            augmentSystem.BeginChoice(() =>
            {
                _waitingAugment = false;
                TryStartNextWave();
            });
        }
        else
        {
            _waitingAugment = false;
        }
    }

    private void TryStartNextWave()
    {
        if (_waitingAugment || _waitingIntermission)
            return;

        NextWave();
        _waveRunning = true;
    }

    private async UniTaskVoid AutoNextWaveAsync()
    {
        if (_waitingIntermission) return;
        _waitingIntermission = true;

        await RunIntermissionAsync(intermissionSeconds);

        _waitingIntermission = false;

        NextWave();
        _waveRunning = true;
    }

    private async UniTask RunIntermissionAsync(float seconds)
    {
        float t = seconds;
        while (t > 0f)
        {
            if (UIManager.Instance != null)
                UIManager.Instance.UpdateNextWaveTimer(Mathf.CeilToInt(t));

            await UniTask.Delay(1000);
            t -= 1f;
        }

        if (UIManager.Instance != null)
            UIManager.Instance.UpdateNextWaveTimer(0);
    }
    
    private void HandleBossDied()
    {
        MonsterAI.OnBossDied -= HandleBossDied;
    }
    
    private void OnDestroy()
    {
        MonsterAI.OnBossDied -= HandleBossDied;

        if (MonsterSpawner.Instance != null)
        {
            MonsterSpawner.Instance.OnWaveCleared -= HandleWaveCleared;
            MonsterSpawner.Instance.OnWaveMonsterCountChanged -= HandleWaveMonsterCountChanged;
        }
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
    
    private void HandleWaveMonsterCountChanged(int alive, int total)
    {
        if (UIManager.Instance != null)
            UIManager.Instance.UpdateWaveMonsterCount(alive, total);
    }

    public bool TrySpendGold(int amount)
    {
        if (amount <= 0) return true;

        if (gold < amount)
            return false;

        gold -= amount;
        UIManager.Instance.UpdateGold(gold);
        return true;
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

        StartWave(currentWave);
    }
}