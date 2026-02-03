using UnityEngine;
using UnityEngine.InputSystem;
using RTD.Scripts.GamePlay.Wave;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;
using System.Threading;
using RTD.Scripts.Network;

public class GameRuntime : MonoBehaviour
{
    public static GameRuntime Instance { get; private set; }

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
    
    [Header("Scene")]
    [SerializeField] private string titleSceneName = "Title";
    [SerializeField] private string gameSceneName = "InGame";

    private bool gameOver;
    private CancellationTokenSource cts;
    
    private int gold;
    private int life;
    private int currentWave;
    private bool waveRunning;
    private bool waitingIntermission;
    private bool waitingAugment;
    private int waveAdvanceId;
    private bool nextWaveStartedForThisAdvance;
    
    private WaveModifiers _currentWaveMods;

    public float TowerDamageMul => (augmentSystem != null) ? augmentSystem.TowerDamageMul : 1f;
    public float TowerAttackSpeedMul => (augmentSystem != null) ? augmentSystem.TowerAttackSpeedMul : 1f;
    public float TowerRangeAdd => (augmentSystem != null) ? augmentSystem.TowerRangeAdd : 0f;

    public float EnemySpeedMul => (augmentSystem != null) ? augmentSystem.EnemySpeedMul : 1f;
    public float EnemyHpMul => (augmentSystem != null) ? augmentSystem.EnemyHpMul : 1f;
    // 외부에서 골드 읽을 수 있게 프로퍼티
    public int Gold => gold;
    public bool IsGameOver => gameOver;
    public int Life => life;
    public int CurrentWave => currentWave;

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
        
        cts = new CancellationTokenSource();
        gameOver = false;
    }

    private void Start()
    {
        PlayCameraIntro().Forget();
        MultiplayerContext.ResolveMyLaneIdFromNgo();
        
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
        if (gameOver)
            return;
        
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
            AddGold(1000);
        }

        /*if (keyboard.hKey.wasPressedThisFrame)
        {
            ChangeLife(-1);
        }

        if (keyboard.jKey.wasPressedThisFrame)
        {
            NextWave();
        }*/
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
        if (gameOver) 
            return;
        
        waveRunning = true;
        StartWave(currentWave);
    }
    
    private void StartWave(int waveIndex)
    {
        if (gameOver) return;

        //Debug.Log($"[WaveStart] waveIndex={waveIndex} | time={Time.time:F2}");
        WavePatternSO pattern = FindWavePattern(waveIndex);

        if (pattern == null)
        {
            Debug.LogError($"WavePattern not found for waveIndex={waveIndex}. (Option A: pattern-only)");
            EndGame(GameEndType.Lose); // 혹은 return; 원하는 정책
            return;
        }

        WaveModifiers currentWaveMods = WaveModifierUtil.ToWaveModifiers(pattern.modifiers);

        Debug.Log($"[Wave {waveIndex}] Pattern={pattern.name} Modifiers={currentWaveMods.label}");

        if (UIManager.Instance != null)
            UIManager.Instance.UpdateWave(waveIndex, maxWave, currentWaveMods.label);

        MonsterAI.OnBossDied -= HandleBossDied;
        if (pattern.isBossWave)
            MonsterAI.OnBossDied += HandleBossDied;

        if (MonsterSpawner.Instance != null)
            MonsterSpawner.Instance.SpawnPattern(pattern, currentWaveMods);
        else
            Debug.LogWarning("MonsterSpawner.Instance is null. Add MonsterSpawner to scene.");
    }
    
    private void HandleWaveCleared()
    {
        if (gameOver) 
            return;
        if (!waveRunning) 
            return;

        waveRunning = false;

        waveAdvanceId++;
        nextWaveStartedForThisAdvance = false;

        if (currentWave >= maxWave)
        {
            EndGame(GameEndType.Win);
            return;
        }

        WavePatternSO pattern = FindWavePattern(currentWave);
        bool isBossWave = (pattern != null && pattern.isBossWave);

        if (isBossWave)
        {
            waitingAugment = true;
            StartBossIntermission(waveAdvanceId);
            return;
        }

        AutoNextWaveAsync(waveAdvanceId).Forget();
    }
    
    private void StartBossIntermission(int advanceId)
    {
        if (waitingIntermission) return;

        waitingIntermission = true;

        RunIntermissionAsync(intermissionSeconds).ContinueWith(() =>
        {
            waitingIntermission = false;

            if (!gameOver && waitingAugment && augmentSystem != null && augmentSystem.IsChoosing)
            {
                augmentSystem.ForcePickRandomIfChoosing();
            }

            TryStartNextWave(advanceId);
        }).Forget();

        if (augmentSystem != null)
        {
            waitingAugment = true;

            augmentSystem.BeginChoice(() =>
            {
                waitingAugment = false;
                TryStartNextWave(advanceId);
            });
        }
        else
        {
            waitingAugment = false;
            TryStartNextWave(advanceId);
        }
    }
    
    private void TryStartNextWave(int advanceId)
    {
        if (gameOver) return;
        
        if (advanceId != waveAdvanceId) 
            return;
        
        if (nextWaveStartedForThisAdvance) 
            return;

        if (waitingAugment || waitingIntermission) 
            return;

        nextWaveStartedForThisAdvance = true;

        NextWave();
        waveRunning = true;
    }

    private async UniTaskVoid AutoNextWaveAsync(int advanceId)
    {
        if (gameOver) return;

        if (waitingIntermission) return;
        waitingIntermission = true;

        await RunIntermissionAsync(intermissionSeconds);

        waitingIntermission = false;

        TryStartNextWave(advanceId);
    }

    private async UniTask RunIntermissionAsync(float seconds)
    {
        try
        {
            float t = seconds;
            while (t > 0f)
            {
                if (UIManager.Instance != null)
                    UIManager.Instance.UpdateNextWaveTimer(Mathf.CeilToInt(t));

                await UniTask.Delay(1000, cancellationToken: cts.Token);
                t -= 1f;
            }
        }
        catch (System.OperationCanceledException)
        {
            return;
        }
        finally
        {
            if (UIManager.Instance != null)
                UIManager.Instance.UpdateNextWaveTimer(0);
        }
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
        
        cts?.Cancel();
        cts?.Dispose();
        cts = null;

        if (Instance == this)
            Instance = null;
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
    
    private void EndGame(GameEndType endType)
    {
        if (gameOver) return;
        gameOver = true;

        waveRunning = false;
        waitingIntermission = false;
        waitingAugment = false;
        
        cts.Cancel();
        
        if (orbitCamera != null)
            orbitCamera.SetInputLock(true);
        
        if (MonsterSpawner.Instance != null)
            MonsterSpawner.Instance.StopAllSpawning(destroyAlive: true);
        
        if (AppFlowManager.Instance != null)
            AppFlowManager.Instance.OnGameEnd(new GameResult(endType, currentWave));
    }
    
    private void HandleWaveMonsterCountChanged(int killed, int total)
    {
        if (UIManager.Instance != null)
            UIManager.Instance.UpdateWaveMonsterCount(killed, total);
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
        if (gameOver)
            return;

        life += amount;
        if (life < 0) life = 0;

        if (UIManager.Instance != null)
            UIManager.Instance.UpdateLife(life);

        if (life <= 0)
            EndGame(GameEndType.Lose);
    }

    public void NextWave()
    {
        currentWave++;
        if (currentWave > maxWave)
            currentWave = maxWave;

        StartWave(currentWave);
    }
}