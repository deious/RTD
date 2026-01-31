using Unity.Netcode;
using UnityEngine;

public class NgoDebugHooks : MonoBehaviour
{
    private void Update()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        // 너무 spam이면 1초에 1번만 찍도록 해도 됨
        // Debug.Log($"[NGO] State IsClient={nm.IsClient} IsServer={nm.IsServer} IsHost={nm.IsHost} IsListening={nm.IsListening}");
    }

    private void OnEnable()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            Debug.LogWarning("[NGO] NetworkManager.Singleton is null on OnEnable");
            return;
        }

        nm.OnClientConnectedCallback += OnConnected;
        nm.OnClientDisconnectCallback += OnDisconnected;
        nm.OnTransportFailure += OnTransportFailure;

        // 추가
        nm.OnServerStarted += OnServerStarted;
        nm.OnClientStarted += OnClientStarted;

        Debug.Log("[NGO] Hooks registered");
    }

    private void OnDisable()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        nm.OnClientConnectedCallback -= OnConnected;
        nm.OnClientDisconnectCallback -= OnDisconnected;
        nm.OnTransportFailure -= OnTransportFailure;

        nm.OnServerStarted -= OnServerStarted;
        nm.OnClientStarted -= OnClientStarted;

        Debug.Log("[NGO] Hooks unregistered");
    }

    private void OnClientStarted()
        => Debug.Log("[NGO] OnClientStarted");

    private void OnServerStarted()
        => Debug.Log("[NGO] OnServerStarted");

    private void OnConnected(ulong clientId)
        => Debug.Log($"[NGO] Connected clientId={clientId} LocalClientId={NetworkManager.Singleton.LocalClientId}");

    private void OnDisconnected(ulong clientId)
        => Debug.LogWarning($"[NGO] Disconnected clientId={clientId}");

    private void OnTransportFailure()
        => Debug.LogError("[NGO] TransportFailure");
}