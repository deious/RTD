using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using System.Collections.Generic;

public class AppFlowManager : MonoBehaviour
{
    public static AppFlowManager Instance { get; private set; }

    public enum Mode { Single, Multi }

    public static event Action<string> OnSceneBecameActive;
    
    [Header("Mode")]
    [SerializeField] private Mode mode = Mode.Single;

    [Header("Scenes")]
    [SerializeField] private string titleSceneName = "Title";
    [SerializeField] private string gameSceneName = "InGame";
    [SerializeField] private string lobbySceneName = "Lobby";

    private bool _endingHandled;
    private bool _loadingGameScene;
    
    public Mode CurrentMode => mode;
    public bool IsMultiMode => mode == Mode.Multi;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 싱글 모드에서만 유효한 보조용 (멀티는 NGO 이벤트로 해제)
        SceneManager.sceneLoaded += OnUnitySceneLoaded;

        // NGO 이벤트 후킹(있으면)
        HookNgoSceneEvents();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnUnitySceneLoaded;
        UnhookNgoSceneEvents();
    }

    private void OnUnitySceneLoaded(Scene scene, LoadSceneMode loadMode)
    {
        if (mode == Mode.Single && scene.name == gameSceneName)
            _loadingGameScene = false;
        
        OnSceneBecameActive?.Invoke(scene.name);
    }

    private void HookNgoSceneEvents()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || nm.SceneManager == null) return;

        nm.SceneManager.OnLoadEventCompleted -= OnNgoLoadEventCompleted;
        nm.SceneManager.OnLoadEventCompleted += OnNgoLoadEventCompleted;
    }

    private void UnhookNgoSceneEvents()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || nm.SceneManager == null) return;

        nm.SceneManager.OnLoadEventCompleted -= OnNgoLoadEventCompleted;
    }

    private void OnNgoLoadEventCompleted(string sceneName, LoadSceneMode mode,
        List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
    {
        if (sceneName == gameSceneName)
        {
            _loadingGameScene = false;

            if (clientsTimedOut != null && clientsTimedOut.Count > 0)
            {
                Debug.LogError($"[AppFlow][NGO] Load timed out. scene={sceneName} timeout=[{string.Join(",", clientsTimedOut)}]");
            }
            else
            {
                Debug.Log($"[AppFlow][NGO] Load completed. scene={sceneName} ok=[{string.Join(",", clientsCompleted)}]");
            }
        }
        
        OnSceneBecameActive?.Invoke(sceneName);
    }

    // ---------------------------
    // Scene Load
    // ---------------------------

    public void LoadGameScene()
    {
        if (_loadingGameScene) return;

        if (mode == Mode.Single)
        {
            _loadingGameScene = true;
            SceneManager.LoadScene(gameSceneName);
            return;
        }

        LoadGameSceneServerOnly();
    }

    public void LoadGameSceneServerOnly()
    {
        if (_loadingGameScene) return;

        var nm = NetworkManager.Singleton;

        if (mode != Mode.Multi)
        {
            // (사실상 싱글인데 여길 타면 이상하니) 안전 처리
            _loadingGameScene = true;
            SceneManager.LoadScene(gameSceneName);
            return;
        }

        if (nm == null)
        {
            Debug.LogError("[AppFlow] NetworkManager.Singleton is null.");
            return;
        }

        // ✅ 멀티는 서버/호스트만 NGO로 씬 전환
        if (nm.IsServer && nm.IsListening)
        {
            _loadingGameScene = true;
            nm.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
        }
        else
        {
            Debug.LogWarning($"[AppFlow] Not server/host or not listening. IsServer={nm.IsServer} IsListening={nm.IsListening}");
        }
    }

    // ---------------------------
    // Single Flow
    // ---------------------------

    public void StartSingleGame()
    {
        mode = Mode.Single;
        _endingHandled = false;
        Time.timeScale = 1f;

        _loadingGameScene = true;
        SceneManager.LoadScene(gameSceneName);
    }

    public void RestartSingleGame()
    {
        mode = Mode.Single;
        _endingHandled = false;
        Time.timeScale = 1f;

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();

        _loadingGameScene = true;
        SceneManager.LoadScene(gameSceneName);
    }

    public void GoTitle()
    {
        _endingHandled = false;
        Time.timeScale = 1f;

        if (mode == Mode.Multi && NetworkManager.Singleton != null)
        {
            if (NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();
        }

        _loadingGameScene = false;
        SceneManager.LoadScene(titleSceneName);
    }

    public void OnGameEnd(GameResult result)
    {
        if (_endingHandled) return;
        _endingHandled = true;

        if (mode == Mode.Single)
        {
            Time.timeScale = 0f;

            if (UIManager.Instance != null)
                UIManager.Instance.ShowResultPanel(result);
        }

        // 멀티 결과처리(나중)
    }

    // ---------------------------
    // Multi Flow
    // ---------------------------

    public void StartMultiLobby()
    {
        mode = Mode.Multi;
        _endingHandled = false;
        Time.timeScale = 1f;

        _loadingGameScene = false;
        SceneManager.LoadScene(lobbySceneName);
    }
    
    public async UniTask StartMultiGameFromHostAsync(int expectedPlayers, float timeoutSec = 15f)
    {
        mode = Mode.Multi;
        _endingHandled = false;
        Time.timeScale = 1f;

        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer || !nm.IsListening)
        {
            Debug.LogWarning($"[AppFlow] StartMultiGameFromHostAsync called but not host/listening. IsServer={nm != null && nm.IsServer} IsListening={nm != null && nm.IsListening}");
            return;
        }

        expectedPlayers = Mathf.Max(1, expectedPlayers);

        if (_loadingGameScene)
        {
            Debug.LogWarning("[AppFlow] Already loading game scene.");
            return;
        }

        float end = Time.realtimeSinceStartup + timeoutSec;

        while (Time.realtimeSinceStartup < end)
        {
            int connected = nm.ConnectedClientsList.Count;

            if (connected >= expectedPlayers)
            {
                LoadGameSceneServerOnly();
                return;
            }

            await UniTask.Delay(200, ignoreTimeScale: true);
        }

        Debug.LogError("[AppFlow] Timeout waiting all clients. Game start aborted.");
    }
}
