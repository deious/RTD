using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class NetworkBootstrap : MonoBehaviour
{
    [SerializeField] private NetworkManager networkManagerPrefab;
    
    [Header("Chat")]
    [SerializeField] private ChatNetworkBridge chatNetworkBridgePrefab;

    private NetworkManager _nm;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        
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
        var list = NetworkManager.Singleton != null ? NetworkManager.Singleton.ConnectedClientsList : null;
        int count = (list != null) ? list.Count : 1;
        EnsureChatBridgeServer();

        Debug.Log($"[NGO] ClientConnected clientId={clientId} MyLaneId={MultiplayerContext.MyLaneId} PlayersCount={MultiplayerContext.PlayersCount}");
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        var list = NetworkManager.Singleton != null ? NetworkManager.Singleton.ConnectedClientsList : null;
        int count = (list != null) ? list.Count : 1;

        Debug.Log($"[NGO] ClientDisconnected clientId={clientId} MyLaneId={MultiplayerContext.MyLaneId} PlayersCount={MultiplayerContext.PlayersCount}");
    }
   
    private void EnsureChatBridgeServer()
    {
        if (_nm == null) return;
        if (!_nm.IsServer || !_nm.IsListening) return;

        if (ChatNetworkBridge.Instance != null && ChatNetworkBridge.Instance.IsSpawned)
            return;

        if (chatNetworkBridgePrefab == null)
        {
            Debug.LogError("[NetworkBootstrap] chatNetworkBridgePrefab is null");
            return;
        }
        
        if (_nm.NetworkConfig != null)
        {
            var prefabs = _nm.NetworkConfig.Prefabs;
            bool found = false;

            for (int i = 0; i < prefabs.Prefabs.Count; i++)
            {
                if (prefabs.Prefabs[i].Prefab == chatNetworkBridgePrefab.gameObject)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                prefabs.Add(new NetworkPrefab { Prefab = chatNetworkBridgePrefab.gameObject });
                Debug.Log("[NetworkBootstrap] ChatNetworkBridge prefab added to NetworkConfig (runtime)");
            }
        }

        var bridge = Instantiate(chatNetworkBridgePrefab);
        var no = bridge.GetComponent<NetworkObject>();
        if (no == null)
        {
            Debug.LogError("[NetworkBootstrap] ChatNetworkBridge prefab has no NetworkObject");
            Destroy(bridge.gameObject);
            return;
        }

        no.DestroyWithScene = false;
        no.Spawn(true);

        Debug.Log("[NetworkBootstrap] ChatNetworkBridge spawned/ensured");
    }
}
