using UnityEngine;

public class PooledTransformReset : MonoBehaviour, IPoolable
{
    [Header("Reset Options")]
    [SerializeField] private bool resetLocalScale = true;
    [SerializeField] private bool resetLocalRotation = false;
    [SerializeField] private bool resetLocalPosition = false;

    private Vector3 _defaultLocalScale;
    private Quaternion _defaultLocalRotation;
    private Vector3 _defaultLocalPosition;
    private bool _cached;

    private void Awake()
    {
        Cache();
    }

    private void Cache()
    {
        if (_cached) return;
        _cached = true;

        _defaultLocalScale = transform.localScale;
        _defaultLocalRotation = transform.localRotation;
        _defaultLocalPosition = transform.localPosition;
    }

    public void OnSpawned()
    {
        Cache();
        ResetNow();
    }

    public void OnDespawned()
    {
        Cache();
        ResetNow();
    }

    private void ResetNow()
    {
        if (resetLocalScale) transform.localScale = _defaultLocalScale;
        if (resetLocalRotation) transform.localRotation = _defaultLocalRotation;
        if (resetLocalPosition) transform.localPosition = _defaultLocalPosition;
    }
}