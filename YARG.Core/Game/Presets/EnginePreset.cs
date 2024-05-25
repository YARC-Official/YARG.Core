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

        public EnginePreset(string name, bool defaultPreset = false) : base(name, defaultPreset)
        {
            FiveFretGuitar = new FiveFretGuitarPreset();
            Drums = new DrumsPreset();
            Vocals = new VocalsPreset();
        }

        public override BasePreset CopyWithNewName(string name)
        {
            return new EnginePreset(name)
            {
                FiveFretGuitar = FiveFretGuitar.Copy(),
                Drums = Drums.Copy(),
                Vocals = Vocals.Copy(),
            };
        }
    }
}