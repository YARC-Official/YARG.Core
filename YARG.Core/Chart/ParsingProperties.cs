using System;

namespace YARG.Core.Chart
{
    /// <summary>
    /// The type of drums contained in the chart.
    /// </summary>
    [Flags]
    public enum DrumsType
    {
        Unknown = 0,
        FourLane = 1 << 0,
        ProDrums = 1 << 1,
        FiveLane = 1 << 2,
        // For scanning
        FourOrPro = FourLane | ProDrums,
        FourOrFive = FourLane | FiveLane,
        ProOrFive = ProDrums | FiveLane,
        Any = FourOrFive | FiveLane | ProDrums,
    }

    public static class DrumsTypeQuery
    {
        public static bool Has(this DrumsType type, DrumsType value)
        {
            return (type & value) == value;
        }
    }

    /// <summary>
    /// Settings used when parsing charts.
    /// </summary>
    public struct ParseSettings
    {
        /// <summary>
        /// The default settings to use for parsing.
        /// </summary>
        public static readonly ParseSettings Default = new()
        {
            DrumsType = DrumsType.Unknown,
            HopoThreshold = SETTING_DEFAULT,
            SustainCutoffThreshold = SETTING_DEFAULT,
            ChordHopoCancellation = false,
            StarPowerNote = SETTING_DEFAULT,
            NoteSnapThreshold = 0,
            TuningOffsetCents = 0,
        };

        public static readonly ParseSettings Default_Chart = new()
        {
            DrumsType = DrumsType.Unknown,
            HopoThreshold = SETTING_DEFAULT,
            SustainCutoffThreshold = 0,
            ChordHopoCancellation = false,
            StarPowerNote = SETTING_DEFAULT,
            NoteSnapThreshold = 0,
            TuningOffsetCents = 0,
        };

        public static readonly ParseSettings Default_Midi = new()
        {
            DrumsType = DrumsType.Unknown,
            HopoThreshold = SETTING_DEFAULT,
            SustainCutoffThreshold = SETTING_DEFAULT,
            ChordHopoCancellation = false,
            StarPowerNote = 116,
            NoteSnapThreshold = 0,
            TuningOffsetCents = 0,
        };

        /// <summary>
        /// The value used to indicate a setting should be overwritten with the
        /// appropriate default value for the chart being parsed.
        /// </summary>
        public const int SETTING_DEFAULT = -1;

        /// <summary>
        /// The drums mode to parse the drums track as.
        /// </summary>
        public DrumsType DrumsType;

        /// <summary>
        /// The tick distance between notes to use as the HOPO threshold.
        /// </summary>
        /// <remarks>
        /// Uses the <c>hopo_threshold</c> tag from song.ini files.<br/>
        /// Defaults to a 1/12th note.
        /// </remarks>
        public long HopoThreshold;

        /// <summary>
        /// Skip marking single notes after chords as HOPOs
        /// if the single note shares a fret with the chord.
        /// </summary>
        public bool ChordHopoCancellation;

        /// <summary>
        /// The tick threshold to use for sustain cutoffs.
        /// </summary>
        /// <remarks>
        /// Uses the <c>sustain_cutoff_threshold</c> tag from song.ini files.<br/>
        /// Defaults to a 1/12th note in .mid, and 0 in .chart.
        /// </remarks>
        public long SustainCutoffThreshold;

        /// <summary>
        /// The tick threshold to use for snapping together single notes into chords.
        /// </summary>
        /// <remarks>
        /// Defaults to 10 in CON files, and 0 in other charts.
        /// </remarks>
        public long NoteSnapThreshold;

        /// <summary>
        /// The MIDI note to use for Star Power phrases in .mid charts.
        /// </summary>
        /// <remarks>
        /// Uses the <c>multiplier_note</c> and <c>star_power_note</c> tags from song.ini files.<br/>
        /// Defaults to 116.
        /// </remarks>
        public int StarPowerNote;

        /// <summary>
        /// Number of cents by which to adjust all pitched vocal notes, for non-A440 songs.
        /// </summary>
        /// <remarks>
        /// Defaults to 0. Should never go beyond the [-50,50] range, but can still be honored if it does.
        /// </remarks>
        public int TuningOffsetCents;
    }
}