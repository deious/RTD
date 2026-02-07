using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("Top Left")]
    [SerializeField] private TextMeshProUGUI goldText;
    [SerializeField] private TextMeshProUGUI lifeText;
    [SerializeField] private TextMeshProUGUI waveText;
    [SerializeField] private TextMeshProUGUI waveModifiersText;
    [SerializeField] private TextMeshProUGUI waveMonsterCountText;
    
    [Header("Top Center")]
    [SerializeField] private TextMeshProUGUI nextWaveTimerText;
    
    [Header("Result Panel")]
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private UnityEngine.UI.Image dimmerImage;
    [SerializeField] private UnityEngine.UI.Image windowImage;
    [SerializeField] private UnityEngine.UI.Image headerBgImage;
    [SerializeField] private UnityEngine.UI.Image detailBgImage;
    [SerializeField] private TMPro.TextMeshProUGUI resultTitleText;
    [SerializeField] private TMPro.TextMeshProUGUI resultDetailText;
    
    [Header("Result Buttons")]
    [SerializeField] private Button btnRestart;
    [SerializeField] private Button btnTitle;
    [SerializeField] private TextMeshProUGUI btnRestartLabel;
    [SerializeField] private TextMeshProUGUI btnTitleLabel; 

    [Header("Result Theme Colors")]
    [SerializeField] private Color winHeaderColor = new Color(0.75f, 0.93f, 0.86f, 1f);
    [SerializeField] private Color loseHeaderColor = new Color(0.95f, 0.76f, 0.74f, 1f);
    [SerializeField] private Color winDetailColor = new Color(0.85f, 0.96f, 0.92f, 1f);
    [SerializeField] private Color loseDetailColor = new Color(0.97f, 0.85f, 0.80f, 1f);

    [SerializeField] private Color titleTextColor = new Color(1f, 0.96f, 0.91f, 1f);
    [SerializeField] private Color detailTextColor = new Color(0.95f, 0.93f, 0.89f, 1f);
    
    [Header("Input Blocker")]
    [SerializeField] private GameObject worldInputBlocker;
    
    private System.Action _restartAction;
    private System.Action _titleAction;
    
    private bool _pauseOpen;
    private bool _resultLocked;
    private float _prevTimeScale = 1f;

    private void Awake()
    {
        Instance = this;
        
        if (btnRestartLabel == null && btnRestart != null)
            btnRestartLabel = btnRestart.GetComponentInChildren<TextMeshProUGUI>(true);

        if (btnTitleLabel == null && btnTitle != null)
            btnTitleLabel = btnTitle.GetComponentInChildren<TextMeshProUGUI>(true);

        btnRestart.onClick.RemoveAllListeners();
        btnRestart.onClick.AddListener(() =>
        {
            Time.timeScale = 1f;
            ForceCloseResultPanel();
            _restartAction?.Invoke();
        });

        if (btnTitle != null)
        {
            btnTitle.onClick.RemoveAllListeners();
            btnTitle.onClick.AddListener(() =>
            {
                Time.timeScale = 1f;
                ForceCloseResultPanel();
                _titleAction?.Invoke();
            });
        }
        
        _pauseOpen = false;
        _resultLocked = false;
        
        if (resultPanel != null) 
            resultPanel.SetActive(false);
        SetWorldBlock(false);
        
        _restartAction = OnClickRestart;
        _titleAction = OnClickGoTitle;
    }
    
    private void ConfigureResultButtonsSingle()
    {
        if (btnRestartLabel != null) btnRestartLabel.text = "다시하기";
        if (btnTitleLabel != null) btnTitleLabel.text = "타이틀로";

        _restartAction = OnClickRestart;
        _titleAction = OnClickGoTitle;

        if (btnRestart != null) btnRestart.gameObject.SetActive(true);
        if (btnTitle != null) btnTitle.gameObject.SetActive(true);
    }

    private void SetWorldBlock(bool on)
    {
        Debug.Log($"[WorldBlock] on={on} time={Time.time:F2}");
        if (worldInputBlocker != null)
            worldInputBlocker.SetActive(on);

        UIState.SetBlockWorldInput(on);
    }
    
    private void ApplyPauseUI(bool isMulti)
    {
        // resultPanel 켜져있다는 전제
        if (resultTitleText != null)
        {
            resultTitleText.text = isMulti ? "메뉴" : "일시정지";
            resultTitleText.color = titleTextColor;
        }

        if (resultDetailText != null)
        {
            resultDetailText.text = "";
            resultDetailText.color = detailTextColor;
        }

        if (headerBgImage != null) headerBgImage.color = winHeaderColor;
        if (detailBgImage != null) detailBgImage.color = winDetailColor;
        if (dimmerImage != null) dimmerImage.color = new Color(0f, 0f, 0f, 0.75f);
        if (windowImage != null) windowImage.color = new Color(0.95f, 0.90f, 0.82f, 1f);

        if (btnRestartLabel != null) btnRestartLabel.text = "종료";
        if (btnTitleLabel != null) btnTitleLabel.text = "타이틀로";

        _restartAction = () => Application.Quit();
        _titleAction = () =>
        {
            if (AppFlowManager.Instance != null)
                AppFlowManager.Instance.GoTitle();
        };

        if (btnRestart != null) btnRestart.gameObject.SetActive(true);
        if (btnTitle != null) btnTitle.gameObject.SetActive(true);
    }
    
    public void UpdateGold(int value)
    {
        goldText.text = $"골드 : {value}";
    }

    public void UpdateLife(int value)
    {
        lifeText.text = $"목숨 : {value}";
    }

    // 추후 웨이브 표기만 필요할 경우 사용하기 위해 남겨둠
    public void UpdateWave(int curr, int max)
    {
        waveText.text = $"라운드 : {curr}/{max}";
    }
    
    public void UpdateWave(int curr, int max, string modifiersLabel)
    {
        waveText.text = $"라운드 : {curr}/{max}";

        if (waveModifiersText == null)
            return;

        if (string.IsNullOrEmpty(modifiersLabel) || modifiersLabel == "None")
        {
            waveModifiersText.text = "Wave Mod: None";
        }
        else
        {
            waveModifiersText.text = $"Wave Mod: {modifiersLabel}";
        }
    }
    
    public void UpdateWaveMonsterCount(int killed, int total)
    {
        if (waveMonsterCountText == null)
            return;
        
        if (total <= 0)
        {
            waveMonsterCountText.text = "";
            return;
        }

        killed = Mathf.Max(0, killed);
        total = Mathf.Max(0, total);

        waveMonsterCountText.text = $"처치 : {killed}/{total}";
    }
    
    public void UpdateNextWaveTimer(int secondsRemaining)
    {
        if (nextWaveTimerText == null)
            return;

        if (secondsRemaining <= 0)
        {
            nextWaveTimerText.text = "";
            return;
        }

        nextWaveTimerText.text = $"남은 시간 : {secondsRemaining}";
    }
    public void ShowResultPanel(GameResult result)
    {
        if (resultPanel != null)
            resultPanel.SetActive(true);

        SetWorldBlock(true);
        
        bool isWin = (result.endType == GameEndType.Win);

        if (resultTitleText != null)
        {
            resultTitleText.text = isWin ? "승리" : "패배";
            resultTitleText.color = titleTextColor;
        }

        if (resultDetailText != null)
        {
            resultDetailText.text = $"최종 라운드 : {result.reachedWave}";
            resultDetailText.color = detailTextColor;
        }

        if (headerBgImage != null)
            headerBgImage.color = isWin ? winHeaderColor : loseHeaderColor;

        if (detailBgImage != null)
            detailBgImage.color = isWin ? winDetailColor : loseDetailColor;
        
        if (dimmerImage != null)
            dimmerImage.color = new Color(0f, 0f, 0f, 0.75f);

        if (windowImage != null)
            windowImage.color = new Color(0.95f, 0.90f, 0.82f, 1f); 
        
        ConfigureResultButtonsSingle();
        
        _resultLocked = true;
        _pauseOpen = false;
    }


    public void OnClickRestart()
    {
        Time.timeScale = 1f;
        
        if (AppFlowManager.Instance != null)
            AppFlowManager.Instance.RestartSingleGame();
    }

    public void OnClickGoTitle()
    {
        Time.timeScale = 1f;
        
        if (AppFlowManager.Instance != null)
            AppFlowManager.Instance.GoTitle();
    }

    public void ShowResultPanelMulti(GameResult result, System.Action onSpectate, System.Action onGoTitle)
    {
        if (resultPanel != null)
            resultPanel.SetActive(true);
        
        SetWorldBlock(true);

        bool isWin = (result.endType == GameEndType.Win);

        if (resultTitleText != null)
        {
            resultTitleText.text = isWin ? "승리" : "패배";
            resultTitleText.color = titleTextColor;
        }

        if (resultDetailText != null)
        {
            resultDetailText.text = $"최종 라운드 : {result.reachedWave}";
            resultDetailText.color = detailTextColor;
        }

        if (headerBgImage != null)
            headerBgImage.color = isWin ? winHeaderColor : loseHeaderColor;

        if (detailBgImage != null)
            detailBgImage.color = isWin ? winDetailColor : loseDetailColor;

        if (dimmerImage != null)
            dimmerImage.color = new Color(0f, 0f, 0f, 0.75f);

        if (windowImage != null)
            windowImage.color = new Color(0.95f, 0.90f, 0.82f, 1f);

        if (btnRestartLabel != null) btnRestartLabel.text = "관전하기";
        if (btnTitleLabel != null) btnTitleLabel.text = "타이틀로";

        _restartAction = onSpectate;
        _titleAction = onGoTitle;

        if (btnRestart != null) btnRestart.gameObject.SetActive(true);
        if (btnTitle != null) btnTitle.gameObject.SetActive(true);
        
        _resultLocked = true;
        _pauseOpen = false;
    }
    
    public void SetSpectating(bool spectating)
    {
        var buildHud = FindFirstObjectByType<BuildHUDController>();
        if (buildHud != null) buildHud.SetSpectating(spectating);

        var ctx = FindFirstObjectByType<ContextUIController>();
        if (ctx != null && spectating) ctx.Hide();
    }

    public void TogglePausePanel()
    {
        Debug.Log($"[TogglePausePanel] pauseOpen(before)={_pauseOpen} locked={_resultLocked} time={Time.time:F2}");

        if (_resultLocked)
            return;

        if (_pauseOpen) 
            ClosePause();
        else 
            OpenPause();
    }
    
    public void ForceCloseResultPanel()
    {
        _pauseOpen = false;

        if (resultPanel != null)
            resultPanel.SetActive(false);
        
        SetWorldBlock(false);
    }
    
    public void OpenPause()
    {
        if (_resultLocked) return;
        if (_pauseOpen) return;

        bool isMulti = (AppFlowManager.Instance != null && AppFlowManager.Instance.IsMultiMode);

        _pauseOpen = true;

        if (!isMulti)
        {
            _prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        if (resultPanel != null)
            resultPanel.SetActive(true);

        SetWorldBlock(true);

        ApplyPauseUI(isMulti);
    }

    public void ClosePause()
    {
        if (!_pauseOpen) return;

        bool isMulti = (AppFlowManager.Instance != null && AppFlowManager.Instance.IsMultiMode);

        _pauseOpen = false;

        if (!isMulti)
            Time.timeScale = _prevTimeScale;

        if (resultPanel != null)
            resultPanel.SetActive(false);

        SetWorldBlock(false);
    }
}