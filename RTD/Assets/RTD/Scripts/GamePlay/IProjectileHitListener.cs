using UnityEngine;

public interface IProjectileHitListener
{
    void OnProjectileHit(MonsterAI primaryTarget, Vector3 hitPoint, int dealtDamage);
}
