using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI goldText;
    [SerializeField] private TextMeshProUGUI lifeText;
    [SerializeField] private TextMeshProUGUI waveText;
    [SerializeField] private TextMeshProUGUI waveModifiersText;

    private void Awake()
    {
        Instance = this;
    }

    public void UpdateGold(int value)
    {
        goldText.text = $"Gold : {value}";
    }

    public void UpdateLife(int value)
    {
        lifeText.text = $"Life : {value}";
    }

    // 추후 웨이브 표기만 필요할 경우 사용하기 위해 남겨둠
    public void UpdateWave(int curr, int max)
    {
        waveText.text = $"Wave : {curr}/{max}";
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
}