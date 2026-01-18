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

    private void Awake()
    {
        Instance = this;
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
        waveText.text = $"Wave : {curr}/{max}";

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
    
    public void UpdateWaveMonsterCount(int remaining, int total)
    {
        if (waveMonsterCountText == null)
            return;
        
        if (total <= 0)
        {
            waveMonsterCountText.text = "";
            return;
        }

        remaining = Mathf.Max(0, remaining);
        total = Mathf.Max(0, total);

        waveMonsterCountText.text = $"처치 : {remaining}/{total}";
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
}