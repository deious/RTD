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
    [SerializeField] private GameObject traitRow;
    [SerializeField] private TextMeshProUGUI txtTrait;
    [SerializeField] private TextMeshProUGUI txtTraitDesc;

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
    
    [Header("Action Buttons")]
    [SerializeField] private Button btnCombineExact;
    [SerializeField] private Button btnCombineRandom;
    [SerializeField] private Button btnRerollTrait;
    
    [Header("Reroll UI")]
    [SerializeField] private TextMeshProUGUI txtRerollCost;

    [Header("Sell UI")]
    [SerializeField] private TextMeshProUGUI txtSellRefund;

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
            if (txtName != null)
                txtName.text = !string.IsNullOrEmpty(d.displayName) ? d.displayName : d.towerId;

            if (txtGrade != null)
                txtGrade.text = $"등급 : {GradeToKr(d.grade)}";
        }
        else
        {
            if (txtName != null)  txtName.text = "타워";
            if (txtGrade != null) txtGrade.text = "등급 : -";
        }

        TowerTraitSO trait = TryGetTrait(tower);
        bool hasTrait = trait != null;

        if (traitRow != null) traitRow.SetActive(hasTrait);

        if (hasTrait)
        {
            if (txtTrait != null)
                txtTrait.text = $"특성 : {FormatTraitKr(trait)}";
            
            if (txtTraitDesc != null)
                txtTraitDesc.text = GetTraitDesc(trait);
        }
        else
        {
            if (txtTrait != null) txtTrait.text = "";
            if (txtTraitDesc != null) txtTraitDesc.text = "";
        }

        if (txtStats != null)
            txtStats.text = BuildStatsTextKr(tower);
        
        if (btnSell != null)
        {
            Debug.Log("[ContextUI] Bind Sell button listener");
            btnSell.gameObject.SetActive(true);
            btnSell.onClick.RemoveAllListeners();
            btnSell.onClick.AddListener(OnClickSell);
        }
        
        if (btnCombineExact != null)
        {
            btnCombineExact.gameObject.SetActive(true);
            btnCombineExact.onClick.RemoveAllListeners();
            btnCombineExact.onClick.AddListener(OnClickCombineExact);
        }

        if (btnCombineRandom != null)
        {
            btnCombineRandom.gameObject.SetActive(true);
            btnCombineRandom.onClick.RemoveAllListeners();
            btnCombineRandom.onClick.AddListener(OnClickCombineRandom);
        }

        TowerData data = tower.GetData();
        bool canReroll = (data != null && data.grade != TowerGrade.Normal);

        if (btnRerollTrait != null)
        {
            btnRerollTrait.gameObject.SetActive(true);
            btnRerollTrait.onClick.RemoveAllListeners();
            btnRerollTrait.onClick.AddListener(OnClickRerollTrait);
            
            btnRerollTrait.interactable = canReroll;
        }
        
        if (txtRerollCost != null)
        {
            if (!canReroll || data == null)
            {
                txtRerollCost.text = "";
            }
            else
            {
                int cost = GetRerollCost(data.grade);
                txtRerollCost.text = cost.ToString();
            }
        }
        
        if (txtSellRefund != null)
        {
            int refund = 0;
            if (TowerManager.Instance != null)
                refund = TowerManager.Instance.GetSellRefund(tower);

            txtSellRefund.text = (refund > 0) ? refund.ToString() : "";
        }
        
        ForceLayoutRebuildTower();
    }
    
    private void ForceLayoutRebuildTower()
    {
        Canvas.ForceUpdateCanvases();

        if (towerPanel != null)
        {
            var rt = towerPanel.GetComponent<RectTransform>();
            if (rt != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }

        Canvas.ForceUpdateCanvases();
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
    
    private void OnClickCombineExact()
    {
        if (TowerManager.Instance == null) return;
        TowerManager.Instance.RequestCombineExact();
    }

    private void OnClickCombineRandom()
    {
        if (TowerManager.Instance == null) return;
        TowerManager.Instance.RequestCombineRandom();
    }

    private void OnClickRerollTrait()
    {
        if (_current == null) return;

        TowerData d = _current.GetData();
        if (d == null) return;
        
        if (d.grade == TowerGrade.Normal) return;

        int cost = GetRerollCost(d.grade);

        if (TowerManager.Instance == null) return;

        bool ok = TowerManager.Instance.TryRerollTrait(_current, cost);
        if (!ok)
        {
            Debug.Log("[ContextUI] Reroll failed (not enough gold or no candidate)");
            return;
        }
        
        ShowTower(_current);
    }

    private int GetRerollCost(TowerGrade grade)
    {
        return grade switch
        {
            TowerGrade.Rare => 50,
            TowerGrade.Epic => 100,
            TowerGrade.Legendary => 200,
            _ => 999999
        };
    }
    
    private static string GradeToKr(TowerGrade grade)
    {
        return grade switch
        {
            TowerGrade.Normal => "일반",
            TowerGrade.Rare => "희귀",
            TowerGrade.Epic => "영웅",
            TowerGrade.Legendary => "전설",
            _ => grade.ToString()
        };
    }

    private static int TierToInt(TraitTier tier) => (int)tier;
    private static string FormatTraitKr(TowerTraitSO trait)
    {
        if (trait == null) 
            return "";

        string name = !string.IsNullOrEmpty(trait.traitName) ? trait.traitName : trait.type.ToString();

        return $"{name}";
    }

    private string BuildStatsTextKr(TowerBase tower)
    {
        if (tower == null) return "-";

        float attacksPerSec = (tower.attackInterval > 0.0001f) ? (1f / tower.attackInterval) : 0f;

        return
            $"대미지: {tower.damage}\n" +
            $"공격속도: {attacksPerSec:0.##}\n" +
            $"공격범위: {tower.range:0.##}";
    }
    
    private static string GetTraitDesc(TowerTraitSO trait)
    {
        if (trait == null) 
            return "";
        
        return trait.description; 
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
        if (btnCombineExact != null) btnCombineExact.onClick.RemoveAllListeners();
        if (btnCombineRandom != null) btnCombineRandom.onClick.RemoveAllListeners();
        if (btnRerollTrait != null) btnRerollTrait.onClick.RemoveAllListeners();
    }
}
