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
    [SerializeField] private TextMeshProUGUI txtBuildLevel;
    [SerializeField] private TextMeshProUGUI txtUpgradeCost;

    [Header("Build Button Label")]
    [SerializeField] private string buildOffText = "건설";
    [SerializeField] private string buildOnText  = "건설 (ON)";

    [Header("Build Button Colors")]
    [SerializeField] private Color buildOffColor = new Color(0.75f, 0.75f, 0.75f, 1f);
    [SerializeField] private Color buildOnColor  = new Color(0.25f, 1.00f, 0.25f, 1f);

    [Header("Refresh")]
    [SerializeField] private float uiRefreshInterval = 0.1f; // 0.1초마다만 체크(렉 방지)

    private float _acc;

    // 캐시(변경 있을 때만 갱신)
    private bool _lastPlacing;
    private int _lastGold = int.MinValue;
    private int _lastBuildLevel = int.MinValue;
    private int _lastUpgradeCost = int.MinValue;
    private bool _lastBuildInteractable;
    private bool _lastUpgradeInteractable;

    private TowerManager TM => TowerManager.Instance;
    private GameManager GM => GameManager.Instance;

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
    }

    private void Update()
    {
        _acc += Time.unscaledDeltaTime;
        if (_acc < uiRefreshInterval)
            return;

        _acc = 0f;

        // TowerManager/GameManager 아직 없으면 그냥 대기
        if (TM == null || GM == null)
        {
            // 나중에 생성될 수도 있으니 주기적으로 재바인딩 시도
            TryBindEvents();
            return;
        }

        // (1) Placing 상태가 바뀌었으면 즉시 반영 (이벤트 못 받아도 여기서 잡힘)
        bool placingNow = TM.IsPlacing;
        if (placingNow != _lastPlacing)
        {
            _lastPlacing = placingNow;
            ApplyBuildVisual(_lastPlacing);
        }

        // (2) 골드 변화 감지해서 인터랙터블만 갱신 (매 프레임 SetText 안 함)
        int goldNow = GM.Gold;
        if (goldNow != _lastGold)
        {
            _lastGold = goldNow;
            RefreshInteractable();
        }

        // (3) 레벨/비용 변화 감지해서 텍스트만 갱신
        int levelNow = TM.BuildLevel;
        if (levelNow != _lastBuildLevel)
        {
            _lastBuildLevel = levelNow;
            if (txtBuildLevel != null) txtBuildLevel.SetText("Lv. {0}", _lastBuildLevel);
        }

        int upgradeCostNow = TM.GetBuildUpgradeCost();
        if (upgradeCostNow != _lastUpgradeCost)
        {
            _lastUpgradeCost = upgradeCostNow;
            if (txtUpgradeCost != null) txtUpgradeCost.SetText("{0}", _lastUpgradeCost);

            // 업그레이드 비용 바뀌면 버튼 상태도 다시 계산
            RefreshInteractable();
        }
    }

    // ===== Button Callbacks =====

    public void OnClickBuild()
    {
        if (TM == null) return;

        if (TM.IsPlacing) TM.CancelPlaceMode();
        else TM.BeginPlaceMode();

        // 이벤트가 안 떠도 즉시 UI 반영
        ApplyBuildVisual(TM.IsPlacing);
        RefreshInteractable();
    }

    public void OnClickUpgrade()
    {
        if (TM == null) return;

        bool ok = TM.TryUpgradeBuildLevel();
        if (ok)
        {
            // 변경값은 Update에서 캐시로 반영되지만 즉시 갱신도 해줌
            ForceRefreshAll();
        }
    }

    // ===== Event handling (optional) =====

    private bool _bound;

    private void TryBindEvents()
    {
        if (_bound) return;
        if (TM == null) return;

        TM.OnPlacingChanged += HandlePlacingChanged;
        _bound = true;
    }

    private void UnbindEvents()
    {
        if (!_bound) return;
        if (TM != null) TM.OnPlacingChanged -= HandlePlacingChanged;
        _bound = false;
    }

    private void HandlePlacingChanged(bool isOn)
    {
        _lastPlacing = isOn;
        ApplyBuildVisual(isOn);
        RefreshInteractable();
    }

    // ===== UI apply =====

    private void ApplyBuildVisual(bool isOn)
    {
        if (btnBuildLabel != null)
            btnBuildLabel.text = isOn ? buildOnText : buildOffText;

        if (btnBuildImage != null)
            btnBuildImage.color = isOn ? buildOnColor : buildOffColor;
    }

    private void ForceRefreshAll()
    {
        if (TM == null || GM == null) return;

        _lastPlacing = TM.IsPlacing;
        ApplyBuildVisual(_lastPlacing);

        _lastGold = GM.Gold;
        _lastBuildLevel = TM.BuildLevel;
        _lastUpgradeCost = TM.GetBuildUpgradeCost();

        if (txtBuildLevel != null) txtBuildLevel.SetText("Lv. {0}", _lastBuildLevel);
        if (txtUpgradeCost != null) txtUpgradeCost.SetText("{0}", _lastUpgradeCost);

        RefreshInteractable(true);
    }

    private void RefreshInteractable(bool force = false)
    {
        if (TM == null || GM == null) return;

        int minCost = TM.GetMinBuildCostForCurrentLevel();
        bool canBuild = GM.Gold >= minCost;

        bool buildInteractable = TM.IsPlacing ? true : canBuild;

        int upgradeCost = TM.GetBuildUpgradeCost();
        bool upgradeInteractable = GM.Gold >= upgradeCost;

        if (btnBuild != null && (force || buildInteractable != _lastBuildInteractable))
        {
            _lastBuildInteractable = buildInteractable;
            btnBuild.interactable = buildInteractable;
        }

        if (btnUpgrade != null && (force || upgradeInteractable != _lastUpgradeInteractable))
        {
            _lastUpgradeInteractable = upgradeInteractable;
            btnUpgrade.interactable = upgradeInteractable;
        }
    }
}
