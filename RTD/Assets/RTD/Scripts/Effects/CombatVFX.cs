using UnityEngine;

public class CombatVFX : MonoBehaviour
{
    public static CombatVFX Instance { get; private set; }

    [Header("Prefabs (Cartoon FX)")]
    [SerializeField] private GameObject chainPrefab;      // 번개 프리팹(라인/빔/라이트닝)
    [SerializeField] private GameObject explosionPrefab;  // 폭발 프리팹

    [Header("Tuning")]
    [SerializeField] private float defaultLifeTime = 2.5f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void PlayChain(Vector3 from, Vector3 to)
    {
        if (chainPrefab == null) return;
        
        Vector3 mid = (from + to) * 0.5f;
        var go = Instantiate(chainPrefab, mid, Quaternion.identity);

        Vector3 dir = (to - from);
        if (dir.sqrMagnitude > 0.0001f)
            go.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        
        Destroy(go, defaultLifeTime);
    }

    public void PlayExplosion(Vector3 pos, float radius)
    {
        if (explosionPrefab == null) return;

        var go = Instantiate(explosionPrefab, pos, Quaternion.identity);
        
        float scale = Mathf.Max(0.5f, radius);
        go.transform.localScale = Vector3.one * scale;

        Destroy(go, defaultLifeTime);
    }
}