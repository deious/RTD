using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Multiplayer;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;

public class LobbySystem : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private LobbyUI ui;

    [Header("Config")]
    [SerializeField] private int maxPlayers = 4;
    
    [SerializeField] private float pollIntervalSeconds = 0.8f;

    private ISession _session;
    private bool _isReady;
    private UniTask _pollTask;
    private bool _polling;
    private bool _connectingToGame;
    private string _lastTriedRelayCode;
    private float _nextRelayTryTime;
    private int _relayTryCount;

    private const string KEY_READY = "ready";
    private const string KEY_NAME  = "name";
    private const string KEY_RELAY_CODE   = "relayJoinCode";
    private const string KEY_GAME_START   = "gameStart";     // "1"이면 시작
    private const string KEY_SCENE_NAME   = "scene"; 
    
    public UniTask OnClickLeave() => OnClickLeaveAsync();
    public UniTask CreateLobby() => CreateLobbyAsync();
    public UniTask JoinByCode(string code) => JoinByCodeAsync(code);
    public UniTask ToggleReady() => ToggleReadyAsync();
    public UniTask StartGame() => StartGameAsync();
    public UniTask LeaveToTitle() => LeaveToTitleAsync();
    
    private void Start()
    {
        if (ui != null) 
            ui.ApplyIdleUI();
    }
    
    private async UniTask CreateLobbyAsync()
    {
        await EnsureUGSReady();

        ui.SetStatus("방 생성 중...");

        var options = new SessionOptions
        {
            MaxPlayers = maxPlayers,
            IsPrivate = false
        };

        _session = await MultiplayerService.Instance.CreateSessionAsync(options).AsUniTask();
        
        _isReady = false;
        await SavePlayerPropsAsync();

        ui.ApplyInSessionUI(_session.Code, _session.IsHost);
        ui.SetReadyButtonText(_isReady);

        StartPolling();

        Debug.Log($"[Lobby] Created Session: Id={_session.Id}, Code={_session.Code}");
    }
    
    private async UniTask OnClickLeaveAsync()
    {
        if (_session != null)
        {
            await LeaveSessionOnlyAsync();
            return;
        }
        
        AppFlowManager.Instance.GoTitle();
    }

    private async UniTask LeaveSessionOnlyAsync()
    {
        StopPolling();

        try
        {
            ui.SetStatus("로비 나가는 중...");
            await _session.LeaveAsync().AsUniTask();
        }
        catch
        {
            // leave 실패해도 UI는 초기화
        }

        _session = null;
        _isReady = false;
        
        if (Unity.Netcode.NetworkManager.Singleton != null &&
            Unity.Netcode.NetworkManager.Singleton.IsListening)
        {
            Unity.Netcode.NetworkManager.Singleton.Shutdown();
        }
        
        ui.ApplyIdleUI();
        ui.SetStatus("방을 생성하거나 코드를 입력해 참가하세요.");
    }

    private async UniTask JoinByCodeAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            ui.SetStatus("코드를 입력하세요.");
            return;
        }

        await EnsureUGSReady();

        ui.SetStatus("참가 중...");

        _session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code).AsUniTask();

        _isReady = false;
        await SavePlayerPropsAsync();

        ui.ApplyInSessionUI(_session.Code, _session.IsHost);
        ui.SetReadyButtonText(_isReady);

        StartPolling();

        Debug.Log($"[Lobby] Joined Session: Id={_session.Id}, Code={_session.Code}");
    }

    private async UniTask ToggleReadyAsync()
    {
        if (_session == null)
        {
            ui.SetStatus("세션이 없습니다.");
            return;
        }

        _isReady = !_isReady;
        await SavePlayerPropsAsync();
        ui.SetReadyButtonText(_isReady);
        
        RefreshUIFromSession(_session);
    }

    private async UniTask StartGameAsync()
    {
        if (_session == null)
        {
            ui.SetStatus("세션이 없습니다.");
            return;
        }

        if (!_session.IsHost)
        {
            ui.SetStatus("호스트만 시작할 수 있습니다.");
            return;
        }

        if (!IsAllReady(_session))
        {
            ui.SetStatus("아직 준비 안 된 플레이어가 있습니다.");
            return;
        }

        ui.SetStatus("게임 시작 준비 중...");

        try
        {
            Debug.Log($"[HostStart] cloudProjectId={Application.cloudProjectId} state={UnityServices.State}");
            int maxConn = Mathf.Max(1, _session.Players.Count - 1);
            string relayJoinCode = await RelayConnector.Instance.StartHostWithRelayAsync(maxConn);
            
            Debug.Log($"[Lobby] Host created Relay joinCode={relayJoinCode}");

            var host = _session.AsHost();

            host.SetProperty(KEY_RELAY_CODE, new SessionProperty(relayJoinCode, VisibilityPropertyOptions.Member));
            host.SetProperty(KEY_GAME_START, new SessionProperty("1", VisibilityPropertyOptions.Member));
            host.SetProperty(KEY_SCENE_NAME, new SessionProperty("InGame", VisibilityPropertyOptions.Member));

            await host.SavePropertiesAsync().AsUniTask();
            
            await _session.RefreshAsync().AsUniTask();
            if (_session.Properties.TryGetValue(KEY_RELAY_CODE, out var p))
                Debug.Log($"[Lobby] Session property relayJoinCode saved = {p.Value}");
            else
                Debug.LogError("[Lobby] Session property relayJoinCode missing AFTER save!");

            StopPolling();

            ui.SetStatus("게임 씬 로딩...");
            int expected = _session.Players.Count;
            await AppFlowManager.Instance.StartMultiGameFromHostAsync(expected);
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            ui.SetStatus("게임 시작 실패. 로그 확인");
        }
    }

    private async UniTask LeaveToTitleAsync()
    {
        StopPolling();

        if (_session != null)
        {
            try
            {
                ui.SetStatus("로비 나가는 중...");
                await _session.LeaveAsync().AsUniTask();
            }
            catch
            {
                // leave 실패해도 타이틀 이동은 진행
            }
            _session = null;
        }

        if (ui != null) ui.ApplyIdleUI();
        
        AppFlowManager.Instance.GoTitle();
    }
    
    private void StartPolling()
    {
        if (_polling) return;
        _polling = true;
        PollLoop().Forget();
    }

    private void StopPolling()
    {
        _polling = false;
    }

    private async UniTaskVoid PollLoop()
    {
        while (_polling)
        {
            if (_session != null)
            {
                try
                {
                    await _session.RefreshAsync().AsUniTask();
                    await TryAutoStartClientAsync(_session);
                    
                    if (!_connectingToGame)
                        RefreshUIFromSession(_session);
                }
                catch
                {
                    ui.SetStatus("세션 갱신 실패(재시도 중)...");
                }
            }

            await UniTask.Delay(System.TimeSpan.FromSeconds(pollIntervalSeconds));
        }
    }
    
    private async UniTask TryAutoStartClientAsync(ISession session)
    {
        if (session == null) return;
        if (session.IsHost) return;
        if (_connectingToGame) return;

        if (!session.Properties.TryGetValue(KEY_GAME_START, out var startProp))
            return;

        bool shouldStart = startProp.Value == "1" ||
                           startProp.Value.Equals("true", System.StringComparison.OrdinalIgnoreCase);
        if (!shouldStart) return;

        if (!session.Properties.TryGetValue(KEY_RELAY_CODE, out var relayProp) ||
            string.IsNullOrWhiteSpace(relayProp.Value))
        {
            Debug.LogWarning("[Lobby] GAME_START=1 but relayJoinCode is missing/empty. Waiting...");
            return;
        }

        string relayCode = relayProp.Value.Trim().ToUpperInvariant();
        
        Debug.Log($"[Lobby] Client read relayJoinCode from session = {relayCode}");

        float now = Time.realtimeSinceStartup;

        bool sameCode = !string.IsNullOrEmpty(_lastTriedRelayCode) && _lastTriedRelayCode == relayCode;
        if (sameCode && now < _nextRelayTryTime)
            return;

        _connectingToGame = true;
        ui.SetStatus("호스트가 게임 시작. Relay 접속 중...");

        try
        {
            _lastTriedRelayCode = relayCode;

            _relayTryCount = sameCode ? (_relayTryCount + 1) : 0;
            float cooldown = Mathf.Min(10f, 1.5f * _relayTryCount);
            _nextRelayTryTime = now + cooldown;

            await UniTask.Yield();
            Debug.Log($"[ClientStart] cloudProjectId={Application.cloudProjectId} state={UnityServices.State} relay={relayCode}");
            await RelayConnector.Instance.StartClientWithRelayAsync(relayCode);

            StopPolling();
            ui.SetStatus("연결 완료. 호스트 씬 전환 대기...");
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            ui.SetStatus("Relay 접속 실패. (코드 갱신 대기 중)");
            _connectingToGame = false;
        }
    }


    private void RefreshUIFromSession(ISession session)
    {
        if (ui == null || session == null) return;

        ui.ClearSlots();

        var players = session.Players;
        string myPlayerId = session.CurrentPlayer.Id;
        bool amIHost = session.IsHost;

        for (int i = 0; i < players.Count && i < maxPlayers; i++)
        {
            var p = players[i];

            string name = GetPlayerDisplayName(p, i);
            bool isHost = amIHost && (p.Id == myPlayerId);
            bool ready = GetBoolProp(p, KEY_READY);

            ui.SetSlot(i, name, isHost, ready);
        }

        bool canStart = session.IsHost && IsAllReady(session) && players.Count >= 1;
        ui.SetStartInteractable(canStart);

        ui.SetStatus($"{players.Count}/{maxPlayers} players" +
                     (canStart ? " - All Ready" : ""));
    }

    private static bool IsAllReady(ISession session)
    {
        foreach (var p in session.Players)
        {
            if (!GetBoolProp(p, KEY_READY))
                return false;
        }
        return session.Players.Count > 0;
    }

    private async UniTask SavePlayerPropsAsync()
    {
        if (_session == null) return;
        
        var props = new Dictionary<string, PlayerProperty>
        {
            { KEY_READY, new PlayerProperty(_isReady ? "1" : "0") },
            { KEY_NAME,  new PlayerProperty(GetDefaultName()) }
        };

        _session.CurrentPlayer.SetProperties(props);
        await _session.SaveCurrentPlayerDataAsync().AsUniTask();
    }

    private static bool GetBoolProp(IReadOnlyPlayer p, string key)
    {
        if (p == null) return false;
        if (!p.Properties.TryGetValue(key, out var prop)) return false;
        return prop.Value == "1" || prop.Value == "true" || prop.Value == "True";
    }

    private static string GetPlayerDisplayName(IReadOnlyPlayer p, int fallbackIndex)
    {
        if (p != null &&
            p.Properties.TryGetValue(KEY_NAME, out var prop) &&
            !string.IsNullOrWhiteSpace(prop.Value))
        {
            return prop.Value;
        }

        return $"Player {fallbackIndex + 1}";
    }

    private static string GetDefaultName()
    {
        var id = AuthenticationService.Instance.IsSignedIn ? AuthenticationService.Instance.PlayerId : "Guest";
        if (string.IsNullOrEmpty(id)) return "Player";
        return id.Length > 6 ? $"P-{id.Substring(0, 6)}" : $"P-{id}";
    }
    
    private static async UniTask EnsureUGSReady()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync().AsUniTask();

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync().AsUniTask();
        
        Debug.Log($"[UGS] cloudProjectId={Application.cloudProjectId}");
        Debug.Log($"[UGS] servicesState={Unity.Services.Core.UnityServices.State}");
    }
}
