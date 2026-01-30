using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using Cysharp.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;

public class LobbyFlow : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private int maxPlayers = 4;

    private ISession _session;
    private bool _ready;

    public void OnClickCreateLobby()
    {
        CreateLobbyAsync().Forget();
    }
    
    public void OnClickJoinLobbyByCode(string sessionCode)
    {
        JoinLobbyByCodeAsync(sessionCode).Forget();
    }

    public void OnClickToggleReady()
    {
        ToggleReadyAsync().Forget();
    }

    public void OnClickStartGame()
    {
        StartGameAsync().Forget();
    }

    private async UniTask CreateLobbyAsync()
    {
        await EnsureUGSReady();

        var options = new SessionOptions
        {
            MaxPlayers = maxPlayers,
            IsPrivate = false
        };

        _session = await MultiplayerService.Instance.CreateSessionAsync(options).AsUniTask();

        Debug.Log($"[Lobby] Created Session. Id={_session.Id} Code={_session.Code}");
        
        _ready = true;
        await SaveReadyAsync(_ready);
    }

    private async UniTask JoinLobbyByCodeAsync(string sessionCode)
    {
        await EnsureUGSReady();

        _session = await MultiplayerService.Instance.JoinSessionByCodeAsync(sessionCode).AsUniTask();

        Debug.Log($"[Lobby] Joined Session. Id={_session.Id} Code={_session.Code}");

        _ready = false;
        await SaveReadyAsync(_ready);
    }

    private async UniTask ToggleReadyAsync()
    {
        if (_session == null) return;

        _ready = !_ready;
        await SaveReadyAsync(_ready);
    }

    private async UniTask StartGameAsync()
    {
        if (_session == null) 
            return;

        if (!_session.IsHost)
        {
            Debug.LogWarning("[Lobby] Only host can start the game.");
            return;
        }

        if (!AllReady())
        {
            Debug.LogWarning("[Lobby] Not all players ready.");
            return;
        }

        Debug.Log("[Lobby] All ready. Starting game...");
        
        if (!NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.StartHost();

        AppFlowManager.Instance.StartMultiGameFromHost();
    }

    private async UniTask EnsureUGSReady()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync().AsUniTask();

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync().AsUniTask();
    }

    private async UniTask SaveReadyAsync(bool value)
    {
        if (_session == null) return;
        
        var props = new Dictionary<string, PlayerProperty>
        {
            { "ready", new PlayerProperty(value ? "1" : "0") }
        };

        _session.CurrentPlayer.SetProperties(props);
        await _session.SaveCurrentPlayerDataAsync().AsUniTask();

        Debug.Log($"[Lobby] Ready saved = {value}");
    }

    private bool AllReady()
    {
        if (_session == null) return false;

        foreach (var p in _session.Players)
        {
            if (!p.Properties.TryGetValue("ready", out var prop)) return false;
            if (prop.Value != "1") return false;
        }
        return true;
    }
}
