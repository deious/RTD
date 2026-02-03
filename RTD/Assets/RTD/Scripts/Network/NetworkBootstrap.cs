using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class NetworkBootstrap : MonoBehaviour
{
    [SerializeField] private NetworkManager networkManagerPrefab;

    private NetworkManager _nm;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);

        // 1) NetworkManager 확보
        if (NetworkManager.Singleton != null)
        {
            _nm = NetworkManager.Singleton;
            InstallNgoCallbacks(_nm);
            return;
        }

        NetworkManager nm;

        if (networkManagerPrefab != null)
        {
            nm = Instantiate(networkManagerPrefab);
            DontDestroyOnLoad(nm.gameObject);
        }
        else
        {
            var go = new GameObject("NetworkManager");
            go.AddComponent<UnityTransport>();
            nm = go.AddComponent<NetworkManager>();
            DontDestroyOnLoad(go);
        }

        var utp = nm.GetComponent<UnityTransport>();
        if (utp != null && nm.NetworkConfig != null && nm.NetworkConfig.NetworkTransport == null)
        {
            nm.NetworkConfig.NetworkTransport = utp;
        }

        _nm = nm;
        
        InstallNgoCallbacks(_nm);
    }

    private void OnDestroy()
    {
        UninstallNgoCallbacks(_nm);
    }
    
    private void InstallNgoCallbacks(NetworkManager nm)
    {
        if (nm == null) return;
        
        nm.OnClientConnectedCallback -= HandleClientConnected;
        nm.OnClientDisconnectCallback -= HandleClientDisconnected;

        nm.OnClientConnectedCallback += HandleClientConnected;
        nm.OnClientDisconnectCallback += HandleClientDisconnected;

        Debug.Log("[NetworkBootstrap] NGO callbacks installed");
    }

    private void UninstallNgoCallbacks(NetworkManager nm)
    {
        if (nm == null) return;

        nm.OnClientConnectedCallback -= HandleClientConnected;
        nm.OnClientDisconnectCallback -= HandleClientDisconnected;
    }

    private void HandleClientConnected(ulong clientId)
    {
        MultiplayerContext.ResolveMyLaneIdFromNgo();
        
        var list = NetworkManager.Singleton != null ? NetworkManager.Singleton.ConnectedClientsList : null;
        int count = (list != null) ? list.Count : 1;
        MultiplayerContext.SetPlayersCount(count);

        Debug.Log($"[NGO] ClientConnected clientId={clientId} MyLaneId={MultiplayerContext.MyLaneId} PlayersCount={MultiplayerContext.PlayersCount}");
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        MultiplayerContext.ResolveMyLaneIdFromNgo();

        var list = NetworkManager.Singleton != null ? NetworkManager.Singleton.ConnectedClientsList : null;
        int count = (list != null) ? list.Count : 1;
        MultiplayerContext.SetPlayersCount(count);

        Debug.Log($"[NGO] ClientDisconnected clientId={clientId} MyLaneId={MultiplayerContext.MyLaneId} PlayersCount={MultiplayerContext.PlayersCount}");
    }
}
