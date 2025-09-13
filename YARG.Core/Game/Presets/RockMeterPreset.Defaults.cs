using System.Collections.Generic;

namespace YARG.Core.Game
{
    public partial class RockMeterPreset
    {
        public static RockMeterPreset Casual = new("Casual", true)
        {
            MissDamageMultiplier = 0.5f,
            OverhitDamageMultiplier = 0.25f,
            HitRecoveryMultiplier = 2.0f,
            VocalsMissDamageMultiplier = 0.5f,
            VocalsHitRecoveryMultiplier = 2.0f
        };
        public static RockMeterPreset Easy = new("Easy", true)
        {
            MissDamageMultiplier = 0.75f,
            OverhitDamageMultiplier = 0.5f,
            HitRecoveryMultiplier = 1.5f,
            VocalsMissDamageMultiplier = 0.75f,
            VocalsHitRecoveryMultiplier = 1.5f
        };
        public static RockMeterPreset Normal = new("Normal", true);
        public static RockMeterPreset Hard = new("Hard", true)
        {
            MissDamageMultiplier = 1.5f,
            OverhitDamageMultiplier = 1.5f,
            HitRecoveryMultiplier = 0.5f,
            VocalsMissDamageMultiplier = 1.5f,
            VocalsHitRecoveryMultiplier = 0.5f,
            StarPowerEffectMultiplier = 3.0f,
            StartingHappiness = 0.5f
        };
        public static RockMeterPreset Unfair = new("Unfair", true)
        {
            MissDamageMultiplier = 3.0f,
            OverhitDamageMultiplier = 3.0f,
            HitRecoveryMultiplier = 0.25f,
            VocalsMissDamageMultiplier = 2.0f,
            VocalsHitRecoveryMultiplier = 0.25f,
            StarPowerEffectMultiplier = 2.0f,
            StartingHappiness = 0.5f
        };

        public static readonly List<RockMeterPreset> Defaults = new()
        {
            Casual,
            Easy,
            Normal,
            Hard,
            Unfair
        };
    }
}
