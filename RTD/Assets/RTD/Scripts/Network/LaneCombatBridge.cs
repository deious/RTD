using System;
using Unity.Netcode;
using UnityEngine;

public class LaneCombatBridge : NetworkBehaviour
{
    public static LaneCombatBridge Instance { get; private set; }
    public static Func<int, byte[]> BuildPackedLaneSnapshot;

    private void Awake()
    {
        Instance = this;
    }

    [ServerRpc(RequireOwnership = false)]
    public void SpawnMonsterServerRpc(
        int laneId, int netId, int typeId, Vector3 pos, int hpMax, int hp)
    {
        SpawnMonsterClientRpc(laneId, netId, typeId, pos, hpMax, hp);
    }

    [ClientRpc]
    private void SpawnMonsterClientRpc(
        int laneId, int netId, int typeId, Vector3 pos, int hpMax, int hp)
    {
        if (laneId == MultiplayerContext.MyLaneId)
            return;

        RemoteLaneWorld.Instance?
            .OnRemoteSpawnMonster(laneId, netId, typeId, pos, hpMax, hp);
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

        byte[] packed = BuildPackedLaneSnapshot?.Invoke(laneId);

        // 안전 처리
        if (packed == null || packed.Length < 4)
            packed = BitConverter.GetBytes(0);

        SyncMonstersClientRpc(laneId, packed);
    }
}
