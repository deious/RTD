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

    private void Awake()
    {
        if (tmp == null) tmp = GetComponentInChildren<TextMeshPro>();
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

            DamageTextManager.Instance.Release(this);
        }
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

        gameObject.SetActive(true);
    }

    public void SetOnRelease(System.Action onRelease) => _onRelease = onRelease;
}