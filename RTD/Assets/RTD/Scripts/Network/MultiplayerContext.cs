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

    public static void ResolveMyLaneIdFromNgo()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            MyLaneId = 0;
            return;
        }

        ulong myId = nm.LocalClientId;
    
        var list = nm.ConnectedClientsList;
        System.Collections.Generic.List<ulong> ids = new();
        for (int i = 0; i < list.Count; i++) ids.Add(list[i].ClientId);
        ids.Sort();

        int idx = ids.IndexOf(myId);
        MyLaneId = Mathf.Clamp(idx < 0 ? 0 : idx, 0, 3);
    }
}