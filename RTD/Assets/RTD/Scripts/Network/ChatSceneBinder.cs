using Unity.Netcode;
using UnityEngine;

public class ChatSceneBinder : MonoBehaviour
{
    [SerializeField] private string lobbySceneName = "Lobby";
    [SerializeField] private string gameSceneName = "InGame";

    [Header("Chat UI Root (Canvas or Panel root)")]
    [SerializeField] private GameObject chatRoot;

    private void Awake()
    {
        if (chatRoot == null) 
            chatRoot = gameObject;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        AppFlowManager.OnSceneBecameActive += HandleScene;
    }

    private void OnDisable()
    {
        AppFlowManager.OnSceneBecameActive -= HandleScene;
    }

    private void HandleScene(string sceneName)
    {
        bool isChatScene = (sceneName == lobbySceneName || sceneName == gameSceneName);
        bool isMulti = (AppFlowManager.Instance != null && AppFlowManager.Instance.IsMultiMode);
        var nm = NetworkManager.Singleton;
        bool isOnline = nm != null && nm.IsListening
                                   && ChatNetworkBridge.Instance != null
                                   && ChatNetworkBridge.Instance.IsSpawned;

        chatRoot.SetActive(isChatScene && isMulti && isOnline);
    }
}