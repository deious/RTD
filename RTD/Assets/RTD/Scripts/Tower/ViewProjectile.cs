using UnityEngine;

public class ViewProjectile : MonoBehaviour
{
    [SerializeField] private float homingTurnSpeed = 25f;

    private Transform _target;
    private float _speed;
    private float _lifeTime;
    private float _timer;

    private Vector3 _lastTargetPos;
    private bool _hasTarget;

    public void Init(Transform target, float speed, float lifeTime)
    {
        _target = target;
        _speed = Mathf.Max(0.01f, speed);
        _lifeTime = Mathf.Max(0.05f, lifeTime);
        _timer = 0f;

        _hasTarget = (_target != null);
        if (_hasTarget) _lastTargetPos = _target.position;
    }

    private void OnEnable()
    {
        _timer = 0f;
    }

    private void Update()
    {
        _timer += Time.deltaTime;
        if (_timer >= _lifeTime)
        {
            Destroy(gameObject);
            return;
        }

        // 타겟이 사라지면 마지막 위치로 날아가다 소멸
        if (_target != null) _lastTargetPos = _target.position;

        Vector3 to = _lastTargetPos - transform.position;
        float dist = _speed * Time.deltaTime;

        if (to.sqrMagnitude <= dist * dist)
        {
            transform.position = _lastTargetPos;
            Destroy(gameObject);
            return;
        }

        Vector3 dir = to.normalized;
        transform.position += dir * dist;

        // 살짝 호밍 느낌
        if (dir.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, homingTurnSpeed * Time.deltaTime);
        }
    }
}
