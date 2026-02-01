using System;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace RTD.Scripts.Network
{
    public sealed class RelayConnector : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private NetworkManager networkManager;

        [Header("Relay Settings")]
        [Tooltip("dtls 권장.")]
        [SerializeField] private string relayProtocol = "dtls";

        [Tooltip("클라이언트 연결 대기 타임아웃(초)")]
        [SerializeField] private float connectTimeoutSec = 15f;
        
        [Header("Chat Settings")]
        [SerializeField] private ChatNetworkBridge chatNetworkBridgePrefab;

        private UnityTransport _utp;

        public static RelayConnector Instance { get; private set; }

        private bool IsDtls =>
            relayProtocol != null &&
            relayProtocol.Equals("dtls", StringComparison.OrdinalIgnoreCase);

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
                return;
            }
        }
        
        private void SpawnChatNetworkBridgeIfNeeded()
        {
            if (!networkManager.IsServer)
                return;

            if (ChatNetworkBridge.Instance != null &&
                ChatNetworkBridge.Instance.IsSpawned)
                return;

            if (chatNetworkBridgePrefab == null)
            {
                Debug.LogError("[RelayConnector] chatNetworkBridgePrefab is null (Inspector에 프리팹 할당 필요)");
                return;
            }

            var bridge = Instantiate(chatNetworkBridgePrefab);
            var no = bridge.GetComponent<NetworkObject>();
            if (no == null)
            {
                Debug.LogError("[RelayConnector] ChatNetworkBridge prefab에 NetworkObject가 없음");
                Destroy(bridge.gameObject);
                return;
            }

            no.Spawn(true);
            Debug.Log("[RelayConnector] ChatNetworkBridge spawned");
        }

        public async UniTask EnsureServicesReadyAsync()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            Debug.Log($"[UGS] cloudProjectId={Application.cloudProjectId}");
            Debug.Log($"[UGS] servicesState={UnityServices.State}");
        }

        public async UniTask<string> StartHostWithRelayAsync(int maxConn)
        {
            ValidateReady();
            await EnsureServicesReadyAsync();
            await ShutdownIfListeningAsync();
            
            maxConn = Mathf.Max(1, maxConn);
            
            Allocation alloc = await RelayService.Instance.CreateAllocationAsync(maxConn);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);
            
            var relayServerData = alloc.ToRelayServerData(relayProtocol);
            _utp.SetRelayServerData(relayServerData);

            Debug.Log(
                $"[RelayConnector][Host] proto={relayProtocol} " +
                $"ip={alloc.RelayServer.IpV4}:{alloc.RelayServer.Port} " +
                $"allocIdBytes={alloc.AllocationIdBytes?.Length ?? 0} " +
                $"connData={alloc.ConnectionData?.Length ?? 0} " +
                $"key={alloc.Key?.Length ?? 0} " +
                $"maxConn={maxConn} joinCode={joinCode}"
            );

            if (!networkManager.StartHost())
                throw new Exception("[RelayConnector] Host 시작 실패");

            SpawnChatNetworkBridgeIfNeeded();
            
            if (AppFlowManager.Instance != null)
            {
                int expectedPlayers = 1 + maxConn;
                AppFlowManager.Instance.StartMultiGameFromHostAsync(expectedPlayers).Forget();
                Debug.Log($"[RelayConnector][Host] Start waiting clients... expectedPlayers={expectedPlayers}");
            }
            else
            {
                Debug.LogWarning("[RelayConnector][Host] AppFlowManager.Instance is null. Can't auto start scene.");
            }

            return joinCode;
        }

        public async UniTask StartClientWithRelayAsync(string joinCode)
        {
            ValidateReady();
            if (string.IsNullOrWhiteSpace(joinCode))
                throw new ArgumentException("Join Code가 비어있습니다.");

            await EnsureServicesReadyAsync();
            await ShutdownIfListeningAsync();

            // 1) JoinAllocation
            JoinAllocation joinAlloc = await RelayService.Instance.JoinAllocationAsync(joinCode.Trim());

            // ✅ 핵심 변경: Unity6/패키지 호환용 변환 API 사용
            var relayServerData = joinAlloc.ToRelayServerData(relayProtocol);
            _utp.SetRelayServerData(relayServerData);

            Debug.Log(
                $"[RelayConnector][Client] proto={relayProtocol} " +
                $"ip={joinAlloc.RelayServer.IpV4}:{joinAlloc.RelayServer.Port} " +
                $"allocIdBytes={joinAlloc.AllocationIdBytes?.Length ?? 0} " +
                $"connData={joinAlloc.ConnectionData?.Length ?? 0} " +
                $"hostConnData={joinAlloc.HostConnectionData?.Length ?? 0} " +
                $"key={joinAlloc.Key?.Length ?? 0} " +
                $"joinCode={joinCode}"
            );

            if (!networkManager.StartClient())
                throw new Exception("[RelayConnector] Client 시작 실패");

            await WaitForLocalClientConnectedAsync(connectTimeoutSec);
            Debug.Log("[RelayConnector] Client Connected.");
        }

        public async UniTask ShutdownAsync() => await ShutdownIfListeningAsync();

        private void ValidateReady()
        {
            if (networkManager == null || _utp == null)
                throw new Exception("[RelayConnector] 필수 컴포넌트가 설정되지 않았습니다.");
        }

        private async UniTask ShutdownIfListeningAsync()
        {
            if (networkManager != null && networkManager.IsListening)
            {
                networkManager.Shutdown();

                float start = Time.realtimeSinceStartup;
                while (networkManager.IsListening)
                {
                    if (Time.realtimeSinceStartup - start > 3f)
                        break;
                    await UniTask.Yield(PlayerLoopTiming.Update);
                }

                await UniTask.DelayFrame(1);
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

            var timeoutTask = UniTask.Delay(TimeSpan.FromSeconds(timeoutSec), ignoreTimeScale: true);
            var completed = await UniTask.WhenAny(tcs.Task, timeoutTask);

            if (completed == 1)
            {
                Unsubscribe();
                if (networkManager.IsListening)
                    networkManager.Shutdown();

                throw new TimeoutException($"Client 연결 타임아웃 ({timeoutSec}s)");
            }

            await tcs.Task;
        }
    }
}
