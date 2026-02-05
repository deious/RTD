using UnityEngine;

[CreateAssetMenu(menuName = "RTD/Monster/Archetype")]
public class MonsterArchetypeSO : ScriptableObject
{
    [Header("ID")]
    public string id; // archer, mage, spearman, swordman

    [Header("Prefab")]
    public GameObject prefab;

    [Header("Base Stats")]
    public float baseHp = 20;
    public float baseMoveSpeed = 2f;
    public float baseShieldHp = 0;

    [Header("Optional")]
    public bool canBeBossCandidate = false;
}
