using UnityEngine;

[CreateAssetMenu(menuName = "RTD/Monsters/Boss Monster Data", fileName = "BossMonsterData")]
public class BossMonsterDataSO : ScriptableObject
{
    [Header("ID (CSV key)")]
    [Tooltip("CSV에서 이 값을 키로 숫자만 덮어씁니다. 예: boss_golem_01")]
    public string bossId = "boss_default";

    [Header("Unity References")]
    public GameObject prefab;
    public float scale = 1.8f;
    
    [Header("Base Stats (can be overridden by CSV)")]
    public int maxHp = 1500;
    public float moveSpeed = 1.2f;
    public int shieldHp = 0;

    [Header("Reward (can be overridden by CSV)")]
    public int rewardGold = 50;

    [Header("Boss Spawn Shake (can be overridden by CSV)")]
    public float shakeDuration = 0.25f;
    public float shakeStrength = 0.25f;
}