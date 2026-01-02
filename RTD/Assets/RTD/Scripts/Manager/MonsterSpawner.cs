using UnityEngine;

public class MonsterSpawner : MonoBehaviour
{
    public static MonsterSpawner Instance { get; private set; }

    [SerializeField] private GameObject monsterPrefab;
    [SerializeField] private int spawnCountPerWave = 5;
    [SerializeField] private float spawnInterval = 0.6f;

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
        
        for (int i = 0; i < spawnCountPerWave; i++)
        {
            float delay = i * spawnInterval;
            StartCoroutine(SpawnOneAfter(delay, mods));
        }
    }

    private System.Collections.IEnumerator SpawnOneAfter(float delay, WaveModifiers mods)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        GameObject go = Instantiate(monsterPrefab);
        MonsterAI ai = go.GetComponent<MonsterAI>();
        if (ai != null)
        {
            ai.ApplyWaveModifiers(mods);
        }
    }
}