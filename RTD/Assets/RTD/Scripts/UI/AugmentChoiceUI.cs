using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AugmentChoiceUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject root;

    [Header("Option 1")]
    [SerializeField] private Button button1;
    [SerializeField] private TMP_Text title1;
    [SerializeField] private TMP_Text desc1;

    [Header("Option 2")]
    [SerializeField] private Button button2;
    [SerializeField] private TMP_Text title2;
    [SerializeField] private TMP_Text desc2;

    [Header("Option 3")]
    [SerializeField] private Button button3;
    [SerializeField] private TMP_Text title3;
    [SerializeField] private TMP_Text desc3;

    private AugmentSO[] _options;
    private System.Action<AugmentSO> _onPick;

    private void Awake()
    {
        if (root == null) root = gameObject;
        Hide();

        if (button1 != null) button1.onClick.AddListener(() => Pick(0));
        if (button2 != null) button2.onClick.AddListener(() => Pick(1));
        if (button3 != null) button3.onClick.AddListener(() => Pick(2));
    }

    public void Show(AugmentSO[] options, System.Action<AugmentSO> onPick)
    {
        _options = options;
        _onPick = onPick;

        ApplyToUI(0, title1, desc1);
        ApplyToUI(1, title2, desc2);
        ApplyToUI(2, title3, desc3);

        root.SetActive(true);
    }

    public void Hide()
    {
        if (root != null)
            root.SetActive(false);
    }

    private void ApplyToUI(int idx, TMP_Text title, TMP_Text desc)
    {
        AugmentSO a = (_options != null && idx >= 0 && idx < _options.Length) ? _options[idx] : null;

        if (title != null) title.text = (a != null) ? a.title : "-";
        if (desc != null)  desc.text  = (a != null) ? a.desc : "";
    }

    private void Pick(int idx)
    {
        if (_options == null || idx < 0 || idx >= _options.Length)
            return;

        AugmentSO a = _options[idx];
        if (a == null)
            return;

        _onPick?.Invoke(a);
    }
}