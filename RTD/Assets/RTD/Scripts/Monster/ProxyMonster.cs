using UnityEngine;

public class ProxyMonster : MonoBehaviour
{
    public int LaneId { get; private set; }
    public int NetId  { get; private set; }
    public int TypeId { get; private set; }

    private int _hpMax;
    private int _hp;

    private Vector3 _targetPos;
    private bool _hasTarget;

    public void Init(int laneId, int netId, int typeId, int hpMax, int hp)
    {
        LaneId = laneId;
        NetId = netId;
        TypeId = typeId;
        SetHP(hpMax, hp);
        _targetPos = transform.position;
        _hasTarget = false;
    }

    public void SetHP(int hpMax, int hp)
    {
        _hpMax = Mathf.Max(1, hpMax);
        _hp = Mathf.Clamp(hp, 0, _hpMax);
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

    private void Update()
    {
        if (!_hasTarget) return;
        transform.position = Vector3.Lerp(transform.position, _targetPos, 15f * Time.deltaTime);
    }
}