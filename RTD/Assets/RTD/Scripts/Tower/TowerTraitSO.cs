using UnityEngine;

public enum TowerTraitType { Critical, Slow, Chain, Explosion }
public enum TraitTier { T1, T2, T3 }
public enum TraitAffinity { Core, Common, Wild }

[CreateAssetMenu(menuName = "RTD/Trait")]
public class TowerTraitSO : ScriptableObject
{
    public TowerTraitType type;
    public TraitTier tier;
    public TraitAffinity affinity;

    public string traitName;
    [TextArea] public string description;

    public float value;      // 치명타 배율/확률, 둔화율 등
    public float duration;   // 둔화 지속시간 등
    public float range;      // 폭발/연쇄 반경
    public int count;        // 연쇄 타겟 수 등
}