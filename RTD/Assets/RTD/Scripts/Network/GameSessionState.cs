using Unity.Netcode;
using UnityEngine;

public class GameSessionState : NetworkBehaviour
{
    public static GameSessionState Instance { get; private set; }

    public NetworkVariable<int> PlayersCount = new(
        1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public NetworkVariable<int> MyLaneId = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private void Awake()
    {
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            var list = NetworkManager.Singleton.ConnectedClientsList;
            PlayersCount.Value = Mathf.Clamp(list.Count, 1, 4);

            ResolveAllLaneIdsServer();
        }
    }

    private void ResolveAllLaneIdsServer()
    {
        var list = NetworkManager.Singleton.ConnectedClientsList;
        var ids = new System.Collections.Generic.List<ulong>();
        foreach (var c in list) ids.Add(c.ClientId);
        ids.Sort();

        for (int i = 0; i < ids.Count; i++)
        {
            if (ids[i] == NetworkManager.Singleton.LocalClientId)
            {
                MyLaneId.Value = i;
                break;
            }
        }
    }
}