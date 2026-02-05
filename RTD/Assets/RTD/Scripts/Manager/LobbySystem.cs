using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using RTD.Scripts.Network;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Multiplayer;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;

public class LobbySystem : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private LobbyUI ui;

    [Header("Config")]
    [SerializeField] private int maxPlayers = 4;

    [SerializeField] private float pollIntervalSeconds = 2.0f;
    [SerializeField] private float sessionRefreshMinInterval = 5.0f;
    private bool _refreshing;
    private bool _connectedForChat;
    private bool _clientConnectInProgress;
    private bool _isReady;
    private bool _polling;
    private bool _connectingToGame;
    private float _nextSessionRefreshTime;
    private float _nextRelayTryTime;
    private float _refreshBackoffSec = 0f;
    private int _relayTryCount;
    private string _lastTriedRelayCode;
    private ISession _session;

    private const string KEY_READY = "ready";
    private const string KEY_NAME = "name";
    private const string KEY_RELAY_CODE = "relayJoinCode";
    private const string KEY_GAME_START = "gameStart";     // "1"이면 시작
    private const string KEY_SCENE_NAME = "scene";

    public UniTask OnClickLeave() => OnClickLeaveAsync();
    public UniTask CreateLobby() => CreateLobbyAsync();
    public UniTask JoinByCode(string code) => JoinByCodeAsync(code);
    public UniTask ToggleReady() => ToggleReadyAsync();
    public UniTask StartGame() => StartGameAsync();
    public UniTask LeaveToTitle() => LeaveToTitleAsync();
    public UniTask RefreshMyNameFromLocal() => RefreshMyNameFromLocalAsync();

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

        await EnsureRelayConnectedForLobbyChatHostAsync();
        StartPolling();

        Debug.Log($"[Lobby] Created Session: Id={_session.Id}, Code={_session.Code}");
    }
    
    private async UniTask EnsureRelayConnectedForLobbyChatHostAsync()
    {
        if (_session == null) return;
        if (!_session.IsHost) return;
        if (_connectedForChat) return;

        ui.SetStatus("로비 채팅 연결 중... (Host Relay 시작)");
        
        int maxConn = Mathf.Max(1, maxPlayers - 1);
        
        string joinCode = await RelayConnector.Instance.StartHostWithRelayAsync(maxConn);
        
        var host = _session.AsHost();
        host.SetProperty(KEY_RELAY_CODE, new SessionProperty(joinCode, VisibilityPropertyOptions.Member));
        host.SetProperty(KEY_GAME_START, new SessionProperty("0", VisibilityPropertyOptions.Member)); // 아직 시작 아님
        host.SetProperty(KEY_SCENE_NAME, new SessionProperty("Lobby", VisibilityPropertyOptions.Member));
        await host.SavePropertiesAsync().AsUniTask();

        _connectedForChat = true;
        ui.SetStatus("로비 채팅 연결 완료 (플레이어 접속 대기)");
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
        
        _connectedForChat = false;
        _lastTriedRelayCode = null;
        _nextRelayTryTime = 0f;
        _relayTryCount = 0;
        _clientConnectInProgress = false;
        _connectingToGame = false;
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
        
        try
        {
            await _session.RefreshAsync().AsUniTask();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Lobby] Refresh after join failed (will retry in polling). {e.Message}");
        }

        ui.ApplyInSessionUI(_session.Code, _session.IsHost);
        ui.SetReadyButtonText(_isReady);
        
        StartPolling();
        RefreshUIFromSession(_session);
        EnsureRelayConnectedForLobbyChatClientAsync().Forget();

        Debug.Log($"[Lobby] Joined Session: Id={_session.Id}, Code={_session.Code}");
    }
    
    private async UniTask EnsureRelayConnectedForLobbyChatClientAsync()
    {
        if (_session == null) return;
        if (_session.IsHost) return;
        if (_connectedForChat) return;
        if (_clientConnectInProgress) return;

        _clientConnectInProgress = true;

        try
        {
            ui.SetStatus("채팅 시스템 준비 중... (Relay 코드 확인)");

            float end = Time.realtimeSinceStartup + 10f;

            while (Time.realtimeSinceStartup < end)
            {
                // ✅ PollLoop가 이미 주기적으로 _session.RefreshAsync()를 하고 있으니
                // 여기서는 Refresh를 직접 호출하지 않는다.

                if (_session != null &&
                    _session.Properties.TryGetValue(KEY_RELAY_CODE, out var relayProp) &&
                    !string.IsNullOrWhiteSpace(relayProp.Value))
                {
                    string code = relayProp.Value.Trim().ToUpperInvariant();

                    ui.SetStatus("채팅 시스템 준비 중... (Relay 접속)");
                    await RelayConnector.Instance.StartClientWithRelayAsync(code);

                    _connectedForChat = true;

                    if (ChatNetworkBridge.Instance != null && ChatNetworkBridge.Instance.IsSpawned)
                        ChatNetworkBridge.Instance.RegisterMyNicknameNow();

                    ui.SetStatus("채팅 시스템 준비 완료");
                    return;
                }

                // ✅ 1초 대기 대신 더 짧게 "확인만" 반복 (Refresh는 PollLoop가 함)
                await UniTask.Delay(250, ignoreTimeScale: true);
            }

            ui.SetStatus("채팅 연결 대기 중 (코드 준비 전)");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            ui.SetStatus("채팅 시스템 준비 실패(로비는 계속 진행)");
        }
        finally
        {
            _clientConnectInProgress = false;
        }
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

        StopPolling();
        _connectingToGame = true;

        ui.SetStatus("Relay 생성/시작 중...");

        try
        {
            int maxConn = Mathf.Max(1, _session.Players.Count - 1);

            Debug.Log($"[HostStart] players={_session.Players.Count} maxConn={maxConn}");
            
            string relayJoinCode =
                await RelayConnector.Instance.StartHostWithRelayAsync(maxConn);

            Debug.Log($"[Lobby] Host created Relay joinCode={relayJoinCode}");
            
            var host = _session.AsHost();
            host.SetProperty(KEY_RELAY_CODE,
                new SessionProperty(relayJoinCode, VisibilityPropertyOptions.Member));
            host.SetProperty(KEY_GAME_START,
                new SessionProperty("1", VisibilityPropertyOptions.Member));
            host.SetProperty(KEY_SCENE_NAME,
                new SessionProperty("InGame", VisibilityPropertyOptions.Member));
            
            await host.SavePropertiesAsync().AsUniTask();

            ui.SetStatus("클라이언트 접속 대기 중...");

            MultiplayerContext.SetPlayersCount(_session.Players.Count);
            
            AppFlowManager.Instance
                .StartMultiGameFromHostAsync(_session.Players.Count, 20f)
                .Forget();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            ui.SetStatus("게임 시작 실패. 로그 확인");

            _connectingToGame = false;
            StartPolling();
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
        _nextSessionRefreshTime = 0f;
        _refreshBackoffSec = 0f;

        while (_polling)
        {
            if (_session != null)
            {
                try
                {
                    float now = Time.realtimeSinceStartup;
                    float minInterval = sessionRefreshMinInterval + _refreshBackoffSec;
                    
                    if (now >= _nextSessionRefreshTime)
                    {
                        _nextSessionRefreshTime = now + minInterval;
                        await _session.RefreshAsync().AsUniTask();
                    }
                    
                    await TryAutoStartClientAsync(_session);
                    
                    if (!_connectingToGame)
                        RefreshUIFromSession(_session);
                    
                    _refreshBackoffSec = 0f;
                }
                catch (Exception e)
                {
                    Debug.LogException(e);

                    // 메시지 기반(임시): 패키지에 따라 예외 타입이 다를 수 있어서
                    bool tooMany = e.Message.Contains("Too Many Requests", StringComparison.OrdinalIgnoreCase);

                    if (tooMany)
                    {
                        // 2초 -> 4초 -> 8초 (최대 20초)
                        _refreshBackoffSec = Mathf.Clamp(_refreshBackoffSec <= 0f ? 2f : _refreshBackoffSec * 2f, 2f, 20f);
                        ui.SetStatus($"세션 갱신 제한(429). {_refreshBackoffSec:0}s 후 재시도...");
                    }
                    else
                    {
                        ui.SetStatus("세션 갱신 실패(재시도 중)...");
                    }
                }
            }

            await UniTask.Delay(TimeSpan.FromSeconds(pollIntervalSeconds), ignoreTimeScale: true);
        }
    }
    
    private async UniTask RefreshMyNameFromLocalAsync()
    {
        if (_session == null) return;
        
        await SavePlayerPropsAsync();
    }

    private async UniTask TryAutoStartClientAsync(ISession session)
    {
        if (session == null) return;
        if (session.IsHost) return;
        if (_connectingToGame) return;
        if (_clientConnectInProgress) return;
        
        if (!session.Properties.TryGetValue(KEY_GAME_START, out var startProp))
            return;

        bool shouldStart = startProp.Value == "1" ||
                           startProp.Value.Equals("true", StringComparison.OrdinalIgnoreCase);
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
        _clientConnectInProgress = true; // ✅ 추가
        ui.SetStatus("호스트가 게임 시작. Relay 접속 중...");

        try
        {
            _lastTriedRelayCode = relayCode;

            _relayTryCount = sameCode ? (_relayTryCount + 1) : 0;

            // ✅ 백오프 강화: 1.5s * count -> 최소 2s, 최대 12s
            float cooldown = Mathf.Clamp(2f + 2f * _relayTryCount, 2f, 12f);
            _nextRelayTryTime = now + cooldown;

            await UniTask.Yield();
            Debug.Log($"[ClientStart] cloudProjectId={Application.cloudProjectId} state={UnityServices.State} relay={relayCode}");

            await RelayConnector.Instance.StartClientWithRelayAsync(relayCode);

            StopPolling();
            ui.SetStatus("연결 완료. 호스트 씬 전환 대기...");
        }
        catch (RelayServiceException rse)
        {
            Debug.LogException(rse);

            // ✅ 404 / NotFound 류는 '같은 코드' 재시도 의미 없음
            bool notFound =
                rse.Message.Contains("join code not found", StringComparison.OrdinalIgnoreCase) ||
                (rse.Message.Contains("not found", StringComparison.OrdinalIgnoreCase));

            if (notFound)
            {
                ui.SetStatus("Relay 코드 만료/무효. 호스트 코드 갱신 대기...");

                // ✅ 같은 코드에 집착하지 않게: 마지막 시도 코드 유지하되, 다음 시도는 더 늦게
                _nextRelayTryTime = Time.realtimeSinceStartup + 6f;
                _connectingToGame = false;
                return;
            }

            ui.SetStatus("Relay 접속 실패. (재시도 대기 중)");
            _nextRelayTryTime = Time.realtimeSinceStartup + 4f;
            _connectingToGame = false;
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            ui.SetStatus("네트워크 오류 발생. 재시도 대기 중");
            _nextRelayTryTime = Time.realtimeSinceStartup + 4f;
            _connectingToGame = false;
        }
        finally
        {
            _clientConnectInProgress = false; // ✅ 추가
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

        string nick = NicknameStore.Get();
        if (string.IsNullOrWhiteSpace(nick))
            nick = GetDefaultName();
        
        var props = new Dictionary<string, PlayerProperty>
        {
            { KEY_READY, new PlayerProperty(_isReady ? "1" : "0") },
            { KEY_NAME, new PlayerProperty(nick) }
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
        Debug.Log($"[UGS] servicesState={UnityServices.State}");
    }
}
