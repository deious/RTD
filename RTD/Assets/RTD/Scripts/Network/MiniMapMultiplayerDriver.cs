using Unity.Netcode;
using UnityEngine;

public class MiniMapMultiplayerDriver : MonoBehaviour
{
    [SerializeField] private MiniMapUIController miniMapUI;

    private void Awake()
    {
        if (!miniMapUI) miniMapUI = FindObjectOfType<MiniMapUIController>(true);
    }

    private void OnEnable()
    {
        TryBindNgoEvents();
        RefreshNow();
    }

    private void OnDisable()
    {
        UnbindNgoEvents();
    }

    private void TryBindNgoEvents()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;
        
        nm.OnClientConnectedCallback -= OnClientConnected;
        nm.OnClientConnectedCallback += OnClientConnected;

        nm.OnClientDisconnectCallback -= OnClientDisconnected;
        nm.OnClientDisconnectCallback += OnClientDisconnected;
    }

    private void UnbindNgoEvents()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        nm.OnClientConnectedCallback -= OnClientConnected;
        nm.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    private void OnClientConnected(ulong _)
    {
        RefreshNow();
    }

    private void OnClientDisconnected(ulong _)
    {
        RefreshNow();
    }

    public void RefreshNow()
    {
        if (!miniMapUI) return;

        int count = GetPlayerCount();
        miniMapUI.SetPlayerCount(count);
    }

    private int GetPlayerCount()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return 1;
        
        if (!nm.IsListening) return 1;
        
        int connected = nm.ConnectedClientsList != null ? nm.ConnectedClientsList.Count : 1;
        return Mathf.Clamp(connected, 1, 4);
    }
}