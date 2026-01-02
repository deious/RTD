using System.Collections.Generic;
using UnityEngine;

public static class WaveModifierRoller
{
    private enum ModType
    {
        SpeedUp,
        Tanky,
        Shield
    }

    private struct WeightedMod
    {
        public ModType type;
        public int weight;
        public WeightedMod(ModType t, int w) { type = t; weight = w; }
    }

    // Tuning values (Day 15 minimal)
    private static readonly Dictionary<ModType, float> SpeedMul = new Dictionary<ModType, float>
    {
        { ModType.SpeedUp, 1.25f }
    };

    private static readonly Dictionary<ModType, float> HpMul = new Dictionary<ModType, float>
    {
        { ModType.Tanky, 1.50f }
    };

    private static readonly Dictionary<ModType, int> ShieldHp = new Dictionary<ModType, int>
    {
        { ModType.Shield, 30 }
    };

    private static readonly WeightedMod[] Pool =
    {
        new WeightedMod(ModType.SpeedUp, 40),
        new WeightedMod(ModType.Tanky,  35),
        new WeightedMod(ModType.Shield, 25),
    };

    // Roll 0~2 modifiers, no duplicates (simple)
    public static WaveModifiers Roll(int minCount, int maxCount)
    {
        int count = Random.Range(minCount, maxCount + 1);

        // Copy pool to list for no-duplicate draws
        List<WeightedMod> temp = new List<WeightedMod>(Pool);

        WaveModifiers result = new WaveModifiers
        {
            speedMul = 1f,
            hpMul = 1f,
            shieldHp = 0,
            label = ""
        };

        List<string> labels = new List<string>();

        for (int i = 0; i < count; i++)
        {
            if (temp.Count == 0)
                break;

            int pickedIndex = PickWeightedIndex(temp);
            ModType picked = temp[pickedIndex].type;
            temp.RemoveAt(pickedIndex);

            ApplyOne(ref result, picked, labels);
        }

        result.label = labels.Count == 0 ? "None" : string.Join(", ", labels);
        return result;
    }

    private static int PickWeightedIndex(List<WeightedMod> list)
    {
        int total = 0;
        for (int i = 0; i < list.Count; i++)
            total += list[i].weight;

        int r = Random.Range(0, total);
        int acc = 0;

        for (int i = 0; i < list.Count; i++)
        {
            acc += list[i].weight;
            if (r < acc)
                return i;
        }

        return list.Count - 1;
    }

    private static void ApplyOne(ref WaveModifiers mods, ModType type, List<string> labels)
    {
        switch (type)
        {
            case ModType.SpeedUp:
                mods.speedMul *= SpeedMul[type];
                labels.Add("SpeedUp");
                break;

            case ModType.Tanky:
                mods.hpMul *= HpMul[type];
                labels.Add("Tanky");
                break;

            case ModType.Shield:
                mods.shieldHp += ShieldHp[type];
                labels.Add("Shield");
                break;
        }
    }
}
