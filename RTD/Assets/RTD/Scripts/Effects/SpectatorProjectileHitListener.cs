using System.Collections.Generic;
using UnityEngine;

public class SpectatorProjectileHitListener : MonoBehaviour, IProjectileHitListener
{
    private RemoteLaneWorld _world;

    private int _laneId;
    private float _splashRadius;
    private float _splashRatio;

    private int _traitType;
    private float _traitValue;
    private float _traitRange;
    private float _traitDuration;
    private int _traitCount;
    
    private readonly HashSet<ProxyMonster> _chainHit = new();

    public void BindWorld(RemoteLaneWorld world) => _world = world;

    public void Configure(
        int laneId,
        float splashRadius,
        float splashRatio,
        int traitType,
        float traitValue,
        float traitRange,
        float traitDuration,
        int traitCount)
    {
        _laneId = laneId;

        _splashRadius = Mathf.Max(0f, splashRadius);
        _splashRatio = Mathf.Clamp01(splashRatio);

        _traitType = traitType;
        _traitValue = traitValue;
        _traitRange = traitRange;
        _traitDuration = traitDuration;
        _traitCount = traitCount;

        _chainHit.Clear();
    }

    public void OnProjectileHit(MonsterAI target, Vector3 hitPos, int dealtDamage)
    {
        if (_splashRadius > 0.01f)
        {
            if (CombatVFX.Instance != null)
                CombatVFX.Instance.PlayExplosion(hitPos, _splashRadius);
        }
        
        if (!System.Enum.IsDefined(typeof(TowerTraitType), _traitType))
            return;

        var tt = (TowerTraitType)_traitType;

        switch (tt)
        {
            case TowerTraitType.Chain:
                PlayChainVfx(hitPos);
                break;

            case TowerTraitType.Shrapnel:
                PlayShrapnelVfx(hitPos);
                break;

            // Stun/Burn/Slow/Curse/Execute 등은 “시각효과를 따로 만들 때”만 여기 확장
            default:
                break;
        }
    }

    private void PlayChainVfx(Vector3 firstHitPos)
    {
        if (_world == null) return;

        float searchRadius = Mathf.Max(0.1f, _traitRange);
        int jumps = Mathf.Max(0, _traitCount);
        if (jumps <= 0) return;
        
        ProxyMonster current = _world.FindNearestProxyMonster(_laneId, firstHitPos, searchRadius, null);
        if (current == null) return;

        _chainHit.Add(current);

        Vector3 from = firstHitPos;
        ProxyMonster cur = current;

        for (int i = 0; i < jumps; i++)
        {
            ProxyMonster next = _world.FindNearestProxyMonster(_laneId, cur.transform.position, searchRadius, _chainHit);
            if (next == null) break;

            _chainHit.Add(next);

            if (CombatVFX.Instance != null)
                CombatVFX.Instance.PlayChain(from, next.transform.position);

            from = next.transform.position;
            cur = next;
        }
    }

    private void PlayShrapnelVfx(Vector3 hitPos)
    {
        int shards = Mathf.Max(1, _traitCount);
        float radius = Mathf.Max(0.1f, _traitRange);

        for (int i = 0; i < shards; i++)
        {
            Vector2 rnd = Random.insideUnitCircle * (radius * 0.6f);
            Vector3 p = hitPos + new Vector3(rnd.x, 0f, rnd.y);

            if (CombatVFX.Instance != null)
                CombatVFX.Instance.PlayExplosion(p, radius * 0.5f);
        }
    }
}
