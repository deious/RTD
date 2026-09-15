using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

public class MiniMapMultiplayerDriver : MonoBehaviour
{
    [SerializeField] private MiniMapUIController miniMapUI;
    private CancellationTokenSource _cts;

    private void Awake()
    {
        if (!miniMapUI) miniMapUI = FindObjectOfType<MiniMapUIController>(true);
    }

    private void OnEnable()
    {
        _cts = new CancellationTokenSource();
        TryBindNgoEvents();
        RefreshStabilizeAsync(_cts.Token).Forget();
    }

    private void OnDisable()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        UnbindNgoEvents();
    }
    
    private async UniTaskVoid RefreshStabilizeAsync(CancellationToken ct)
    {
        float end = Time.realtimeSinceStartup + 3f;
        int last = -1;

        while (Time.realtimeSinceStartup < end)
        {
            ct.ThrowIfCancellationRequested();

            int count = GetPlayerCount();
            if (count != last)
            {
                last = count;
                miniMapUI?.SetPlayerCount(count);
            }
            
            if (count >= MultiplayerContext.PlayersCount)
                break;

            await UniTask.Delay(100, ignoreTimeScale: true, cancellationToken: ct);
        }

        miniMapUI?.SetPlayerCount(GetPlayerCount());
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
    
    private void OnClientConnected(ulong _)  => miniMapUI?.SetPlayerCount(GetPlayerCount());
    private void OnClientDisconnected(ulong _) => miniMapUI?.SetPlayerCount(GetPlayerCount());

    private int GetPlayerCount()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening) return 1;

        int connected = nm.ConnectedClientsList != null ? nm.ConnectedClientsList.Count : 1;
        return Mathf.Clamp(connected, 1, 4);
    }
}