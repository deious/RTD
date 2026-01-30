using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

public class AppFlowManager : MonoBehaviour
{
    public static AppFlowManager Instance { get; private set; }

    public enum Mode { Single, Multi }

    [Header("Mode")]
    [SerializeField] private Mode mode = Mode.Single;

    [Header("Scenes")]
    [SerializeField] private string titleSceneName = "Title";
    [SerializeField] private string gameSceneName = "InGame";
    [SerializeField] private string lobbySceneName = "Lobby";

    private bool _endingHandled;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    
    public void LoadGameScene()
    {
        if (mode == Mode.Single)
        {
            SceneManager.LoadScene(gameSceneName);
            return;
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
        }
        else
        {
            Debug.LogWarning("[AppFlow] LoadGameScene called but not host/server.");
        }
    }
    
    public void StartSingleGame()
    {
        mode = Mode.Single;
        _endingHandled = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(gameSceneName);
    }
    
    public void RestartSingleGame()
    {
        mode = Mode.Single;
        _endingHandled = false;
        Time.timeScale = 1f;

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();

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

            return;
        }

        // 멀티(나중):
        // - 상대방 종료 기다림
        // - reachedWave 비교로 승패 확정
        // - 결과창 띄우기
        // - 로비 씬 이동
    }
    
    public void StartMultiLobby()
    {
        mode = Mode.Multi;
        _endingHandled = false;
        Time.timeScale = 1f;
        
        SceneManager.LoadScene(lobbySceneName);
    }
    
    public void StartMultiGameFromHost()
    {
        if (mode != Mode.Multi)
            mode = Mode.Multi;

        _endingHandled = false;
        Time.timeScale = 1f;

        LoadGameScene();
    }
}