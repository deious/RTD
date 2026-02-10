using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

public static class MultiplayerContext
{
    public static int PlayersCount { get; private set; } = 1;
    public static int MyLaneId { get; private set; } = 0;

    public static void SetPlayersCount(int count)
    {
        PlayersCount = Mathf.Clamp(count, 1, 4);
    }
    
    public static void SyncFromSessionState()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening)
        {
            PlayersCount = 1;
            MyLaneId = 0;
            return;
        }
        
        int connected = (nm.ConnectedClientsList != null) ? nm.ConnectedClientsList.Count : 1;
        PlayersCount = Mathf.Clamp(connected, 1, 4);
        
        ulong myId = nm.LocalClientId;

        var list = nm.ConnectedClientsList;
        var ids = new List<ulong>(list.Count);
        for (int i = 0; i < list.Count; i++)
            ids.Add(list[i].ClientId);

        ids.Sort();

        int idx = ids.IndexOf(myId);
        MyLaneId = Mathf.Clamp(idx < 0 ? 0 : idx, 0, 3);
    }

    public static async UniTask ResolveMyLaneIdFromNgoAsync(
        int expectedPlayers,
        float timeoutSec,
        CancellationToken ct)
    {
        expectedPlayers = Mathf.Clamp(expectedPlayers, 1, 4);
        timeoutSec = Mathf.Max(0.1f, timeoutSec);

        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening)
        {
            PlayersCount = 1;
            MyLaneId = 0;
            return;
        }

        float end = Time.realtimeSinceStartup + timeoutSec;

        while (Time.realtimeSinceStartup < end)
        {
            ct.ThrowIfCancellationRequested();

            int connected = nm.ConnectedClientsList != null ? nm.ConnectedClientsList.Count : 0;
            if (connected >= expectedPlayers)
                break;

            await UniTask.Delay(100, ignoreTimeScale: true, cancellationToken: ct);
        }
        
        SyncFromSessionState();

        Debug.Log($"[MultiplayerContext] Resolved(Async). players={PlayersCount} myLane={MyLaneId} localClientId={nm.LocalClientId}");
    }
}
