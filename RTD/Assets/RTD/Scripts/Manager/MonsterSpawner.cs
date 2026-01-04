using UnityEngine;
using Cysharp.Threading.Tasks;

public class MonsterSpawner : MonoBehaviour
{
    public static MonsterSpawner Instance { get; private set; }

    [SerializeField] private GameObject monsterPrefab;
    [SerializeField] private int spawnCountPerWave = 5;
    [SerializeField] private float spawnInterval = 0.6f;
    
    [Header("Wave Scaling")]
    [SerializeField] private int spawnAddPerWave = 1;
    [SerializeField] private int maxSpawnCount = 30;
    [SerializeField] private int hpAddPerWave = 2;  

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void SpawnWave(int waveIndex, WaveModifiers mods)
    {
        if (monsterPrefab == null)
        {
            Debug.LogError("MonsterSpawner: monsterPrefab is null");
            return;
        }
        
        int count = spawnCountPerWave + (waveIndex - 1) * spawnAddPerWave;
        if (count < 1) count = 1;
        if (count > maxSpawnCount) count = maxSpawnCount;

        for (int i = 0; i < count; i++)
        {
            float delay = i * spawnInterval;
            SpawnOneAfterAsync(delay, waveIndex, mods).Forget();
        }
    }

    private async UniTaskVoid SpawnOneAfterAsync(
        float delay,
        int waveIndex,
        WaveModifiers mods)
    {
        if (delay > 0f)
            await UniTask.Delay(
                System.TimeSpan.FromSeconds(delay),
                DelayType.DeltaTime,
                PlayerLoopTiming.Update,
                this.GetCancellationTokenOnDestroy());

        GameObject go = Instantiate(monsterPrefab);

        MonsterAI ai = go.GetComponent<MonsterAI>();
        if (ai != null)
        {
            ai.AddBaseHp(hpAddPerWave * (waveIndex - 1));
            ai.ApplyWaveModifiers(mods);
        }
    }
}