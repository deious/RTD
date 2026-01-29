using UnityEngine;

public class AutoDestroyAfterPlay : MonoBehaviour
{
    [SerializeField] private float extraLife = 0.2f;

    private ParticleSystem _ps;

    private void Awake()
    {
        _ps = GetComponent<ParticleSystem>();
    }

    private void OnEnable()
    {
        if (_ps == null)
            _ps = GetComponent<ParticleSystem>();

        float life = 0.6f;
        if (_ps != null)
            life = _ps.main.duration + _ps.main.startLifetime.constantMax;

        Destroy(gameObject, life + Mathf.Max(0f, extraLife));
    }
}
