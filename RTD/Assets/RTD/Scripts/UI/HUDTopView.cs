using TMPro;
using UnityEngine;

public class HUDTopView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI goldText;
    [SerializeField] private TextMeshProUGUI lifeText;
    [SerializeField] private TextMeshProUGUI waveText;

    [Header("Optional")]
    [SerializeField] private GameObject waveModifierRoot;
    [SerializeField] private TextMeshProUGUI waveModifierText;

    public void SetGold(int gold) => goldText.text = gold.ToString();
    public void SetLife(int life) => lifeText.text = life.ToString();

    public void SetWave(int current, int max)
        => waveText.text = $"{current} / {max}";

    public void SetWaveModifier(string modifier)
    {
        bool has = !string.IsNullOrWhiteSpace(modifier);
        if (waveModifierRoot != null) waveModifierRoot.SetActive(has);
        if (has && waveModifierText != null) waveModifierText.text = modifier;
    }
}