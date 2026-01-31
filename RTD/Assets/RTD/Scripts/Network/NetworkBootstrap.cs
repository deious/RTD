using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class NetworkBootstrap : MonoBehaviour
{
    [SerializeField] private NetworkManager networkManagerPrefab; 

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);

        if (NetworkManager.Singleton != null)
            return;

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
    }
}
