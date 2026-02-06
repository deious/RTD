using TMPro;
using UnityEngine;

public class DamageText : MonoBehaviour
{
    [SerializeField] private TextMeshPro tmp;
    [SerializeField] private float floatSpeed = 1.5f;
    [SerializeField] private float lifeTime = 0.8f;

    private float _t;
    private Vector3 _vel;
    private System.Action _onRelease;

    private Vector3 _initialScale;

    private void Awake()
    {
        if (tmp == null)
            tmp = GetComponentInChildren<TextMeshPro>();

        _initialScale = transform.localScale;
    }

    private void OnEnable()
    {
        _t = 0f;
        _vel = Vector3.zero;
        transform.localScale = _initialScale;

        if (tmp != null)
            tmp.alpha = 1f;
    }

    private void Update()
    {
        _t += Time.deltaTime;
        transform.position += _vel * Time.deltaTime;

        if (tmp != null)
            tmp.alpha = Mathf.Clamp01(1f - (_t / lifeTime));

        if (_t >= lifeTime)
        {
            _onRelease?.Invoke();
            _onRelease = null;

            if (DamageTextManager.Instance != null)
                DamageTextManager.Instance.Release(this);
        }
    }

    public void SetScaleMul(float mul)
    {
        mul = Mathf.Max(0.01f, mul);
        transform.localScale = _initialScale * mul;
    }

    public void Play(int damage, Vector3 worldPos)
    {
        transform.position = worldPos;

        _t = 0f;
        _vel = Vector3.up * floatSpeed;

        if (tmp != null)
        {
            tmp.text = damage.ToString();
            tmp.alpha = 1f;
        }
    }

    public void SetOnRelease(System.Action onRelease) => _onRelease = onRelease;
}