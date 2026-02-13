using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
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
    [SerializeField] private string buildOffText = "건설 (C)";
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
    
    [Header("HotKey")]
    [SerializeField] private Key buildHotkey = Key.C;

    private float _acc;
    private bool _bound;
    private bool _lastIsMax;

    // 캐시(변경 있을 때만 갱신)
    private bool _lastPlacing;
    private int _lastGold = int.MinValue;
    private int _lastBuildLevel = int.MinValue;
    private int _lastUpgradeCost = int.MinValue;
    private bool _lastBuildInteractable;
    private bool _lastUpgradeInteractable;
    private bool _spectating;

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
        
        if (!_spectating)
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                // If user is typing in an input field, ignore hotkey
                if (!IsTextInputFocused())
                {
                    if (kb[buildHotkey].wasPressedThisFrame)
                    {
                        // Optional: block while augment choosing
                        if (GameRuntime.Instance == null || (GameRuntime.Instance != null && (GameRuntime.Instance.IsGameOver == false)))
                        {
                            OnClickBuild();
                        }
                    }
                }
            }
        }

        // (1) Placing 상태가 바뀌었으면 즉시 반영 (이벤트 못 받아도 여기서 잡힘)
        bool placingNow = TM.IsPlacing;
        if (placingNow != _lastPlacing)
        {
            _lastPlacing = placingNow;
            ApplyBuildVisual(_lastPlacing);
        }

        // (2) 골드 변화 감지해서 인터랙터블만 갱신 (매 프레임 SetText 안 함)
        int goldNow = GR.Gold;
        if (goldNow != _lastGold)
        {
            _lastGold = goldNow;
            RefreshInteractable();
        }

        // (3) 레벨/비용 변화 감지해서 텍스트만 갱신
        int levelNow = TM.BuildLevel;
        bool isMax = TM.IsBuildLevelMax;

        if (levelNow != _lastBuildLevel || isMax != _lastIsMax)
        {
            _lastBuildLevel = levelNow;
            _lastIsMax = isMax;
            
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
        if (upgradeCostNow != _lastUpgradeCost)
        {
            _lastUpgradeCost = upgradeCostNow;
            if (txtUpgradeCost != null) txtUpgradeCost.SetText("{0}", _lastUpgradeCost);
            
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

        _lastPlacing = TM.IsPlacing;
        ApplyBuildVisual(_lastPlacing);

        _lastGold = GR.Gold;
        _lastBuildLevel = TM.BuildLevel;
        _lastUpgradeCost = TM.GetBuildUpgradeCost();

        if (txtBuildLevel != null) txtBuildLevel.SetText("Lv. {0}", _lastBuildLevel);
        if (txtUpgradeCost != null) txtUpgradeCost.SetText("{0}", _lastUpgradeCost);

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
    
    private bool IsTextInputFocused()
    {
        if (EventSystem.current == null) return false;

        var go = EventSystem.current.currentSelectedGameObject;
        if (go == null) return false;
        
        if (go.GetComponent<TMP_InputField>() != null) return true;
        if (go.GetComponent<UnityEngine.UI.InputField>() != null) return true;

        return false;
    }
    
    public void SetSpectating(bool spectating)
    {
        _spectating = spectating;

        if (btnBuild != null) btnBuild.interactable = !spectating;
        if (btnUpgrade != null) btnUpgrade.interactable = !spectating;
        
        if (spectating && TowerManager.Instance != null && TowerManager.Instance.IsPlacing)
            TowerManager.Instance.CancelPlaceMode();
    }
}
