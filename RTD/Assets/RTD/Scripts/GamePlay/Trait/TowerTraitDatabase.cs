using System.Collections.Generic;
using UnityEngine;

public class TowerTraitDatabase : MonoBehaviour
{
    [Header("All Traits (put all TraitSO assets here)")]
    [SerializeField] private TowerTraitSO[] allTraits;

    [System.Serializable]
    public struct AffinityWeights
    {
        public int core;
        public int common;
        public int wild;

        public AffinityWeights(int core, int common, int wild)
        {
            this.core = core;
            this.common = common;
            this.wild = wild;
        }
    }

    private static readonly Dictionary<string, AffinityWeights> AffinityWeightByType =
        new Dictionary<string, AffinityWeights>
        {
            { "basic",  new AffinityWeights(50, 45, 5) },
            { "cannon", new AffinityWeights(60, 30, 10) },
            { "magic",  new AffinityWeights(45, 35, 20) },
        };

    private static int GetAffinityWeight(TraitAffinity affinity, AffinityWeights w)
    {
        return affinity switch
        {
            TraitAffinity.Core => Mathf.Max(0, w.core),
            TraitAffinity.Common => Mathf.Max(0, w.common),
            TraitAffinity.Wild => Mathf.Max(0, w.wild),
            _ => 1
        };
    }
    
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
        if (string.IsNullOrEmpty(towerId))
            return "basic";

        string lower = towerId.ToLower();
        if (lower.Contains("cannon")) return "cannon";
        if (lower.Contains("magic")) return "magic";
        return "basic";
    }

    public TowerTraitSO RollTrait(string towerId, TowerGrade grade)
    {
        if (allTraits == null || allTraits.Length == 0)
            return null;

        TraitTier tier = GetFixedTierByGrade(grade);
        if (tier == TraitTier.None)
            return null;

        string typeKey = GetTypeKeyFromTowerId(towerId);

        AffinityWeights w = AffinityWeightByType.TryGetValue(typeKey, out var found)
            ? found
            : new AffinityWeights(50, 40, 10);

        List<TowerTraitSO> candidates = new List<TowerTraitSO>(16);
        for (int i = 0; i < allTraits.Length; i++)
        {
            var t = allTraits[i];
            if (t == null) continue;
            if (t.tier != tier) continue;
            candidates.Add(t);
        }

        if (candidates.Count == 0)
            return null;

        int total = 0;
        for (int i = 0; i < candidates.Count; i++)
            total += GetAffinityWeight(candidates[i].affinity, w);

        if (total <= 0)
            return candidates[Random.Range(0, candidates.Count)];

        int roll = Random.Range(1, total + 1);
        for (int i = 0; i < candidates.Count; i++)
        {
            roll -= GetAffinityWeight(candidates[i].affinity, w);
            if (roll <= 0)
                return candidates[i];
        }

        return candidates[candidates.Count - 1];
    }
    
    public TowerTraitSO RollTraitExclude(string towerId, TowerGrade grade, TowerTraitSO exclude)
    {
        if (allTraits == null || allTraits.Length == 0)
            return null;

        TraitTier tier = GetFixedTierByGrade(grade);
        if (tier == TraitTier.None)
            return null;

        string typeKey = GetTypeKeyFromTowerId(towerId);

        AffinityWeights w = AffinityWeightByType.TryGetValue(typeKey, out var found)
            ? found
            : new AffinityWeights(50, 40, 10);

        List<TowerTraitSO> candidates = new List<TowerTraitSO>(16);
        for (int i = 0; i < allTraits.Length; i++)
        {
            var t = allTraits[i];
            if (t == null) continue;
            if (t.tier != tier) continue;
            if (exclude != null && t == exclude) continue;
            candidates.Add(t);
        }

        if (candidates.Count == 0)
            return null;

        int total = 0;
        for (int i = 0; i < candidates.Count; i++)
            total += GetAffinityWeight(candidates[i].affinity, w);

        if (total <= 0)
            return candidates[Random.Range(0, candidates.Count)];

        int roll = Random.Range(1, total + 1);
        for (int i = 0; i < candidates.Count; i++)
        {
            roll -= GetAffinityWeight(candidates[i].affinity, w);
            if (roll <= 0)
                return candidates[i];
        }

        return candidates[candidates.Count - 1];
    }
    
    public TowerTraitSO UpgradeTrait(TowerTraitSO current, TowerGrade toGrade)
    {
        if (current == null) return null;

        TraitTier targetTier = GetFixedTierByGrade(toGrade);
        if (targetTier == TraitTier.None)
            return null;

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
