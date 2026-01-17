using UnityEngine;

public class BasicTower : TowerBase
{
    protected override void Attack()
    {
        MonsterAI target = FindTarget();
        if (target == null) return;
        
        if (TryFireProjectile(target))
        {
            if (RuntimeTrait != null && RuntimeTrait.type == TowerTraitType.DoubleShot)
            {
                float chance = Mathf.Clamp01(RuntimeTrait.value);
                if (Random.value < chance)
                    TryFireProjectile(target);
            }
            return;
        }
        
        ApplyHitAndReturnDamage(target, damage);

        if (RuntimeTrait != null && RuntimeTrait.type == TowerTraitType.DoubleShot)
        {
            float chance = Mathf.Clamp01(RuntimeTrait.value);
            if (Random.value < chance)
                ApplyHitAndReturnDamage(target, damage);
        }
    }
}