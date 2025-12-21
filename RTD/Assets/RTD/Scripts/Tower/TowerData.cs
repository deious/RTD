using UnityEngine;

[CreateAssetMenu(menuName = "RTD/Tower Data")]
public class TowerData : ScriptableObject
{
    [Header("Identity")]
    public string towerId;
    public TowerGrade grade;

    [Header("Stats")]
    public float damage;
    public float attackSpeed;
    public float range;

    [Header("Cost")]
    public int buildCost;

    [Header("Visual")]
    public GameObject towerPrefab;
    public Color gradeColor;
}
