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
    private string _lastActiveSceneName = string.Empty;
    
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
        
        SceneManager.sceneLoaded += OnUnitySceneLoaded;
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
        
        HandleChatResetOnSceneTransition(scene.name);
        OnSceneBecameActive?.Invoke(scene.name);
        _lastActiveSceneName = scene.name;
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
        
        HandleChatResetOnSceneTransition(sceneName);
        OnSceneBecameActive?.Invoke(sceneName);
        _lastActiveSceneName = sceneName;
    }
    
    private void HandleChatResetOnSceneTransition(string newSceneName)
    {
        bool wasInGame = _lastActiveSceneName == gameSceneName;
        bool movedOutFromGame = newSceneName != gameSceneName;

        if (!wasInGame || !movedOutFromGame)
            return;

        ChatManager.Instance?.ClearHistory();
    }
    
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
        

        bool isSoloMultiplayer =
            mode == Mode.Multi &&
            NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsServer &&
            NetworkManager.Singleton.ConnectedClientsList.Count == 1;
        
        if (mode != Mode.Multi || isSoloMultiplayer)
        {
            mode = Mode.Single;
            _loadingGameScene = true;
            SceneManager.LoadScene(gameSceneName);
            return;
        }

        if (nm == null)
        {
            Debug.LogError("[AppFlow] NetworkManager.Singleton is null.");
            return;
        }
        
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
        
        if (result.endType == GameEndType.Win)
            AudioManager.Instance?.PlayWin();
        else
            AudioManager.Instance?.PlayLose();
        
        if (UIManager.Instance == null)
            return;

        if (mode == Mode.Single)
        {
            Time.timeScale = 0f;

            if (UIManager.Instance != null)
                UIManager.Instance.ShowResultPanel(result);

            return;
        }

        Time.timeScale = 1f;
        UIManager.Instance.ShowResultPanelMulti(
            result,
            onSpectate: () =>
            {
                if (GameRuntime.Instance != null)
                    GameRuntime.Instance.EnterSpectatorMode();
            },
            onGoTitle: () =>
            {
                GoTitle();
            }
        );
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
