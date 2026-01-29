using UnityEngine;

public class MagicTower : TowerBase, IProjectileHitListener
{
    [Header("Magic DoT (Base)")]
    [SerializeField] private float baseDotDps = 6f;        // 초당 피해
    [SerializeField] private float baseDotDuration = 2.5f; // 지속시간

    protected override void Attack()
    {
        MonsterAI target = FindTarget();
        if (target == null)
            return;
        
        TryFireProjectile(target);
    }
    
    public void OnProjectileHit(MonsterAI target, Vector3 hitPos, int damageDealt)
    {
        if (target == null) return;

        float dps = baseDotDps;

        if (GameRuntime.Instance != null)
            dps *= GameRuntime.Instance.TowerDamageMul;

        target.ApplyDot(dps, baseDotDuration);
    }
}