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
        
        if (networkManagerPrefab != null)
        {
            var nm = Instantiate(networkManagerPrefab);
            DontDestroyOnLoad(nm.gameObject);
            return;
        }
        
        var go = new GameObject("NetworkManager");
        go.AddComponent<UnityTransport>();
        go.AddComponent<NetworkManager>();
        DontDestroyOnLoad(go);
    }
}
