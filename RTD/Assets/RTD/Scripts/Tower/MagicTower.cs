using UnityEngine;

public class MagicTower : TowerBase, IProjectileHitListener
{
    [Header("Magic DoT (Base)")]
    [SerializeField] private float baseDotDps = 6f;        // 초당 피해
    [SerializeField] private float baseDotDuration = 2.5f; // 지속시간
    
    [Header("Audio")]
    [SerializeField] private AudioCue fireCue;
    [SerializeField] private int towerTypeId = 3;

    protected override void Attack()
    {
        MonsterAI target = FindTarget();
        if (target == null) return;

        Vector3 muzzlePos = (FirePoint != null)
            ? FirePoint.position
            : (transform.position + Vector3.up * 0.7f);

        //int laneId = MultiplayerContext.MyLaneId;
        int spamKey = (towerTypeId * 100000) + (MultiplayerContext.MyLaneId * 1000) + TowerViewId;

        if (fireCue != null)
            AudioManager.Instance?.PlayFire(fireCue, muzzlePos, spamKey);
        
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