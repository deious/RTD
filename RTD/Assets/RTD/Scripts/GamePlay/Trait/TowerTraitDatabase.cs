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

    // 타워 타입별 성향 가중치
    private static readonly Dictionary<string, AffinityWeights> AffinityWeightByType =
        new Dictionary<string, AffinityWeights>
        {
            { "basic",  new AffinityWeights(50, 45, 5) },
            { "cannon", new AffinityWeights(60, 30, 10) },
            { "magic",  new AffinityWeights(45, 35, 20) },
        };

    public TowerTraitSO RollTrait(string towerId, TowerGrade grade)
    {
        if (allTraits == null || allTraits.Length == 0)
            return null;

        // 1) 타워 타입 키(basic/cannon/magic)
        string typeKey = GetTypeKeyFromTowerId(towerId);

        // 2) 등급별 티어 확률로 티어 결정
        TraitTier tier = RollTierByGrade(grade);

        // 3) 타입별 성향 가중치로 affinity 결정
        AffinityWeights w = AffinityWeightByType.TryGetValue(typeKey, out var found)
            ? found
            : new AffinityWeights(50, 40, 10);

        TraitAffinity affinity = RollAffinity(w);

        // 4) 후보 수집: (tier + affinity) 매칭
        List<TowerTraitSO> candidates = new List<TowerTraitSO>(16);
        for (int i = 0; i < allTraits.Length; i++)
        {
            var t = allTraits[i];
            if (t == null) continue;

            if (t.tier != tier) continue;
            if (t.affinity != affinity) continue;

            candidates.Add(t);
        }

        // 5) 폴백: tier만 맞는 걸로라도 뽑기 (데이터 미구성 대비)
        if (candidates.Count == 0)
        {
            for (int i = 0; i < allTraits.Length; i++)
            {
                var t = allTraits[i];
                if (t == null) continue;

                if (t.tier == tier)
                    candidates.Add(t);
            }
        }

        if (candidates.Count == 0)
            return null;

        return candidates[Random.Range(0, candidates.Count)];
    }

    private static TraitTier RollTierByGrade(TowerGrade grade)
    {
        // 네 룰: Normal은 Trait 없음이니 이 함수는 Rare/Epic/Legend에만 호출한다고 가정
        int t1, t2, t3;

        switch (grade)
        {
            case TowerGrade.Rare:
                t1 = 80; t2 = 20; t3 = 0;
                break;
            case TowerGrade.Epic:
                t1 = 50; t2 = 45; t3 = 5;
                break;
            case TowerGrade.Legendary:
                t1 = 25; t2 = 55; t3 = 20;
                break;
            default:
                t1 = 100; t2 = 0; t3 = 0;
                break;
        }

        int sum = t1 + t2 + t3;
        int roll = Random.Range(1, sum + 1);

        if (roll <= t1) return TraitTier.T1;
        roll -= t1;
        if (roll <= t2) return TraitTier.T2;
        return TraitTier.T3;
    }

    private static TraitAffinity RollAffinity(AffinityWeights w)
    {
        int core = Mathf.Max(0, w.core);
        int common = Mathf.Max(0, w.common);
        int wild = Mathf.Max(0, w.wild);

        int sum = Mathf.Max(1, core + common + wild);
        int roll = Random.Range(1, sum + 1);

        if (roll <= core) return TraitAffinity.Core;
        roll -= core;
        if (roll <= common) return TraitAffinity.Common;
        return TraitAffinity.Wild;
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
}
