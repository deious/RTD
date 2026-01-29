using UnityEngine;

public class CannonTower : TowerBase
{
    [Header("Cannon Splash (Base Ability)")]
    [SerializeField] private float baseSplashRadius = 1.6f;
    [SerializeField, Range(0f, 1f)] private float baseSplashRatio = 0.6f;

    protected override void Attack()
    {
        MonsterAI target = FindTarget();
        if (target == null) return;

        float radius = baseSplashRadius;
        float ratio  = baseSplashRatio;
        
        if (RuntimeTrait != null && RuntimeTrait.type == TowerTraitType.Siege)
        {
            float radiusMul = Mathf.Max(1f, RuntimeTrait.range);
            float dmgMul = Mathf.Max(1f, RuntimeTrait.value);
            radius *= radiusMul;
            ratio = Mathf.Clamp01(ratio * dmgMul);
        }

        TryFireProjectile(target, radius, ratio);
    }
}