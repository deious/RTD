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

    public void UpdateWave(int curr, int max)
    {
        waveText.text = $"Wave : {curr}/{max}";
    }
}