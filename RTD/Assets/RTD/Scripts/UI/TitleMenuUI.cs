using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TitleMenuUI : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button btnSingle;
    [SerializeField] private Button btnMulti;
    [SerializeField] private Button btnSettings;
    [SerializeField] private Button btnQuit;

    [Header("Scenes")]
    [SerializeField] private string gameSceneName = "InGame";
    [SerializeField] private string lobbySceneName = "Lobby";

    [Header("Optional Panels")]
    [SerializeField] private GameObject settingsPanel;

    private void Awake()
    {
        if (btnSingle != null) btnSingle.onClick.AddListener(OnClickSingle);
        if (btnMulti != null) btnMulti.onClick.AddListener(OnClickMulti);
        if (btnSettings != null) btnSettings.onClick.AddListener(OnClickSettings);
        if (btnQuit != null) btnQuit.onClick.AddListener(OnClickQuit);
    }

    private void OnDestroy()
    {
        if (btnSingle != null) btnSingle.onClick.RemoveListener(OnClickSingle);
        if (btnMulti != null) btnMulti.onClick.RemoveListener(OnClickMulti);
        if (btnSettings != null) btnSettings.onClick.RemoveListener(OnClickSettings);
        if (btnQuit != null) btnQuit.onClick.RemoveListener(OnClickQuit);
    }

    private void OnClickSingle()
    {
        if (AppFlowManager.Instance != null)
        {
            AppFlowManager.Instance.StartSingleGame(); // 아래에 AppFlowManager 쪽에 추가할 함수 예시 제공
            return;
        }
    }

    private void OnClickMulti()
    {
        /*if (AppFlowManager.Instance != null)
            AppFlowManager.Instance.StartMultiLobby();
        else
            SceneManager.LoadScene(lobbySceneName);*/
        
        if (AppFlowManager.Instance == null)
        {
            Debug.LogError("[TitleMenuUI] AppFlowManager.Instance is null. Title 씬에 AppFlowManager가 존재해야 합니다.");
            return;
        }

        AppFlowManager.Instance.StartMultiLobby();
    }

    private void OnClickSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(!settingsPanel.activeSelf);
        else
            Debug.Log("SettingsPanel이 아직 없습니다. (추후 추가 가능)");
    }

    private void OnClickQuit()
    {
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
