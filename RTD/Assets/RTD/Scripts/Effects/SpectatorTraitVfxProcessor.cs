using System.Collections.Generic;
using UnityEngine;

public static class SpectatorTraitVfxProcessor
{
    public static void Apply(
        RemoteLaneWorld world,
        int laneId,
        Vector3 hitPos,
        ProxyMonster directTarget,
        int traitType,
        float value,
        float range,
        float duration,
        int count)
    {
        if (world == null) return;
        if (CombatVFX.Instance == null) return;
        if (traitType < 0) return;
        
        var t = (TowerTraitType)traitType;

        switch (t)
        {
            case TowerTraitType.Chain:
                ApplyChainVfx(world, laneId, directTarget, hitPos, Mathf.Max(0.1f, range), Mathf.Max(0, count));
                break;

            case TowerTraitType.Shrapnel:
                ApplyShrapnelVfx(hitPos, Mathf.Max(0.1f, range), Mathf.Max(1, count));
                break;
        }
    }

    private static void ApplyChainVfx(RemoteLaneWorld world, int laneId, ProxyMonster firstTarget, Vector3 hitPos, float searchRadius, int jumps)
    {
        if (jumps <= 0) return;

        ProxyMonster current = firstTarget;
        Vector3 currentPos = (current != null) ? current.transform.position : hitPos;

        var hitSet = new HashSet<ProxyMonster>();
        if (current != null) hitSet.Add(current);

        for (int i = 0; i < jumps; i++)
        {
            var next = world.FindNearestProxyMonster(laneId, currentPos, searchRadius, hitSet);
            if (next == null) break;

            CombatVFX.Instance.PlayChain(currentPos, next.transform.position);

            hitSet.Add(next);
            current = next;
            currentPos = next.transform.position;
        }
    }

    private static void ApplyShrapnelVfx(Vector3 center, float radius, int shards)
    {
        for (int i = 0; i < shards; i++)
        {
            Vector2 rnd = Random.insideUnitCircle * (radius * 0.6f);
            Vector3 p = center + new Vector3(rnd.x, 0f, rnd.y);
            CombatVFX.Instance.PlayExplosion(p, radius * 0.5f);
        }
    }
}
