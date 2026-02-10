
using System;
using System.IO;
using YARG.Core.Chart;
using YARG.Core.Extensions;
using YARG.Core.IO;
using YARG.Core.Chart;
using YARG.Core.Replays;

namespace YARG.Core.Engine
{
    public abstract class BaseStats
    {
        /// <summary>
        /// Finalized score (e.g from notes hit and sustains)
        /// </summary>
        public int CommittedScore;

        /// <summary>
        /// Score that is currently pending addition (e.g. from active sustains).
        /// These points are recalculated every update, and only get added once their
        /// final condition has been met.
        /// </summary>
        /// <remarks>
        /// These points are still earned, but their total value is not final yet.
        /// Adding them immediately would result in problems such as precision errors.
        /// </remarks>
        public int PendingScore;

        /// <summary>
        /// Total score across all score values.
        /// </summary>
        /// <remarks>
        /// Calculated from <see cref="CommittedScore"/>, <see cref="PendingScore"/> and <see cref="SoloBonuses"/>.
        /// </remarks>
        public int TotalScore => CommittedScore + PendingScore + SoloBonuses;

        /// <summary>
        /// Total score earned from hitting notes.
        /// </summary>
        public int NoteScore;

        /// <summary>
        /// Total score earned from holding sustains.
        /// </summary>
        public int SustainScore;

        /// <summary>
        /// Total score earned from score multipliers.
        /// </summary>
        public int MultiplierScore;

        /// <summary>
        /// Total score earned from band bonuses, typically from Star Power/Overdrive activations from other players.
        /// </summary>
        public int BandBonusScore;

        /// <summary>
        /// The score used to calculate star progress.
        /// </summary>
        /// <remarks>
        /// Calculated from <see cref="CommittedScore"/> and <see cref="PendingScore"/>.
        /// <see cref="SoloBonuses"/> is not included in star progress.
        /// </remarks>
        public int StarScore => CommittedScore + PendingScore;

        /// <summary>
        /// The player's current combo (such as 500 note streak)
        /// </summary>
        public int Combo;

        /// <summary>
        /// The player's current combo, when counted as part of the Band Streak.
        /// </summary>
        public virtual int ComboInBandUnits => Combo * BandComboUnits;

        /// <summary>
        /// How much the band combo should increment per note or phrase hit for this instrument.
        /// </summary>
        public virtual int BandComboUnits => 1;

        /// <summary>
        /// The player's highest combo achieved.
        /// </summary>
        public int MaxCombo;

        /// <summary>
        /// The player's current score multiplier (e.g 2x, 3x)
        /// </summary>
        public int ScoreMultiplier;

        /// <summary>
        /// The score multiplier currently applied to the entire band.
        /// </summary>
        public int BandMultiplier;

        /// <summary>
        /// The bonus multiplier awared to this player as a result of other players having Star Power/Overdrive active.
        /// See also <see cref="BandBonusScore"/>.
        /// </summary>
        public int BandBonusMultiplier => IsStarPowerActive ? BandMultiplier - 2 : BandMultiplier - 1;

        /// <summary>
        /// Number of notes which have been hit.
        /// </summary>
        public int NotesHit;

        /// <summary>
        /// Number of laned notes which have been hit.
        /// </summary>
        public int LanedNotesHit;

        /// <summary>
        /// Number of notes in the chart. This value should never be modified.
        /// </summary>
        public int TotalNotes;

        /// <summary>
        /// Number of chords in the chart. Defaults to total notes, but some instruments calculate differently.
        /// </summary>
        public int TotalChords;

        /// <summary>
        /// Number of notes which have been missed.
        /// </summary>
        /// <remarks>Value is calculated from <see cref="TotalNotes"/> - <see cref="NotesHit"/>.</remarks>
        public int NotesMissed => TotalNotes - NotesHit;

        /// <summary>
        /// The percent of notes hit.
        /// </summary>
        public virtual float Percent => TotalNotes == 0 ? 1f : (float) NotesHit / TotalNotes;

        /// <summary>
        /// Current amount of Star Power ticks the player has.
        /// </summary>
        public uint StarPowerTickAmount;

        /// <summary>
        /// Total ticks of Star Power earned.
        /// </summary>
        public uint TotalStarPowerTicks;

        /// <summary>
        /// Total bars of Star Power filled.
        /// </summary>
        public double TotalStarPowerBarsFilled;

        /// <summary>
        /// Number of times the player has activated Star Power.
        /// </summary>
        public int StarPowerActivationCount;

        /// <summary>
        /// Total amount of time the player has been in Star Power.
        /// </summary>
        public double TimeInStarPower;

        /// <summary>
        /// Amount of Star Power/Overdrive gained from whammy.
        /// </summary>
        public uint StarPowerWhammyTicks;

        /// <summary>
        /// True if the player currently has Star Power active.
        /// </summary>
        public bool IsStarPowerActive;

        /// <summary>
        /// Number of Star Power phrases which have been hit.
        /// </summary>
        public int StarPowerPhrasesHit;

        /// <summary>
        /// Number of Star Power phrases in the chart. This value should never be modified.
        /// </summary>
        public int TotalStarPowerPhrases;

        /// <summary>
        /// Number of Star Power phrases which have been missed.
        /// </summary>
        /// <remarks>Value is calculated from <see cref="TotalStarPowerPhrases"/> - <see cref="StarPowerPhrasesHit"/>.</remarks>
        public int StarPowerPhrasesMissed => TotalStarPowerPhrases - StarPowerPhrasesHit;

        /// <summary>
        /// Amount of points earned from solo bonuses.
        /// </summary>
        public int SoloBonuses;

        /// <summary>
        /// Amount of points earned from Star Power.
        /// </summary>
        public int StarPowerScore;

        /// <summary>
        /// The number of stars the player has achieved, along with the progress to the next star.
        /// </summary>
        public float Stars;

        /// <summary>
        /// Is this a full combo?
        /// </summary>
        public virtual bool IsFullCombo => MaxCombo == TotalNotes;

        /// The total note timing offset in milliseconds.
        /// Used together with NotesHit to calculate the average timing offset.
        /// </summary>
        public double TotalNoteTimingOffset;

        /// <summary>
        /// The sum of squared note timing values for calculating variance and standard deviation.
        /// </summary>
        public double TotalNoteTimingOffsetSquared;

        /// <summary>
        /// The timing accuracy of the last note hit in milliseconds.
        /// Positive values indicate a late hit, negative values indicate an early hit.
        /// </summary>
        public double LastNoteTimingMs;

        // Note judgement buckets (based on absolute deviation in ms)
        public int NotesPerfect;
        public int NotesGreat;
        public int NotesGood;
        public int NotesPoor;

        /// <summary>
        /// Whether to record advanced timing statistics. Set this before gameplay starts.
        /// </summary>
        public bool RecordTimingStatistics { get; set; }

        protected BaseStats()
        {
        }

        protected BaseStats(BaseStats stats)
        {
            CommittedScore = stats.CommittedScore;
            PendingScore = stats.PendingScore;
            NoteScore = stats.NoteScore;
            SustainScore = stats.SustainScore;
            MultiplierScore = stats.MultiplierScore;
            BandBonusScore = stats.BandBonusScore;
            Combo = stats.Combo;
            MaxCombo = stats.MaxCombo;
            ScoreMultiplier = stats.ScoreMultiplier;
            BandMultiplier = stats.BandMultiplier;

            NotesHit = stats.NotesHit;
            TotalNotes = stats.TotalNotes;

            TotalNoteTimingOffset = stats.TotalNoteTimingOffset;
            TotalNoteTimingOffsetSquared = stats.TotalNoteTimingOffsetSquared;

            TotalNoteTimingOffset = stats.TotalNoteTimingOffset;
            TotalNoteTimingOffsetSquared = stats.TotalNoteTimingOffsetSquared;

            StarPowerTickAmount = stats.StarPowerTickAmount;
            TotalStarPowerTicks = stats.TotalStarPowerTicks;
            TotalStarPowerBarsFilled = stats.TotalStarPowerBarsFilled;
            StarPowerActivationCount = stats.StarPowerActivationCount;
            TimeInStarPower = stats.TimeInStarPower;
            StarPowerWhammyTicks = stats.StarPowerWhammyTicks;
            IsStarPowerActive = stats.IsStarPowerActive;

            StarPowerPhrasesHit = stats.StarPowerPhrasesHit;
            TotalStarPowerPhrases = stats.TotalStarPowerPhrases;

            SoloBonuses = stats.SoloBonuses;
            StarPowerScore = stats.StarPowerScore;

            Stars = stats.Stars;

            NotesPerfect = stats.NotesPerfect;
            NotesGreat = stats.NotesGreat;
            NotesGood = stats.NotesGood;
            NotesPoor = stats.NotesPoor;
        }

        protected BaseStats(ref FixedArrayStream stream, int version)
        {
            CommittedScore = stream.Read<int>(Endianness.Little);
            PendingScore = stream.Read<int>(Endianness.Little);
            NoteScore = stream.Read<int>(Endianness.Little);
            SustainScore = stream.Read<int>(Endianness.Little);
            MultiplierScore = stream.Read<int>(Endianness.Little);
            if (version >= 9)
            {
                BandBonusScore = stream.Read<int>(Endianness.Little);
            }

            Combo = stream.Read<int>(Endianness.Little);
            MaxCombo = stream.Read<int>(Endianness.Little);
            ScoreMultiplier = stream.Read<int>(Endianness.Little);
            if (version >= 9)
            {
                BandMultiplier = stream.Read<int>(Endianness.Little);
            }

            NotesHit = stream.Read<int>(Endianness.Little);
            TotalNotes = stream.Read<int>(Endianness.Little);

            StarPowerTickAmount = stream.Read<uint>(Endianness.Little);
            TotalStarPowerTicks = stream.Read<uint>(Endianness.Little);
            TimeInStarPower = stream.Read<double>(Endianness.Little);
            StarPowerWhammyTicks = stream.Read<uint>(Endianness.Little);
            StarPowerActivationCount = stream.Read<int>(Endianness.Little);
            IsStarPowerActive = stream.ReadBoolean();

            StarPowerPhrasesHit = stream.Read<int>(Endianness.Little);
            TotalStarPowerPhrases = stream.Read<int>(Endianness.Little);

            SoloBonuses = stream.Read<int>(Endianness.Little);
            StarPowerScore = stream.Read<int>(Endianness.Little);

            // Deliberately not read so that stars can be re-calculated if thresholds change
            // Stars = reader.ReadInt32();
        }

        public virtual void Reset()
        {
            CommittedScore = 0;
            PendingScore = 0;
            NoteScore = 0;
            SustainScore = 0;
            MultiplierScore = 0;
            BandBonusScore = 0;
            Combo = 0;
            MaxCombo = 0;
            ScoreMultiplier = 1;
            BandMultiplier = 1;
            NotesHit = 0;
            TotalNoteTimingOffset = 0.0;
            TotalNoteTimingOffsetSquared = 0.0;
            TotalNoteTimingOffset = 0.0;
            TotalNoteTimingOffsetSquared = 0.0;
            // Don't reset TotalNotes
            // TotalNotes = 0;

            StarPowerTickAmount = 0;
            TotalStarPowerTicks = 0;
            TotalStarPowerBarsFilled = 0;
            StarPowerActivationCount = 0;
            TimeInStarPower = 0;
            StarPowerWhammyTicks = 0;
            IsStarPowerActive = false;

            StarPowerPhrasesHit = 0;
            // TotalStarPowerPhrases = 0;

            SoloBonuses = 0;
            StarPowerScore = 0;

            Stars = 0;

            NotesPerfect = 0;
            NotesGreat = 0;
            NotesGood = 0;
            NotesPoor = 0;
        }

        public virtual void Serialize(BinaryWriter writer)
        {
            writer.Write(CommittedScore);
            writer.Write(PendingScore);
            writer.Write(NoteScore);
            writer.Write(SustainScore);
            writer.Write(MultiplierScore);
            writer.Write(BandBonusScore);

            writer.Write(Combo);
            writer.Write(MaxCombo);
            writer.Write(ScoreMultiplier);
            writer.Write(BandMultiplier);

            writer.Write(NotesHit);
            writer.Write(TotalNotes);
            writer.Write(TotalNoteTimingOffset);
            writer.Write(TotalNoteTimingOffsetSquared);
            writer.Write(TotalNoteTimingOffset);
            writer.Write(TotalNoteTimingOffsetSquared);

            writer.Write(StarPowerTickAmount);
            writer.Write(TotalStarPowerTicks);
            writer.Write(TimeInStarPower);
            writer.Write(StarPowerWhammyTicks);
            writer.Write(StarPowerActivationCount);
            writer.Write(IsStarPowerActive);

            writer.Write(StarPowerPhrasesHit);
            writer.Write(TotalStarPowerPhrases);

            writer.Write(SoloBonuses);
            writer.Write(StarPowerScore);

            // Deliberately not written so that stars can be re-calculated with different thresholds
            // writer.Write(Stars);

            writer.Write(NotesPerfect);
            writer.Write(NotesGreat);
            writer.Write(NotesGood);
            writer.Write(NotesPoor);
        }

        public abstract ReplayStats ConstructReplayStats(string name);

        public double GetAverageOffset()
        {
            return NotesHit > 0 ? TotalNoteTimingOffset / NotesHit : 0.0;
        }

        /// <summary>
        /// Calculates the standard deviation of note timing in milliseconds.
        /// Standard deviation measures the consistency/precision of timing.
        /// A lower value indicates more consistent timing.
        /// </summary>
        /// <returns>The standard deviation in milliseconds, or 0 if no notes have been hit.</returns>
        public double GetStandardDeviation()
        {
            if (NotesHit == 0) return 0.0;

            double mean = GetAverageOffset();
            double variance = (TotalNoteTimingOffsetSquared / NotesHit) - (mean * mean);
            return Math.Sqrt(Math.Max(0, variance));
        }

        /// <summary>
        /// Records timing statistics for a note hit if timing recording is enabled.
        /// </summary>
        /// <param name="note">The note that was hit</param>
        /// <param name="currentTime">The current time when the note was hit</param>
        /// <param name="maxHitWindow">The maximum hit window in seconds</param>
        /// <param name="perfectPercent">Perfect threshold as percentage of hit window</param>
        /// <param name="greatPercent">Great threshold as percentage of hit window</param>
        /// <param name="goodPercent">Good threshold as percentage of hit window</param>
        /// <param name="poorPercent">Poor threshold as percentage of hit window</param>
        public void RecordNoteHitTiming<NoteType>(NoteType note, double currentTime, double maxHitWindow,
            double perfectPercent, double greatPercent,
            double goodPercent, double poorPercent) where NoteType : Note<NoteType>
        {
            ++NotesHit;

            if (!RecordTimingStatistics)
                return;
            
            // Calculate timing accuracy in milliseconds (signed: negative=early, positive=late)
            double timingMs = (currentTime - note.Time) * 1000.0;
            LastNoteTimingMs = timingMs;
            TotalNoteTimingOffset += timingMs;
            TotalNoteTimingOffsetSquared += timingMs * timingMs;
        
            // Grade thresholds as percentages of the half-window (since timing is centered):
            // The hit window is split: early (negative) and late (positive) from the note time
            // So we use half the window as the base for each direction.
            double halfWindow = (maxHitWindow * 1000.0) / 2.0;
            
            // Convert percentages to actual thresholds
            double perfectThreshold = halfWindow * (perfectPercent / 100.0);
            double greatThreshold = halfWindow * (greatPercent / 100.0);
            double goodThreshold = halfWindow * (goodPercent / 100.0);
            double poorThreshold = halfWindow * (poorPercent / 100.0);

            double abs = Math.Abs(timingMs);
            if (abs <= perfectThreshold) NotesPerfect++;
            else if (abs <= greatThreshold) NotesGreat++;
            else if (abs <= goodThreshold) NotesGood++;
            else if (abs <= poorThreshold) NotesPoor++;
            else
            {
                // Outside POOR is effectively a miss; don't count here since misses are tracked
                // by NotesMissed.
            }
        }
    }
}