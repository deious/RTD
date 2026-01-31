using System;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay; // RelayServerData를 위해 필수
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public sealed class RelayConnector : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NetworkManager networkManager;

    [Header("Relay Settings")]
    [Tooltip("보통 dtls 권장.")]
    [SerializeField] private string relayProtocol = "dtls";

    [Tooltip("클라이언트 연결 대기 타임아웃(초)")]
    [SerializeField] private float connectTimeoutSec = 15f;

    [Tooltip("최대 접속 인원 (Host 제외)")]
    [SerializeField] private int maxConnections = 3;

    private UnityTransport _utp;
    
    public static RelayConnector Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        if (networkManager == null)
            networkManager = NetworkManager.Singleton;

        if (networkManager == null)
        {
            Debug.LogError("[RelayConnector] NetworkManager가 없습니다.");
            return;
        }

        _utp = networkManager.NetworkConfig.NetworkTransport as UnityTransport;
        if (_utp == null)
        {
            Debug.LogError("[RelayConnector] Transport가 UnityTransport가 아닙니다.");
        }
    }

    public async UniTask EnsureServicesReadyAsync()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
            await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        
        Debug.Log($"[UGS] cloudProjectId={Application.cloudProjectId}");
        Debug.Log($"[UGS] servicesState={Unity.Services.Core.UnityServices.State}");
    }

    // 기존: public async UniTask<string> StartHostWithRelayAsync()
    // 수정: int maxConn 매개변수 추가
    public async UniTask<string> StartHostWithRelayAsync(int maxConn)
    {
        ValidateReady();
        await EnsureServicesReadyAsync();
        await ShutdownIfListeningAsync();

        // 1) Allocation 생성 (전달받은 maxConn 사용)
        // Relay 서비스는 호스트를 제외한 '추가 접속 인원'을 인자로 받습니다.
        Allocation alloc = await RelayService.Instance.CreateAllocationAsync(maxConn);

        // 2) Join Code 가져오기
        string joinCode = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);

        // 3) RelayServerData 직접 조립
        var serverData = new RelayServerData(
            alloc.RelayServer.IpV4,
            (ushort)alloc.RelayServer.Port,
            alloc.AllocationIdBytes,
            alloc.ConnectionData,
            alloc.ConnectionData, 
            alloc.Key,
            relayProtocol == "dtls"
        );

        _utp.SetRelayServerData(serverData);

        if (!networkManager.StartHost())
            throw new Exception("[RelayConnector] Host 시작 실패");

        Debug.Log($"[RelayConnector] Host Started. Code: {joinCode}");
        return joinCode;
    }

    public async UniTask StartClientWithRelayAsync(string joinCode)
    {
        ValidateReady();
        if (string.IsNullOrWhiteSpace(joinCode))
            throw new ArgumentException("Join Code가 비어있습니다.");

        await EnsureServicesReadyAsync();
        await ShutdownIfListeningAsync();

        // 1) Join Allocation
        JoinAllocation joinAlloc = await RelayService.Instance.JoinAllocationAsync(joinCode);

        // ✅ 2) RelayServerData 직접 조립 (Unity 6 생성자 이슈 해결)
        // Client는 본인의 ConnectionData와 Host의 ConnectionData를 구분해서 전달합니다.
        var serverData = new RelayServerData(
            joinAlloc.RelayServer.IpV4,
            (ushort)joinAlloc.RelayServer.Port,
            joinAlloc.AllocationIdBytes,
            joinAlloc.ConnectionData,
            joinAlloc.HostConnectionData, 
            joinAlloc.Key,
            relayProtocol == "dtls"
        );

        _utp.SetRelayServerData(serverData);

        if (!networkManager.StartClient())
            throw new Exception("[RelayConnector] Client 시작 실패");

        await WaitForLocalClientConnectedAsync(connectTimeoutSec);
        Debug.Log("[RelayConnector] Client Connected.");
    }

    public async UniTask ShutdownAsync()
    {
        if (networkManager != null && networkManager.IsListening)
        {
            networkManager.Shutdown();
            await UniTask.DelayFrame(2);
        }
    }

    private void ValidateReady()
    {
        if (networkManager == null || _utp == null)
            throw new Exception("[RelayConnector] 필수 컴포넌트가 설정되지 않았습니다.");
    }

    private async UniTask ShutdownIfListeningAsync()
    {
        if (networkManager.IsListening)
        {
            networkManager.Shutdown();
            await UniTask.DelayFrame(2);
        }
    }

    private async UniTask WaitForLocalClientConnectedAsync(float timeoutSec)
    {
        var tcs = new UniTaskCompletionSource();

        void OnConnected(ulong clientId)
        {
            if (clientId == networkManager.LocalClientId)
            {
                Unsubscribe();
                tcs.TrySetResult();
            }
        }

        void OnDisconnected(ulong clientId)
        {
            if (clientId == networkManager.LocalClientId)
            {
                Unsubscribe();
                tcs.TrySetException(new Exception("연결 전 해제됨"));
            }
        }

        void Unsubscribe()
        {
            networkManager.OnClientConnectedCallback -= OnConnected;
            networkManager.OnClientDisconnectCallback -= OnDisconnected;
        }

        networkManager.OnClientConnectedCallback += OnConnected;
        networkManager.OnClientDisconnectCallback += OnDisconnected;

        var timeoutTask = UniTask.Delay(TimeSpan.FromSeconds(timeoutSec));
        var completed = await UniTask.WhenAny(tcs.Task, timeoutTask);

        if (completed == 1)
        {
            Unsubscribe();
            if (networkManager.IsListening) networkManager.Shutdown();
            throw new TimeoutException($"Client 연결 타임아웃 ({timeoutSec}s)");
        }

        await tcs.Task;
    }
}