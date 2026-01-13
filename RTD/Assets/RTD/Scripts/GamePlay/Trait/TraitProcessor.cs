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
                // value=확률(0~1), range=배율(>=1)
                float chance = Mathf.Clamp01(trait.value);
                float mul = Mathf.Max(1f, trait.range);

                if (Random.value < chance)
                    return Mathf.RoundToInt(baseDamage * mul);

                return baseDamage;
            }
            default:
                return baseDamage;
        }
    }
    
    public static void ApplyOnHit(TowerTraitSO trait, TowerBase source, MonsterAI target, int hitDamage)
    {
        if (trait == null || target == null) return;

        switch (trait.type)
        {
            case TowerTraitType.Slow:
                ApplySlow(trait, target);
                break;

            case TowerTraitType.Chain:
                ApplyChain(trait, target, hitDamage);
                break;

            case TowerTraitType.Explosion:
                ApplyExplosion(trait, target.transform.position, target, hitDamage);
                break;
        }
    }

    private static void ApplySlow(TowerTraitSO trait, MonsterAI target)
    {
        // value=둔화율(0.3이면 30% 느려짐), duration=지속시간
        float slowRate = Mathf.Clamp01(trait.value);
        float dur = Mathf.Max(0.1f, trait.duration);
        target.ApplySlow(slowRate, dur);
    }

    private static void ApplyChain(TowerTraitSO trait, MonsterAI firstTarget, int hitDamage)
    {
        // range = 다음 타겟 탐색 반경
        // count = 추가로 맞출 타겟 수
        // value = 연쇄 데미지 비율(원 데미지 대비)
        float searchRadius = Mathf.Max(0.1f, trait.range);
        int jumps = Mathf.Max(0, trait.count);
        float dmgRatio = Mathf.Clamp01(trait.value);

        if (jumps <= 0 || dmgRatio <= 0f) return;

        int chainDamage = Mathf.Max(1, Mathf.RoundToInt(hitDamage * dmgRatio));

        // 이미 맞은 대상은 다시 맞지 않도록
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

    private static void ApplyExplosion(TowerTraitSO trait, Vector3 center, MonsterAI directTarget, int hitDamage)
    {
        // range = 폭발 반경
        // value = 폭발 데미지 비율
        float radius = Mathf.Max(0.1f, trait.range);
        float dmgRatio = Mathf.Clamp01(trait.value);

        if (dmgRatio <= 0f) return;

        int splashDamage = Mathf.Max(1, Mathf.RoundToInt(hitDamage * dmgRatio));
        
        if (CombatVFX.Instance != null)
            CombatVFX.Instance.PlayExplosion(center, radius);

        Collider[] cols = Physics.OverlapSphere(center, radius, MonsterLayerMask);

        for (int i = 0; i < cols.Length; i++)
        {
            MonsterAI m = cols[i].GetComponentInParent<MonsterAI>();
            if (m == null) continue;
            
            if (m == directTarget) continue;

            m.TakeDamage(splashDamage);
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
