namespace YARG.Core.Chart
{
    /// <summary>
    /// The type of chart file to read.
    /// </summary>
    public enum ChartType
    {
        MID,
        MIDI,
        CHART,
    };

    /// <summary>
    /// The type of drums contained in the chart.
    /// </summary>
    public enum DrumsType
    {
        FourLane,
        FiveLane,
        Unknown
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
            NoteSnapThreshold = SETTING_DEFAULT,

            StarPowerNote = SETTING_DEFAULT,
        };

        public const int SETTING_DEFAULT = -1;

        public DrumsType DrumsType;

        public int HopoThreshold;
        public int HopoFreq_FoF;
        public bool EighthNoteHopo;
        public int SustainCutoffThreshold;
        public int NoteSnapThreshold;

        public int StarPowerNote;
    }
}