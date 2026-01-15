using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ContextUIController : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject root;

    [Header("Panels")]
    [SerializeField] private GameObject towerPanel;
    [SerializeField] private GameObject buildPanel;

    [Header("Tower Texts")]
    [SerializeField] private TextMeshProUGUI txtName;
    [SerializeField] private TextMeshProUGUI txtGrade;

    [Header("Trait (optional)")]
    [SerializeField] private GameObject traitRow;          // ★ Trait 라인 전체
    [SerializeField] private TextMeshProUGUI txtTrait;

    [Header("Stats")]
    [SerializeField] private TextMeshProUGUI txtStats;

    [Header("Build Texts")]
    [SerializeField] private TextMeshProUGUI txtBuildTitle;
    [SerializeField] private TextMeshProUGUI txtBuildChance;
    [SerializeField] private TextMeshProUGUI txtBuildMinCost;
    [SerializeField] private TextMeshProUGUI txtBuildHint;

    [Header("Buttons")]
    [SerializeField] private Button btnSell;
    [SerializeField] private Button btnCancel;

    private TowerBase _current;

    private void Awake()
    {
        Hide();
    }

    public void ShowTower(TowerBase tower)
    {
        Debug.Log($"[ContextUI] ShowTower called. tower={(tower? tower.name : "null")}");
        _current = tower;
        if (tower == null)
        {
            Hide();
            return;
        }

        SetRoot(true);
        SetModeTower();
        
        TowerData d = tower.GetData();
        if (d != null)
        {
            if (txtName != null)  txtName.text = string.IsNullOrEmpty(d.towerId) ? "Tower" : d.towerId;
            if (txtGrade != null) txtGrade.text = d.grade.ToString();
        }
        else
        {
            if (txtName != null)  txtName.text = "Tower";
            if (txtGrade != null) txtGrade.text = "-";
        }
        
        TowerTraitSO trait = TryGetTrait(tower);
        bool hasTrait = trait != null;
        if (traitRow != null) traitRow.SetActive(hasTrait);
        if (txtTrait != null && hasTrait)
            txtTrait.text = $"{trait.type} {trait.tier}";
        
        if (txtStats != null)
            txtStats.text = BuildStatsText(tower);
        
        if (btnSell != null)
        {
            Debug.Log("[ContextUI] Bind Sell button listener");
            btnSell.gameObject.SetActive(true);
            btnSell.onClick.RemoveAllListeners();
            btnSell.onClick.AddListener(OnClickSell);
        }
    }

    public void ShowBuild(int minCost, int buildLevel)
    {
        _current = null;

        SetRoot(true);
        SetModeBuild();

        if (txtBuildTitle != null)
            txtBuildTitle.text = $"Build Lv. {buildLevel}";

        if (txtBuildChance != null && TowerManager.Instance != null)
            txtBuildChance.text = TowerManager.Instance.GetBuildLevelChanceLabel();

        if (txtBuildMinCost != null)
            txtBuildMinCost.text = $"최소 비용: {minCost}";

        if (txtBuildHint != null)
            txtBuildHint.text = "타일을 클릭해 타워를 설치하세요";

        if (btnCancel != null)
        {
            btnCancel.gameObject.SetActive(true);
            btnCancel.onClick.RemoveAllListeners();
            btnCancel.onClick.AddListener(OnClickCancel);
        }
        
        ForceLayoutRebuild();
    }


    public void Hide()
    {
        _current = null;
        SetRoot(false);
        
        if (btnSell != null) btnSell.onClick.RemoveAllListeners();
        if (btnCancel != null) btnCancel.onClick.RemoveAllListeners();
    }

    private void SetRoot(bool on)
    {
        if (root != null) root.SetActive(on);
    }

    private void SetModeTower()
    {
        if (towerPanel != null) towerPanel.SetActive(true);
        if (buildPanel != null) buildPanel.SetActive(false);
    }

    private void SetModeBuild()
    {
        if (towerPanel != null) towerPanel.SetActive(false);
        if (buildPanel != null) buildPanel.SetActive(true);
    }

    private void OnClickSell()
    {
        Debug.Log($"[ContextUI] Sell clicked. current={(_current? _current.name : "null")}");
        if (_current == null) return;

        if (TowerManager.Instance != null)
        {
            TowerManager.Instance.TrySellTower(_current);
        }

        Hide();
    }

    private void OnClickCancel()
    {
        if (TowerManager.Instance != null)
            TowerManager.Instance.CancelPlaceMode();
        
        Hide();
    }

    private TowerTraitSO TryGetTrait(TowerBase tower)
    {
        return tower != null ? tower.RuntimeTrait : null;
    }

    private string BuildStatsText(TowerBase tower)
    {
        if (tower == null) return "-";

        float aspd = (tower.attackInterval > 0.0001f) ? (1f / tower.attackInterval) : 0f;

        return $"DMG: {tower.damage}\nASPD: {aspd:0.##}\nRNG: {tower.range:0.##}";
    }
    
    private void ForceLayoutRebuild()
    {
        Canvas.ForceUpdateCanvases();

        if (buildPanel != null)
        {
            var rt = buildPanel.GetComponent<RectTransform>();
            if (rt != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }

        Canvas.ForceUpdateCanvases();
    }
}
