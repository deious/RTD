using UnityEngine;

public class BasicTower : TowerBase
{
    // 기본 타워는 단일 타겟에게 데미지를 주는 형태
    protected override void Attack()
    {
        MonsterAI target = FindTarget();
        
        if (target != null)
        {
            target.TakeDamage(damage);
        }
    }
}