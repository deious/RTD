using UnityEngine;

public enum AugmentTarget
{
    Tower,
    Enemy
}

public enum AugmentType
{
    // Tower buffs
    TowerDamageMul,
    TowerAttackSpeedMul,
    TowerRangeAdd,

    // Enemy debuffs
    EnemySpeedMul,
    EnemyHpMul
}

[CreateAssetMenu(menuName = "RTD/Augment/Augment", fileName = "Augment_")]
public class AugmentSO : ScriptableObject
{
    public string augmentId;
    public string title;
    [TextArea] public string desc;

    public AugmentTarget target;
    public AugmentType type;

    public float value = 1f;
}