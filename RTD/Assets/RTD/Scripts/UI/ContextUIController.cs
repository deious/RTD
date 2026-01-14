using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ContextUIController : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject root;

    [Header("Texts")]
    [SerializeField] private TextMeshProUGUI txtName;
    [SerializeField] private TextMeshProUGUI txtGrade;
    [SerializeField] private TextMeshProUGUI txtTrait;
    [SerializeField] private TextMeshProUGUI txtStats;

    [Header("Buttons")]
    [SerializeField] private Button btnSell;

    private TowerBase _current;

    private void Awake()
    {
        if (root != null) root.SetActive(false);
    }

    public void ShowTower(TowerBase tower)
    {
        _current = tower;

        if (tower == null)
        {
            Hide();
            return;
        }

        if (root != null) root.SetActive(true);
        
        TowerData d = tower.GetData();
        if (d != null)
        {
            if (txtName != null)  txtName.text  = string.IsNullOrEmpty(d.towerId) ? "Tower" : d.towerId;
            if (txtGrade != null) txtGrade.text = d.grade.ToString();
        }
        else
        {
            if (txtName != null)  txtName.text  = "Tower";
            if (txtGrade != null) txtGrade.text = "-";
        }
        
        TowerTraitSO trait = TryGetTrait(tower);
        if (txtTrait != null)
            txtTrait.text = (trait != null) ? $"{trait.type} {trait.tier}" : "-";

        if (txtStats != null)
        {
            txtStats.text = "DMG: -  ASPD: -  RNG: -";
        }
        
        if (btnSell != null)
        {
            btnSell.gameObject.SetActive(false);
            btnSell.onClick.RemoveAllListeners();
        }
    }

    public void Hide()
    {
        _current = null;
        if (root != null) root.SetActive(false);
    }
    
    private TowerTraitSO TryGetTrait(TowerBase tower)
    {
        return null;
    }
}
