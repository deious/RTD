using System.Collections.Generic;

namespace RTD.Scripts.GamePlay.Wave
{
    public static class WaveModifierUtil
    {
        private const float FAST_SPEED_MUL = 1.25f;
        private const float TANK_HP_MUL = 1.50f;
        private const int SHIELD_HP_ADD = 30;

        public static WaveModifiers ToWaveModifiers(WaveModifierType[] types)
        {
            WaveModifiers mods = new WaveModifiers
            {
                speedMul = 1f,
                hpMul = 1f,
                shieldHp = 0,
                label = ""
            };

            if (types == null || types.Length == 0)
            {
                mods.label = "None";
                return mods;
            }

            List<string> labels = new List<string>();

            for (int i = 0; i < types.Length; i++)
            {
                switch (types[i])
                {
                    case WaveModifierType.Fast:
                        mods.speedMul *= FAST_SPEED_MUL;
                        labels.Add("SpeedUp");
                        break;

                    case WaveModifierType.Tank:
                        mods.hpMul *= TANK_HP_MUL;
                        labels.Add("Tanky");
                        break;

                    case WaveModifierType.Shield:
                        mods.shieldHp += SHIELD_HP_ADD;
                        labels.Add("Shield");
                        break;

                    case WaveModifierType.Split:
                        labels.Add("Split");
                        break;

                    default:
                        break;
                }
            }

            mods.label = (labels.Count == 0) ? "None" : string.Join(", ", labels);
            return mods;
        }
    }
}