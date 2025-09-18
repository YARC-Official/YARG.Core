using YARG.Core.Game.Settings;

namespace YARG.Core.Game
{
    public partial class RockMeterPreset : BasePreset
    {
        [SettingType(SettingType.Slider)]
        [SettingRange(0.01f, 1f)]
        public float StartingHappiness = 0.833f;

        [SettingType(SettingType.Slider)]
        [SettingRange(0f, 10f)]
        public float MissDamageMultiplier = 1.0f;

        [SettingType(SettingType.Slider)]
        [SettingRange(0f, 10f)]
        public float OverhitDamageMultiplier = 0.333f;

        [SettingType(SettingType.Slider)]
        [SettingRange(0f, 10f)]
        public float HitRecoveryMultiplier = 1.0f;

        [SettingType(SettingType.Slider)]
        [SettingRange(0f, 10f)]
        public float StarPowerEffectMultiplier = 5.0f;

        [SettingType(SettingType.Slider)]
        [SettingRange(0f, 10f)]
        public float VocalsMissDamageMultiplier = 1.0f;

        [SettingType(SettingType.Slider)]
        [SettingRange(0f, 10f)]
        public float VocalsHitRecoveryMultiplier = 1.0f;

        public RockMeterPreset(string name, bool defaultPreset = false) : base(name, defaultPreset)
        {
        }

        public override BasePreset CopyWithNewName(string name)
        {
            return new RockMeterPreset(name)
            {
                MissDamageMultiplier = MissDamageMultiplier,
                OverhitDamageMultiplier = OverhitDamageMultiplier,
                HitRecoveryMultiplier = HitRecoveryMultiplier,
                StartingHappiness = StartingHappiness,
                StarPowerEffectMultiplier = StarPowerEffectMultiplier,
                VocalsMissDamageMultiplier = VocalsMissDamageMultiplier,
                VocalsHitRecoveryMultiplier = VocalsHitRecoveryMultiplier
            };
        }
    }
}