using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Unity.Collections;

public class TowerCombatBridge : NetworkBehaviour
{
    public static TowerCombatBridge Instance { get; private set; }

    [Header("Client Receiver")]
    [SerializeField] private RemoteTowerWorld remoteWorld;

    // ✅ 서버가 마지막 상태를 기억(관전 전환 시 즉시 Sync용)
    private readonly Dictionary<long, TowerSnapshot> _serverCache = new();

    private static long Key(int worldSlotId, int towerId)
        => ((long)worldSlotId << 32) | (uint)towerId;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (remoteWorld == null)
            remoteWorld = FindFirstObjectByType<RemoteTowerWorld>();
    }

    [Serializable]
    public struct TowerSnapshot : INetworkSerializable
    {
        public int worldSlotId;
        public int towerId;
        public FixedString64Bytes towerTypeId;
        public int gx, gy;
        public int level;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref worldSlotId);
            serializer.SerializeValue(ref towerId);
            serializer.SerializeValue(ref towerTypeId);
            serializer.SerializeValue(ref gx);
            serializer.SerializeValue(ref gy);
            serializer.SerializeValue(ref level);
        }
    }

    // --------------------
    // Client API
    // --------------------
    public void SendSpawnOrUpdate(TowerSnapshot snap)
    {
        if (IsServer) SpawnOrUpdateTowerClientRpc(snap);
        else SpawnOrUpdateTowerServerRpc(snap);
    }

    public void SendRemove(int worldSlotId, int towerId)
    {
        if (IsServer) RemoveTowerClientRpc(worldSlotId, towerId);
        else RemoveTowerServerRpc(worldSlotId, towerId);
    }

    // ✅ 관전 전환 시 “해당 lane 상태 전체”를 요청
    public void RequestSyncLane(int laneId)
    {
        if (!IsSpawned) return;

        if (IsServer)
        {
            // 호스트(서버)는 자기 자신에게도 보내야 UI가 즉시 맞음
            SendLaneCacheToClient(laneId, NetworkManager.LocalClientId);
        }
        else
        {
            RequestSyncLaneServerRpc(laneId);
        }
    }

    // --------------------
    // Server RPC
    // --------------------
    [ServerRpc(RequireOwnership = false)]
    private void SpawnOrUpdateTowerServerRpc(TowerSnapshot snap, ServerRpcParams rpcParams = default)
    {
        // ✅ 서버 캐시 갱신
        _serverCache[Key(snap.worldSlotId, snap.towerId)] = snap;

        SpawnOrUpdateTowerClientRpc(snap);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RemoveTowerServerRpc(int worldSlotId, int towerId, ServerRpcParams rpcParams = default)
    {
        // ✅ 서버 캐시 제거
        _serverCache.Remove(Key(worldSlotId, towerId));

        RemoveTowerClientRpc(worldSlotId, towerId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSyncLaneServerRpc(int laneId, ServerRpcParams rpcParams = default)
    {
        ulong requester = rpcParams.Receive.SenderClientId;
        SendLaneCacheToClient(laneId, requester);
    }

    private void SendLaneCacheToClient(int laneId, ulong clientId)
    {
        // laneId에 해당하는 스냅샷만 모아서 전송
        List<TowerSnapshot> list = new List<TowerSnapshot>(128);
        foreach (var kv in _serverCache)
        {
            var s = kv.Value;
            if (s.worldSlotId == laneId)
                list.Add(s);
        }

        var param = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
        };

        SyncTowersClientRpc(list.ToArray(), param);
    }

    // --------------------
    // Client RPC
    // --------------------
    [ClientRpc]
    private void SpawnOrUpdateTowerClientRpc(TowerSnapshot snap)
    {
        if (remoteWorld == null) return;
        remoteWorld.OnSpawnOrUpdateTower(snap);
    }

    [ClientRpc]
    private void RemoveTowerClientRpc(int worldSlotId, int towerId)
    {
        if (remoteWorld == null) return;
        remoteWorld.OnDespawnTower(worldSlotId, towerId);
    }

    // ✅ lane sync 결과 수신
    [ClientRpc]
    private void SyncTowersClientRpc(TowerSnapshot[] snaps, ClientRpcParams rpcParams = default)
    {
        if (remoteWorld == null) return;
        remoteWorld.OnSyncTowers(snaps);
    }
}
