using UnityEngine;

public static class SpectatorCleanup
{
    public static void ClearMyLaneAll()
    {
        int myLane = MultiplayerContext.MyLaneId;
        
        var monsters = Object.FindObjectsByType<MonsterAI>(FindObjectsSortMode.None);
        foreach (var m in monsters)
        {
            if (m == null) continue;
            
            if (m.GetComponent<ProxyMonster>() != null) continue;

            if (m.WorldSlotId != myLane) continue;

            if (SimplePool.Instance != null) SimplePool.Instance.Release(m.gameObject);
            else Object.Destroy(m.gameObject);
        }
        
        var projectiles = Object.FindObjectsByType<Projectile>(FindObjectsSortMode.None);
        foreach (var p in projectiles)
        {
            if (p == null) continue;
            Object.Destroy(p.gameObject);
        }

        var towers = Object.FindObjectsByType<TowerBase>(FindObjectsSortMode.None);
        foreach (var t in towers)
        {
            if (t == null) continue;

            var own = t.GetComponent<TowerOwnership>();
            if (own == null) continue;
            if (own.ownerLane != myLane) continue;

            t.SetTile(null);
            Object.Destroy(t.gameObject);
        }
    }
}