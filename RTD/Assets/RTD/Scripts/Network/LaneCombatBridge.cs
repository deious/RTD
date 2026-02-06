using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class LaneCombatBridge : NetworkBehaviour
{
    public static LaneCombatBridge Instance { get; private set; }
    public static Func<int, byte[]> BuildPackedLaneSnapshot;
    private readonly Dictionary<int, byte[]> _lastPackedByLane = new Dictionary<int, byte[]>(4);
    
    public override void OnNetworkSpawn()
    {
        Instance = this;
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this)
            Instance = null;
        
        _lastPackedByLane.Clear();
    }

    [ServerRpc(RequireOwnership = false)]
    public void SpawnMonsterServerRpc(
        int laneId, int netId, int typeId, Vector3 pos, float hpMax, float hp, float shieldHp)
    {
        SpawnMonsterClientRpc(laneId, netId, typeId, pos, hpMax, hp, shieldHp);
    }

    [ClientRpc]
    private void SpawnMonsterClientRpc(
        int laneId, int netId, int typeId, Vector3 pos, float hpMax, float hp, float shieldHp)
    {
        if (laneId == MultiplayerContext.MyLaneId)
            return;

        RemoteLaneWorld.Instance?
            .OnRemoteSpawnMonster(laneId, netId, typeId, pos, hpMax, hp, shieldHp);
    }

    [ServerRpc(RequireOwnership = false)]
    public void DespawnMonsterServerRpc(int laneId, int netId)
    {
        DespawnMonsterClientRpc(laneId, netId);
    }

    [ClientRpc]
    private void DespawnMonsterClientRpc(int laneId, int netId)
    {
        if (laneId == MultiplayerContext.MyLaneId)
            return;

        RemoteLaneWorld.Instance?
            .OnRemoteDespawnMonster(laneId, netId);
    }

    // ===============================
    // 2️⃣ 실시간 Sync (tick)
    // ===============================

    [ServerRpc(RequireOwnership = false)]
    public void SyncMonstersServerRpc(int laneId, byte[] packedData)
    {
        if (IsServer && packedData != null && packedData.Length >= 4)
        {
            var copy = new byte[packedData.Length];
            Buffer.BlockCopy(packedData, 0, copy, 0, packedData.Length);
            _lastPackedByLane[laneId] = copy;
            
            SyncMonstersClientRpc(laneId, copy);
            return;
        }
        
        SyncMonstersClientRpc(laneId, packedData);
    }

    [ClientRpc]
    private void SyncMonstersClientRpc(int laneId, byte[] packedData)
    {
        if (laneId == MultiplayerContext.MyLaneId)
            return;

        RemoteLaneWorld.Instance?
            .OnRemoteSyncMonsters(laneId, packedData);
    }

    // ===============================
    // 3️⃣ 관전용 요청 Sync
    // ===============================

    public void RequestSyncLane(int laneId)
    {
        if (!IsClient) return;
        RequestSyncLaneServerRpc(laneId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSyncLaneServerRpc(int laneId)
    {
        if (!IsServer) return;
        
        if (_lastPackedByLane.TryGetValue(laneId, out var cached) && cached != null && cached.Length >= 4)
        {
            SyncMonstersClientRpc(laneId, cached);
            return;
        }
        
        byte[] packed = BuildPackedLaneSnapshot?.Invoke(laneId);

        if (packed != null && packed.Length >= 4)
        {
            var copy = new byte[packed.Length];
            Buffer.BlockCopy(packed, 0, copy, 0, packed.Length);
            _lastPackedByLane[laneId] = copy;

            SyncMonstersClientRpc(laneId, copy);
        }
    }
    
    [ClientRpc]
    private void TowerFireClientRpc(
        int laneId,
        int towerNetId,
        int targetMonsterNetId,
        Vector3 firePos,
        string towerTypeId
    )
    {
        if (laneId == MultiplayerContext.MyLaneId)
            return;

        RemoteLaneWorld.Instance?
            .OnRemoteTowerFire(laneId, towerNetId, targetMonsterNetId, firePos, towerTypeId);
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void NotifyTowerFireServerRpc(
        int laneId,
        int towerNetId,
        int targetMonsterNetId,
        Vector3 firePos,
        string towerTypeId,
        float splashRadius,
        float splashRatio,
        int traitType,
        float traitValue,
        float traitRange,
        float traitDuration,
        int traitCount,
        ServerRpcParams rpcParams = default)
    {
        NotifyTowerFireClientRpc(
            laneId, towerNetId, targetMonsterNetId, firePos, towerTypeId,
            splashRadius, splashRatio,
            traitType, traitValue, traitRange, traitDuration, traitCount);
    }

    [ClientRpc]
    private void NotifyTowerFireClientRpc(
        int laneId,
        int towerNetId,
        int targetMonsterNetId,
        Vector3 firePos,
        string towerTypeId,
        float splashRadius,
        float splashRatio,
        int traitType,
        float traitValue,
        float traitRange,
        float traitDuration,
        int traitCount)
    {
        if (RemoteLaneWorld.Instance == null)
            return;

        RemoteLaneWorld.Instance.OnRemoteTowerFire(
            laneId, towerNetId, targetMonsterNetId, firePos, towerTypeId,
            splashRadius, splashRatio,
            traitType, traitValue, traitRange, traitDuration, traitCount);
    }
}
