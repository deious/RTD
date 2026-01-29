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

    [Header("Result Theme Colors")]
    [SerializeField] private Color winHeaderColor = new Color(0.75f, 0.93f, 0.86f, 1f);
    [SerializeField] private Color loseHeaderColor = new Color(0.95f, 0.76f, 0.74f, 1f);
    [SerializeField] private Color winDetailColor = new Color(0.85f, 0.96f, 0.92f, 1f);
    [SerializeField] private Color loseDetailColor = new Color(0.97f, 0.85f, 0.80f, 1f);

    [SerializeField] private Color titleTextColor = new Color(1f, 0.96f, 0.91f, 1f);
    [SerializeField] private Color detailTextColor = new Color(0.95f, 0.93f, 0.89f, 1f);

    private void Awake()
    {
        Instance = this;
        
        if (btnRestart != null)
        {
            btnRestart.onClick.RemoveAllListeners();
            btnRestart.onClick.AddListener(OnClickRestart);
        }

        if (btnTitle != null)
        {
            btnTitle.onClick.RemoveAllListeners();
            btnTitle.onClick.AddListener(OnClickGoTitle);
        }
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
            windowImage.color = new Color(0.95f, 0.90f, 0.82f, 1f); // 베이지 살짝 눌린 톤
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

}