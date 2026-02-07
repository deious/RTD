using UnityEngine;

public class CannonTower : TowerBase
{
    [Header("Cannon Splash (Base Ability)")]
    [SerializeField] private float baseSplashRadius = 1.6f;
    [SerializeField, Range(0f, 1f)] private float baseSplashRatio = 0.6f;
    
    [Header("Audio")]
    [SerializeField] private AudioCue fireCue;
    [SerializeField] private AudioCue explosionCue;
    [SerializeField] private int towerTypeId = 2;

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
        
        Vector3 muzzlePos =
            FirePoint != null ? FirePoint.position : transform.position;

        //int laneId = MultiplayerContext.MyLaneId;
        int spamKey = (towerTypeId * 100000) + (MultiplayerContext.MyLaneId * 1000) + TowerViewId;

        if (fireCue != null)
            AudioManager.Instance?.PlayFire(fireCue, muzzlePos, spamKey);

        TryFireProjectile(target, Vector3.zero, radius, ratio, explosionCue);
    }
}