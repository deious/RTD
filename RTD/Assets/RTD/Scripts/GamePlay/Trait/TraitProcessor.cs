using System.Collections.Generic;
using UnityEngine;

public static class TraitProcessor
{
    public static LayerMask MonsterLayerMask = ~0;

    public static int ModifyDamage(TowerTraitSO trait, int baseDamage)
    {
        if (trait == null) return baseDamage;

        switch (trait.type)
        {
            case TowerTraitType.Critical:
            {
                float chance = Mathf.Clamp01(trait.value);
                float mul = Mathf.Max(1f, trait.range);

                if (Random.value < chance)
                    return Mathf.RoundToInt(baseDamage * mul);

                return baseDamage;
            }
        }

        return baseDamage;
    }

    public static void ApplyOnHit(TowerTraitSO trait, TowerBase source, MonsterAI target, int hitDamage)
    {
        if (trait == null || target == null) return;

        switch (trait.type)
        {
            // Common
            case TowerTraitType.Chain:
                ApplyChain(trait, target, hitDamage);
                break;
            
            case TowerTraitType.Execute:
                ApplyExecute(trait, target);
                break;

            // Magic
            case TowerTraitType.Slow:
                target.ApplySlow(Mathf.Clamp01(trait.value), Mathf.Max(0.1f, trait.duration));
                break;

            case TowerTraitType.Burn:
                target.ApplyBurn(hitDamage, Mathf.Clamp01(trait.value), Mathf.Max(0.1f, trait.duration), Mathf.Max(1, trait.count));
                break;

            case TowerTraitType.Curse:
                target.ApplyCurse(Mathf.Clamp01(trait.value), Mathf.Max(0.1f, trait.duration));
                break;

            // Cannon
            case TowerTraitType.Stun:
                if (Random.value < Mathf.Clamp01(trait.value))
                    target.ApplyStun(Mathf.Max(0.05f, trait.duration));
                break;

            case TowerTraitType.Shrapnel:
                ApplyShrapnel(trait, source, target.transform.position, hitDamage);
                break;
        }
    }
    
    public static void ApplySplashDamage(TowerBase source, Vector3 center, float radius, MonsterAI directTarget, int splashDamage)
    {
        Collider[] cols = Physics.OverlapSphere(center, radius, MonsterLayerMask);
        for (int i = 0; i < cols.Length; i++)
        {
            MonsterAI m = cols[i].GetComponentInParent<MonsterAI>();
            if (m == null) continue;
            if (m == directTarget) continue;
            
            m.TakeDamage(splashDamage);
        }
    }

    private static void ApplyChain(TowerTraitSO trait, MonsterAI firstTarget, int hitDamage)
    {
        float searchRadius = Mathf.Max(0.1f, trait.range);
        int jumps = Mathf.Max(0, trait.count);
        float dmgRatio = Mathf.Clamp01(trait.value);

        if (jumps <= 0 || dmgRatio <= 0f) return;

        int chainDamage = Mathf.Max(1, Mathf.RoundToInt(hitDamage * dmgRatio));

        HashSet<MonsterAI> hitSet = new HashSet<MonsterAI>();
        hitSet.Add(firstTarget);

        MonsterAI current = firstTarget;

        for (int i = 0; i < jumps; i++)
        {
            MonsterAI next = FindNearestMonster(current.transform.position, searchRadius, hitSet);
            if (next == null) break;

            hitSet.Add(next);

            if (CombatVFX.Instance != null)
                CombatVFX.Instance.PlayChain(current.transform.position, next.transform.position);

            next.TakeDamage(chainDamage);
            current = next;
        }
    }
    
    private static void ApplyExecute(TowerTraitSO trait, MonsterAI target)
    {
        float hpThreshold = Mathf.Clamp01(trait.range);
        float bossMul = Mathf.Max(1f, trait.duration);
        
        if (target.Hp01 > hpThreshold)
            return;
        
        if (target.ImmuneExecute)
        {
            int extraDamage = Mathf.Max(
                1,
                Mathf.RoundToInt(target.CurrentHp * (bossMul - 1f))
            );
            target.TakeDamage(extraDamage);
            return;
        }
        
        target.TakeDamage(target.CurrentHp);
    }

    private static void ApplyShrapnel(TowerTraitSO trait, TowerBase source, Vector3 center, int hitDamage)
    {
        int shards = Mathf.Max(1, trait.count);
        float radius = Mathf.Max(0.1f, trait.range);
        float ratio = Mathf.Clamp01(trait.value);

        if (ratio <= 0f) return;

        int shardDamage = Mathf.Max(1, Mathf.RoundToInt(hitDamage * ratio));

        // “파편”은 주변에 작은 추가 폭발을 여러 번 발생시키는 느낌
        for (int i = 0; i < shards; i++)
        {
            Vector2 rnd = Random.insideUnitCircle * (radius * 0.6f);
            Vector3 p = center + new Vector3(rnd.x, 0f, rnd.y);

            if (CombatVFX.Instance != null)
                CombatVFX.Instance.PlayExplosion(p, radius * 0.5f);

            Collider[] cols = Physics.OverlapSphere(p, radius * 0.5f, MonsterLayerMask);
            for (int c = 0; c < cols.Length; c++)
            {
                MonsterAI m = cols[c].GetComponentInParent<MonsterAI>();
                if (m == null) continue;
                m.TakeDamage(shardDamage);
            }
        }
    }

    private static MonsterAI FindNearestMonster(Vector3 center, float radius, HashSet<MonsterAI> exclude)
    {
        Collider[] cols = Physics.OverlapSphere(center, radius, MonsterLayerMask);

        MonsterAI best = null;
        float bestDistSq = float.PositiveInfinity;

        for (int i = 0; i < cols.Length; i++)
        {
            MonsterAI m = cols[i].GetComponentInParent<MonsterAI>();
            if (m == null) continue;
            if (exclude != null && exclude.Contains(m)) continue;

            float distSq = (m.transform.position - center).sqrMagnitude;
            if (distSq < bestDistSq)
            {
                bestDistSq = distSq;
                best = m;
            }
        }

        return best;
    }
}
