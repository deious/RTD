using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BuildHUDController : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button btnBuild;
    [SerializeField] private Image btnBuildImage;
    [SerializeField] private TextMeshProUGUI btnBuildLabel;
    [SerializeField] private Button btnUpgrade;
    
    [SerializeField] private GameObject upgradeRow;
    [SerializeField] private Image btnUpgradeImage;
    [SerializeField] private TextMeshProUGUI btnUpgradeLabel;
    [SerializeField] private TextMeshProUGUI txtBuildLevel;
    [SerializeField] private TextMeshProUGUI txtUpgradeCost;

    [Header("Build Button Label")]
    [SerializeField] private string buildOffText = "건설";
    [SerializeField] private string buildOnText  = "건설 (ON)";

    [Header("Build Button Colors")]
    [SerializeField] private Color buildOffColor = new Color(0.75f, 0.75f, 0.75f, 1f);
    [SerializeField] private Color buildOnColor  = new Color(0.25f, 1.00f, 0.25f, 1f);
    
    [Header("Upgrade Button Colors")]
    [SerializeField] private Color upgradeOffColor = new Color(0.75f, 0.75f, 0.75f, 1f);

    [Header("Refresh")]
    //[SerializeField] private float uiRefreshInterval = 0.1f; // 0.1초마다만 체크(렉 방지)
    
    [Header("Build Cost Row")]
    [SerializeField] private TextMeshProUGUI txtBuildCost;

    [Header("Upgrade Max UI")]
    [SerializeField] private string buildLevelMaxText = "Lv. Max";
    [SerializeField] private string upgradeBlockedText = "강화불가";

    private float acc;
    private bool bound;
    private bool lastIsMax;

    // 캐시(변경 있을 때만 갱신)
    private bool lastPlacing;
    private int lastGold = int.MinValue;
    private int lastBuildLevel = int.MinValue;
    private int lastUpgradeCost = int.MinValue;
    private bool lastBuildInteractable;
    private bool lastUpgradeInteractable;

    private TowerManager TM => TowerManager.Instance;
    private GameRuntime GR => GameRuntime.Instance;

    private void Awake()
    {
        if (btnBuild == null || btnUpgrade == null)
            Debug.LogWarning("[BuildHUDController] Buttons not wired in Inspector.");

        if (btnBuild != null)
        {
            if (btnBuildImage == null) btnBuildImage = btnBuild.GetComponent<Image>();
            if (btnBuildLabel == null) btnBuildLabel = btnBuild.GetComponentInChildren<TextMeshProUGUI>(true);
            btnBuild.transition = Selectable.Transition.None;
        }
    }

    private void OnEnable()
    {
        TryBindEvents();
    }

    private void OnDisable()
    {
        UnbindEvents();
    }

    private void Start()
    {
        ForceRefreshAll();
        InitBuildCostUI();
    }

    private void Update()
    {
        /*acc += Time.unscaledDeltaTime;
        if (acc < uiRefreshInterval)
            return;

        acc = 0f;*/

        // TowerManager/GameManager 아직 없으면 그냥 대기
        if (TM == null || GR == null)
        {
            // 나중에 생성될 수도 있으니 주기적으로 재바인딩 시도
            TryBindEvents();
            return;
        }

        // (1) Placing 상태가 바뀌었으면 즉시 반영 (이벤트 못 받아도 여기서 잡힘)
        bool placingNow = TM.IsPlacing;
        if (placingNow != lastPlacing)
        {
            lastPlacing = placingNow;
            ApplyBuildVisual(lastPlacing);
        }

        // (2) 골드 변화 감지해서 인터랙터블만 갱신 (매 프레임 SetText 안 함)
        int goldNow = GR.Gold;
        if (goldNow != lastGold)
        {
            lastGold = goldNow;
            RefreshInteractable();
        }

        // (3) 레벨/비용 변화 감지해서 텍스트만 갱신
        int levelNow = TM.BuildLevel;
        bool isMax = TM.IsBuildLevelMax;

        if (levelNow != lastBuildLevel || isMax != lastIsMax)
        {
            lastBuildLevel = levelNow;
            lastIsMax = isMax;
            
            if (txtBuildLevel != null)
                txtBuildLevel.text = isMax ? buildLevelMaxText : $"Lv. {levelNow}";
            
            if (btnUpgradeLabel != null)
                btnUpgradeLabel.text = isMax ? upgradeBlockedText : "타워 소환 확률 강화"; 
            
            if (upgradeRow != null)
                upgradeRow.SetActive(!isMax);

            if (btnUpgradeImage != null)
                btnUpgradeImage.color = isMax ? upgradeOffColor : btnUpgradeImage.color;
            
            if (btnUpgrade != null)
                btnUpgrade.enabled = !isMax;

            RefreshInteractable(true);
        }

        int upgradeCostNow = TM.GetBuildUpgradeCost();
        if (upgradeCostNow != lastUpgradeCost)
        {
            lastUpgradeCost = upgradeCostNow;
            if (txtUpgradeCost != null) txtUpgradeCost.SetText("{0}", lastUpgradeCost);
            
            RefreshInteractable();
        }
    }
    
    private void InitBuildCostUI()
    {
        if (TM == null)
            return;

        int cost = TM.BuildCost;

        if (txtBuildCost != null)
            txtBuildCost.SetText("{0}", cost);
    }

    public void OnClickBuild()
    {
        if (TM == null) return;

        if (TM.IsPlacing) TM.CancelPlaceMode();
        else TM.BeginPlaceMode();
        
        ApplyBuildVisual(TM.IsPlacing);
        RefreshInteractable();
    }

    public void OnClickUpgrade()
    {
        if (TM == null) return;

        bool ok = TM.TryUpgradeBuildLevel();
        if (ok)
        {
            ForceRefreshAll();
        }
    }

    private void TryBindEvents()
    {
        if (bound) return;
        if (TM == null) return;

        TM.OnPlacingChanged += HandlePlacingChanged;
        bound = true;
    }

    private void UnbindEvents()
    {
        if (!bound) return;
        if (TM != null) TM.OnPlacingChanged -= HandlePlacingChanged;
        bound = false;
    }

    private void HandlePlacingChanged(bool isOn)
    {
        lastPlacing = isOn;
        ApplyBuildVisual(isOn);
        RefreshInteractable();
    }

    private void ApplyBuildVisual(bool isOn)
    {
        if (btnBuildLabel != null)
            btnBuildLabel.text = isOn ? buildOnText : buildOffText;

        if (btnBuildImage != null)
            btnBuildImage.color = isOn ? buildOnColor : buildOffColor;
    }

    private void ForceRefreshAll()
    {
        if (TM == null || GR == null) return;

        lastPlacing = TM.IsPlacing;
        ApplyBuildVisual(lastPlacing);

        lastGold = GR.Gold;
        lastBuildLevel = TM.BuildLevel;
        lastUpgradeCost = TM.GetBuildUpgradeCost();

        if (txtBuildLevel != null) txtBuildLevel.SetText("Lv. {0}", lastBuildLevel);
        if (txtUpgradeCost != null) txtUpgradeCost.SetText("{0}", lastUpgradeCost);

        RefreshInteractable(true);
    }

    private void RefreshInteractable(bool force = false)
    {
        if (TM == null || GR == null) return;

        int minCost = TM.GetMinBuildCostForCurrentLevel();
        bool canBuild = GR.Gold >= minCost;

        bool buildInteractable = TM.IsPlacing ? true : canBuild;

        int upgradeCost = TM.GetBuildUpgradeCost();
        bool upgradeInteractable = GR.Gold >= upgradeCost;

        if (btnBuild != null && (force || buildInteractable != lastBuildInteractable))
        {
            lastBuildInteractable = buildInteractable;
            btnBuild.interactable = buildInteractable;
        }

        if (btnUpgrade != null && (force || upgradeInteractable != lastUpgradeInteractable))
        {
            lastUpgradeInteractable = upgradeInteractable;
            btnUpgrade.interactable = upgradeInteractable;
        }
    }
}
