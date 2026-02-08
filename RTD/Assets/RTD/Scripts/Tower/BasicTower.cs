using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

public class BasicTower : TowerBase
{
    [Header("MultiShot Burst")]
    [SerializeField] private bool useAutoBurstInterval = true;
    [SerializeField] private float burstIntervalMin = 0.06f;
    [SerializeField] private float burstIntervalMax = 0.14f;
    [SerializeField] private float muzzleJitterRadius = 0.03f;
    
    [SerializeField] private float burstShotInterval = 0.06f;   // 수동으로 조절할 때
    
    [Header("Audio")]
    [SerializeField] private AudioCue fireCue;
    [SerializeField] private int towerTypeId = 1;

    private bool _isBursting;

    protected override void Attack()
    {
        if (_isBursting)
            return;

        MonsterAI target = FindTarget();
        if (target == null)
            return;

        int extraShots = 0;
        if (RuntimeTrait != null && RuntimeTrait.type == TowerTraitType.DoubleShot)
            extraShots = Mathf.Max(0, RuntimeTrait.count);

        int totalShots = 1 + extraShots;

        if (totalShots <= 1)
        {
            FireOnce(target);
            return;
        }

        BurstFireAsync(target, totalShots).Forget();
    }

    private float GetBurstInterval(int totalShots)
    {
        if (!useAutoBurstInterval)
            return Mathf.Max(0.001f, burstShotInterval);
        
        float ideal = attackInterval / Mathf.Max(2, totalShots);
        return Mathf.Clamp(ideal, burstIntervalMin, burstIntervalMax);
    }

    private Vector3 GetMuzzleJitter()
    {
        if (muzzleJitterRadius <= 0f) return Vector3.zero;

        Vector2 r = UnityEngine.Random.insideUnitCircle * muzzleJitterRadius;
        return new Vector3(r.x, 0f, r.y);
    }

    private void FireOnce(MonsterAI target)
    {
        Debug.Log($"[BasicTower] FireOnce called. fireCue={(fireCue!=null?fireCue.name:"null")}");
        if (target == null || target.IsEnded) return;
        
        Vector3 jitter = GetMuzzleJitter();

        Vector3 muzzlePos = (FirePoint  != null)
            ? (FirePoint .position + jitter)
            : (transform.position + Vector3.up * 0.7f + jitter);
        
        int laneId = MultiplayerContext.MyLaneId;
        int spamKey = (towerTypeId * 100000) + (MultiplayerContext.MyLaneId * 1000) + TowerViewId;
        
        if (fireCue != null)
            AudioManager.Instance?.PlayFire(fireCue, muzzlePos, spamKey);
        
        if (TryFireProjectile(target, jitter))
            return;

        // 투사체가 없는 타워도 연사 느낌은 간격으로 확보
        ApplyHitAndReturnDamage(target, damage);
    }

    private async UniTaskVoid BurstFireAsync(MonsterAI target, int totalShots)
    {
        _isBursting = true;

        CancellationToken ct = this.GetCancellationTokenOnDestroy();
        float interval = GetBurstInterval(totalShots);

        try
        {
            for (int i = 0; i < totalShots; i++)
            {
                if (target == null || target.IsEnded)
                    break;

                FireOnce(target);

                if (i < totalShots - 1)
                    await UniTask.Delay(TimeSpan.FromSeconds(interval), cancellationToken: ct);
            }
        }
        catch (OperationCanceledException)
        {
            // 타워가 파괴/비활성 등으로 취소되면 정상 종료
        }
        finally
        {
            _isBursting = false;
        }
    }
}
