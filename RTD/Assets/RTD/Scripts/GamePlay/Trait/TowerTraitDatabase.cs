using System.Collections.Generic;
using UnityEngine;

public class TowerTraitDatabase : MonoBehaviour
{
    [Header("All Traits")]
    [SerializeField] private TowerTraitSO[] allTraits;

    private static TraitTier GetFixedTierByGrade(TowerGrade grade)
    {
        return grade switch
        {
            TowerGrade.Rare => TraitTier.T1,
            TowerGrade.Epic => TraitTier.T2,
            TowerGrade.Legendary => TraitTier.T3,
            _ => TraitTier.None
        };
    }

    private static string GetTypeKeyFromTowerId(string towerId)
    {
        if (string.IsNullOrEmpty(towerId)) return "basic";
        string lower = towerId.ToLower();
        if (lower.Contains("cannon")) return "cannon";
        if (lower.Contains("magic")) return "magic";
        return "basic";
    }

    private static TowerTraitAllowed GetAllowedFlagFromTowerId(string towerId)
    {
        string key = GetTypeKeyFromTowerId(towerId);
        return key switch
        {
            "cannon" => TowerTraitAllowed.Cannon,
            "magic"  => TowerTraitAllowed.Magic,
            _        => TowerTraitAllowed.Basic
        };
    }

    public TowerTraitSO RollTrait(string towerId, TowerGrade grade)
    {
        if (allTraits == null || allTraits.Length == 0) return null;

        TraitTier tier = GetFixedTierByGrade(grade);
        if (tier == TraitTier.None) return null;

        TowerTraitAllowed towerFlag = GetAllowedFlagFromTowerId(towerId);

        List<TowerTraitSO> candidates = new List<TowerTraitSO>(16);
        for (int i = 0; i < allTraits.Length; i++)
        {
            var t = allTraits[i];
            if (t == null) continue;
            if (t.tier != tier) continue;
            if ((t.allowed & towerFlag) == 0) continue;
            candidates.Add(t);
        }

        if (candidates.Count == 0) return null;
        return candidates[Random.Range(0, candidates.Count)];
    }

    public TowerTraitSO RollTraitExclude(string towerId, TowerGrade grade, TowerTraitSO exclude)
    {
        if (allTraits == null || allTraits.Length == 0) return null;

        TraitTier tier = GetFixedTierByGrade(grade);
        if (tier == TraitTier.None) return null;

        TowerTraitAllowed towerFlag = GetAllowedFlagFromTowerId(towerId);

        List<TowerTraitSO> candidates = new List<TowerTraitSO>(16);
        for (int i = 0; i < allTraits.Length; i++)
        {
            var t = allTraits[i];
            if (t == null) continue;
            if (t.tier != tier) continue;
            if (exclude != null && t == exclude) continue;
            if ((t.allowed & towerFlag) == 0) continue;
            candidates.Add(t);
        }

        if (candidates.Count == 0) return null;
        return candidates[Random.Range(0, candidates.Count)];
    }

    public TowerTraitSO UpgradeTrait(TowerTraitSO current, TowerGrade toGrade)
    {
        if (current == null) return null;

        TraitTier targetTier = GetFixedTierByGrade(toGrade);
        if (targetTier == TraitTier.None) return null;

        for (int i = 0; i < allTraits.Length; i++)
        {
            var t = allTraits[i];
            if (t == null) continue;
            if (t.type == current.type && t.tier == targetTier)
                return t;
        }

        return current; // fallback
    }
}
