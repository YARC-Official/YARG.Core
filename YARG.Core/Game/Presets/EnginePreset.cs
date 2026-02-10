using YARG.Core.Game.Settings;

namespace YARG.Core.Game
{
    public partial class EnginePreset : BasePreset
    {
        [SettingSubSection]
        public FiveFretGuitarPreset FiveFretGuitar;
        [SettingSubSection]
        public DrumsPreset Drums;
        [SettingSubSection]
        public VocalsPreset Vocals;
        [SettingSubSection]
        public ProKeysPreset ProKeys;

        public EnginePreset(string name, bool defaultPreset = false) : base(name, defaultPreset)
        {
            FiveFretGuitar = new FiveFretGuitarPreset();
            Drums = new DrumsPreset();
            Vocals = new VocalsPreset();
            ProKeys = new ProKeysPreset();
        }

        public override BasePreset CopyWithNewName(string name)
        {
            return new EnginePreset(name)
            {
                FiveFretGuitar = FiveFretGuitar.Copy(),
                Drums = Drums.Copy(),
                Vocals = Vocals.Copy(),
                ProKeys = ProKeys.Copy(),
            };
        }

        public override string? Validate()
        {
            // Validate timing thresholds for all instruments (except Vocals which doesn't have a hit window)
            string? error;

            error = ValidateHitWindowThresholds("Five Fret Guitar", FiveFretGuitar.HitWindow);
            if (error != null) return error;

            error = ValidateHitWindowThresholds("Drums", Drums.HitWindow);
            if (error != null) return error;

            error = ValidateHitWindowThresholds("Pro Keys", ProKeys.HitWindow);
            if (error != null) return error;

            return null;
        }

        private static string? ValidateHitWindowThresholds(string instrumentName, HitWindowPreset hitWindow)
        {
            if (hitWindow.PerfectThresholdPercent >= hitWindow.GreatThresholdPercent)
            {
                return $"{instrumentName}: Perfect threshold ({hitWindow.PerfectThresholdPercent:F2}%) must be less than Great threshold ({hitWindow.GreatThresholdPercent:F2}%).";
            }

            if (hitWindow.GreatThresholdPercent >= hitWindow.GoodThresholdPercent)
            {
                return $"{instrumentName}: Great threshold ({hitWindow.GreatThresholdPercent:F2}%) must be less than Good threshold ({hitWindow.GoodThresholdPercent:F2}%).";
            }

            if (hitWindow.GoodThresholdPercent >= hitWindow.PoorThresholdPercent)
            {
                return $"{instrumentName}: Good threshold ({hitWindow.GoodThresholdPercent:F2}%) must be less than Poor threshold ({hitWindow.PoorThresholdPercent:F2}%).";
            }

            return null;
        }
    }
}