using UnityEngine;
using Cysharp.Threading.Tasks;

public class CameraShaker : MonoBehaviour
{
    public static CameraShaker Instance { get; private set; }

    [SerializeField] private OrbitCamera orbitCamera;

    private int _shakeToken;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (orbitCamera == null)
            orbitCamera = GetComponent<OrbitCamera>();
    }

    public void Shake(float duration, float strength)
    {
        _shakeToken++;
        int token = _shakeToken;

        ShakeAsync(duration, strength, token).Forget();
    }

    private async UniTaskVoid ShakeAsync(float duration, float strength, int token)
    {
        if (orbitCamera == null)
        {
            Debug.LogWarning("[CameraShaker] OrbitCamera reference is null.");
            return;
        }

        float t = 0f;
        
        var ct = this.GetCancellationTokenOnDestroy();

        while (t < duration)
        {
            if (token != _shakeToken)
                return;

            t += Time.deltaTime;

            float x = Random.Range(-1f, 1f) * strength;
            float y = Random.Range(-1f, 1f) * strength;

            orbitCamera.AddPositionOffset(new Vector3(x, y, 0f));

            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, ct);
        }
    }
}