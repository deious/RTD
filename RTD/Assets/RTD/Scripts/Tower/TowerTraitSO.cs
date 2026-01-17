using System;
using UnityEngine;

public enum TowerTraitType
{
    // Common
    Critical,
    Chain,

    // Basic
    DoubleShot,
    Execute,
    Focus,

    // Cannon
    Siege,
    Shrapnel,
    Stun,

    // Magic
    Slow,
    Burn,
    Curse,
}

public enum TraitTier { None = 0, T1 = 1, T2 = 2, T3 = 3 }

[Flags]
public enum TowerTraitAllowed
{
    None  = 0,
    Basic = 1 << 0,
    Cannon= 1 << 1,
    Magic = 1 << 2,
    All   = Basic | Cannon | Magic
}

[CreateAssetMenu(menuName = "RTD/Trait")]
public class TowerTraitSO : ScriptableObject
{
    public TowerTraitType type;
    public TraitTier tier;

    [Header("Allowed Tower Types")]
    public TowerTraitAllowed allowed = TowerTraitAllowed.All;

    public string traitName;
    [TextArea] public string description;

    [Header("Params")]
    public float value;
    public float duration;
    public float range;
    public int count;
}