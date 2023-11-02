using System.Text;

namespace YARG.Core.Chart
{
    /// <summary>
    /// The type of drums contained in the chart.
    /// </summary>
    public enum DrumsType
    {
        FourLane,
        ProDrums,
        FiveLane,
        Unknown,
        UnknownPro,
    }

    /// <summary>
    /// Settings used when parsing charts.
    /// </summary>
    public class ParseSettings
    {
        public static ParseSettings Default => new()
        {
            DrumsType = DrumsType.Unknown,

            HopoThreshold = SETTING_DEFAULT,
            HopoFreq_FoF = SETTING_DEFAULT,
            EighthNoteHopo = false,
            SustainCutoffThreshold = SETTING_DEFAULT,
            NoteSnapThreshold = 0,

            StarPowerNote = SETTING_DEFAULT,
            Encoding = null,
        };

        public const int SETTING_DEFAULT = -1;

        public DrumsType DrumsType;

        public long HopoThreshold;
        public int HopoFreq_FoF;
        public bool EighthNoteHopo;
        public long SustainCutoffThreshold;
        public long NoteSnapThreshold;

        public int StarPowerNote;
        public string? Encoding;
    }
}