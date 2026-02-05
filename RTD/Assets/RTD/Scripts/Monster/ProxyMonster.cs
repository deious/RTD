using UnityEngine;

public class ProxyMonster : MonoBehaviour
{
    public int LaneId { get; private set; }
    public int NetId  { get; private set; }
    public int TypeId { get; private set; }

    public float HpMax { get; private set; }
    public float Hp    { get; private set; }
    public float ShieldHp { get; private set; }

    [Header("Move Smoothing")]
    [SerializeField] private float smoothSpeed = 15f;
    [SerializeField] private float stopSqrEpsilon = 0.0004f;

    private Vector3 _targetPos;
    private bool _hasTarget;

    private MonsterAI _ai;

    private void Awake()
    {
        _ai = GetComponent<MonsterAI>();
    }

    private void Update()
    {
        if (!_hasTarget) return;

        transform.position = Vector3.Lerp(transform.position, _targetPos, smoothSpeed * Time.deltaTime);

        if ((transform.position - _targetPos).sqrMagnitude <= stopSqrEpsilon)
        {
            transform.position = _targetPos;
            _hasTarget = false;
        }
    }

    public void Init(int laneId, int netId, int typeId, float hpMax, float hp, float shieldHp)
    {
        LaneId = laneId;
        NetId = netId;
        TypeId = typeId;

        _targetPos = transform.position;
        _hasTarget = false;

        SetVitals(hpMax, hp, shieldHp);
    }

    public void SetVitals(float hpMax, float hp, float shieldHp)
    {
        HpMax = Mathf.Max(1f, hpMax);
        Hp = Mathf.Clamp(hp, 0f, HpMax);
        ShieldHp = Mathf.Max(0f, shieldHp);
        
        if (_ai != null)
            _ai.SetShieldForProxy(ShieldHp);
    }

    public void Teleport(Vector3 pos)
    {
        transform.position = pos;
        _targetPos = pos;
        _hasTarget = false;
    }

    public void SmoothTo(Vector3 pos)
    {
        _targetPos = pos;
        _hasTarget = true;
    }
}