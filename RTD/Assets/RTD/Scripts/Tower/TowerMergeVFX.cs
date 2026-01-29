using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class TowerMergeVFX : MonoBehaviour
{
    public static TowerMergeVFX Instance { get; private set; }

    [Header("Timing")]
    [SerializeField] private float mergeDuration = 0.22f;

    [Header("Motion")]
    [SerializeField] private AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Header("FX")]
    [SerializeField] private ParticleSystem mergeBurstPrefab;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // sources: 합성에 쓰이는 3개 타워(Transform)
    // mergePoint: 빨려 들어갈 위치(대개 중앙 타일 or 결과 타워 생성 위치)
    // afterVfx: 연출이 끝난 직후 실행할 실제 합성 로직(3개 제거 + 결과 생성 등)
    public async UniTask PlayMergeAsync(Transform[] sources, Vector3 mergePoint, Action afterVfx)
    {
        if (sources == null || sources.Length == 0)
        {
            afterVfx?.Invoke();
            return;
        }

        float dur = Mathf.Max(0.05f, mergeDuration);
        
        int n = sources.Length;
        Vector3[] startPos = new Vector3[n];
        Vector3[] startScale = new Vector3[n];

        for (int i = 0; i < n; i++)
        {
            if (sources[i] == null) continue;
            startPos[i] = sources[i].position;
            startScale[i] = sources[i].localScale;
        }

        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / dur);

            float mu = (moveCurve != null) ? moveCurve.Evaluate(u) : u;
            float su = (scaleCurve != null) ? scaleCurve.Evaluate(u) : (1f - u);

            for (int i = 0; i < n; i++)
            {
                Transform tr = sources[i];
                if (tr == null) continue;

                tr.position = Vector3.Lerp(startPos[i], mergePoint, mu);
                tr.localScale = startScale[i] * su;
            }

            await UniTask.Yield(PlayerLoopTiming.Update);
        }
        
        afterVfx?.Invoke();
        
        if (mergeBurstPrefab != null)
        {
            Instantiate(mergeBurstPrefab, mergePoint, Quaternion.identity);
        }
    }
}
