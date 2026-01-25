using UnityEngine;

[CreateAssetMenu(menuName = "RTD/Monster/Color")]
public class MonsterColorSO : ScriptableObject
{
    [Header("ID")]
    public string id; // red/blue/...

    [Header("Material")]
    public Material material;

    [Header("Optional Balance")]
    public float hpMul = 1f;
    public float speedMul = 1f;
    public float shieldMul = 1f;
}