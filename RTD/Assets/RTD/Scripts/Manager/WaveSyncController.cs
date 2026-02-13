using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

public enum WavePhase : byte
{
    None = 0,
    InWave = 1,
    Intermission = 2,
    Augment = 3,
}

public sealed class WaveSyncController : NetworkBehaviour
{
    public static WaveSyncController Instance { get; private set; }

    [Header("Config")]
    [SerializeField] private float intermissionSeconds = 20f;

    // 서버가 들고 있는 "전체 웨이브 상태"
    private readonly NetworkVariable<int> _currentWave = new(1);
    private readonly NetworkVariable<WavePhase> _phase = new(WavePhase.None);
    private readonly NetworkVariable<double> _nextWaveStartServerTime = new(0); // NetworkManager.ServerTime.Time 기준

    // 서버가 들고 있는 "클리어/증강완료" 플래그
    private readonly Dictionary<ulong, bool> _clearedByClient = new();
    private readonly Dictionary<ulong, bool> _augmentDoneByClient = new();
    private readonly Dictionary<ulong, bool> _aliveByClient = new();
    private readonly Dictionary<ulong, int> _laneByClient = new();

    private CancellationTokenSource _cts;

    public int CurrentWave => _currentWave.Value;
    public WavePhase Phase => _phase.Value;
    public double NextWaveStartServerTime => _nextWaveStartServerTime.Value;

    public event Action<int> OnWaveStartClient;                 // 클라: 웨이브 시작
    public event Action<int, float> OnIntermissionClient;       // 클라: 인터미션 시작(20초)
    public event Action<int> OnAugmentStartClient;              // 클라: 증강 선택 시작

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        // 클라도 phase 변화에 반응 가능(원하면 활용)
        _phase.OnValueChanged -= OnPhaseChanged;
        _phase.OnValueChanged += OnPhaseChanged;

        if (!IsServer) return;

        // 씬 진입 직후 기본 상태
        _phase.Value = WavePhase.None;
        _nextWaveStartServerTime.Value = 0;

        // 현재 접속자 기준 초기화
        ResetFlagsForConnectedClients();
        NetworkManager.OnClientConnectedCallback += OnClientConnected;
        NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
        
        StartInitialIntermissionThenWave1Server(intermissionSeconds).Forget();

        Debug.Log($"[WaveSync][Server] Spawned. Start initial intermission={intermissionSeconds}s then Wave 1");
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager != null)
        {
            NetworkManager.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        _phase.OnValueChanged -= OnPhaseChanged;

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        if (Instance == this) Instance = null;
        base.OnNetworkDespawn();
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;

        _clearedByClient[clientId] = false;
        _augmentDoneByClient[clientId] = false;
        _aliveByClient[clientId] = true;

        if (!_laneByClient.ContainsKey(clientId))
            ResetFlagsForConnectedClients();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;

        _clearedByClient.Remove(clientId);
        _augmentDoneByClient.Remove(clientId);
        _aliveByClient.Remove(clientId);

        if (_laneByClient.TryGetValue(clientId, out int leftLane))
        {
            _laneByClient.Remove(clientId);
            LaneLeftClientRpc(leftLane);
        }

        CheckAndAdvanceIfReady();
    }

    private void ResetFlagsForConnectedClients()
    {
        _clearedByClient.Clear();
        _augmentDoneByClient.Clear();
        _aliveByClient.Clear();
        _laneByClient.Clear();

        var list = NetworkManager.ConnectedClientsList;

        var ids = new List<ulong>(list.Count);
        for (int i = 0; i < list.Count; i++)
            ids.Add(list[i].ClientId);

        ids.Sort();

        for (int i = 0; i < ids.Count; i++)
        {
            ulong id = ids[i];
            _clearedByClient[id] = false;
            _augmentDoneByClient[id] = false;
            _aliveByClient[id] = true;

            _laneByClient[id] = Mathf.Clamp(i, 0, 3);
        }
    }

    // -------------------------
    // Client -> Server reports
    // -------------------------

    [ServerRpc(RequireOwnership = false)]
    public void ReportWaveClearedServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;

        ulong cid = rpcParams.Receive.SenderClientId;
        _clearedByClient[cid] = true;

        CheckAndAdvanceIfReady();
    }

    [ServerRpc(RequireOwnership = false)]
    public void ReportAugmentDoneServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;

        ulong cid = rpcParams.Receive.SenderClientId;
        _augmentDoneByClient[cid] = true;

        CheckAndAdvanceIfReady();
    }
    
    [ServerRpc(RequireOwnership = false)]
    public void ReportPlayerEliminatedServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;

        ulong cid = rpcParams.Receive.SenderClientId;
        _aliveByClient[cid] = false;

        // 죽은 사람은 대기 조건에서 제외되므로 즉시 재평가
        CheckAndAdvanceIfReady();
    }

    // -------------------------
    // Server state machine
    // -------------------------

    private void CheckAndAdvanceIfReady()
    {
        if (!IsServer) return;
        
        int aliveCount = GetAliveCount();
        if (aliveCount <= 0) return;

        if (_phase.Value == WavePhase.InWave)
        {
            if (!IsAllTrueAmongAlive(_clearedByClient, _aliveByClient)) return;

            bool isBossWave = GameRuntime.Instance != null && GameRuntime.Instance.IsBossWave(_currentWave.Value);

            if (isBossWave)
            {
                StartAugmentPhaseServer();
            }
            else
            {
                StartIntermissionPhaseServer(intermissionSeconds).Forget();
            }
            return;
        }

        if (_phase.Value == WavePhase.Augment)
        {
            if (!IsAllTrueAmongAlive(_augmentDoneByClient, _aliveByClient)) return;

            StartIntermissionPhaseServer(intermissionSeconds).Forget();
            return;
        }
    }
    
    private int GetAliveCount()
    {
        int alive = 0;
        foreach (var kv in _aliveByClient)
            if (kv.Value) alive++;
        return alive;
    }

    private static bool IsAllTrueAmongAlive(Dictionary<ulong, bool> dict, Dictionary<ulong, bool> aliveDict)
    {
        foreach (var kv in aliveDict)
        {
            if (!kv.Value) continue;
            if (!dict.TryGetValue(kv.Key, out bool ok) || !ok)
                return false;
        }
        return true;
    }

    private static bool IsAllTrue(Dictionary<ulong, bool> dict, int connectedCount)
    {
        int ok = 0;
        foreach (var kv in dict)
        {
            if (kv.Value) ok++;
        }
        return ok >= connectedCount;
    }

    private async UniTaskVoid StartInitialIntermissionThenWave1Server(float sec)
    {
        if (!IsServer) return;
        
        _phase.Value = WavePhase.Intermission;

        double startAt = NetworkManager.ServerTime.Time + Math.Max(0.01f, sec);
        _nextWaveStartServerTime.Value = startAt;

        IntermissionClientRpc(1, sec);

        try
        {
            _cts.Token.ThrowIfCancellationRequested();
            await UniTask.Delay(TimeSpan.FromSeconds(sec), ignoreTimeScale: true, cancellationToken: _cts.Token);
        }
        catch (OperationCanceledException) { return; }

        StartWaveServer(1);
    }

    private void StartWaveServer(int wave)
    {
        if (!IsServer) return;

        ResetFlagsForConnectedClients();

        _currentWave.Value = wave;
        _phase.Value = WavePhase.InWave;
        _nextWaveStartServerTime.Value = 0;

        WaveStartClientRpc(wave);
    }

    private void StartAugmentPhaseServer()
    {
        if (!IsServer) return;

        var list = NetworkManager.ConnectedClientsList;
        for (int i = 0; i < list.Count; i++)
            _augmentDoneByClient[list[i].ClientId] = false;

        _phase.Value = WavePhase.Augment;
        AugmentStartClientRpc(_currentWave.Value);
    }

    private async UniTaskVoid StartIntermissionPhaseServer(float sec)
    {
        if (!IsServer) return;

        _phase.Value = WavePhase.Intermission;

        int nextWave = _currentWave.Value + 1;
        if (GameRuntime.Instance != null && nextWave > GameRuntime.Instance.MaxWave)
            return;

        double startAt = NetworkManager.ServerTime.Time + Math.Max(0.01f, sec);
        _nextWaveStartServerTime.Value = startAt;
        
        IntermissionClientRpc(nextWave, sec);

        try
        {
            _cts.Token.ThrowIfCancellationRequested();
            await UniTask.Delay(TimeSpan.FromSeconds(sec), ignoreTimeScale: true, cancellationToken: _cts.Token);
        }
        catch (OperationCanceledException) { return; }

        StartWaveServer(nextWave);
    }

    // -------------------------
    // RPCs (Server -> Clients)
    // -------------------------

    [ClientRpc]
    private void WaveStartClientRpc(int wave)
    {
        OnWaveStartClient?.Invoke(wave);
    }

    [ClientRpc]
    private void IntermissionClientRpc(int nextWave, float sec)
    {
        OnIntermissionClient?.Invoke(nextWave, sec);
    }

    [ClientRpc]
    private void AugmentStartClientRpc(int wave)
    {
        OnAugmentStartClient?.Invoke(wave);
    }

    private void OnPhaseChanged(WavePhase oldV, WavePhase newV)
    {
        // Debug.Log($"[WaveSync] phase {oldV} -> {newV}");
    }
    
    [ClientRpc]
    private void LaneLeftClientRpc(int laneId)
    {
        laneId = Mathf.Clamp(laneId, 0, 3);

        // Do NOT recompute MyLaneId here. Lane is fixed.
        // Only clear visuals related to the disconnected lane.
        if (RemoteLaneWorld.Instance != null)
            RemoteLaneWorld.Instance.ClearProxyMonstersByLane(laneId);

        // Clear minimap dots for that lane only
        if (MiniMapLaneRegistry.Instance != null)
        {
            MiniMapLaneRegistry.Instance.ClearLaneMonsterRenderer(laneId);
            MiniMapLaneRegistry.Instance.SetLaneActive(laneId, false);
        }

        // Rebind reporters to ensure my lane stays visible
        MiniMapLaneRegistry.Instance?.RebindAllMonsterReportersAsync().Forget();
    }
}
