﻿using System.IO;
using YARG.Core.Utility;

namespace YARG.Core.Engine
{
    public abstract class BaseStats : IBinarySerializable
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
        /// Total score across finalized and pending score.
        /// </summary>
        public int TotalScore => CommittedScore + PendingScore;

        /// <summary>
        /// The player's current combo (such as 500 note streak)
        /// </summary>
        public int Combo;

        /// <summary>
        /// The player's highest combo achieved.
        /// </summary>
        public int MaxCombo;

        /// <summary>
        /// The player's current score multiplier (e.g 2x, 3x)
        /// </summary>
        public int ScoreMultiplier;

        /// <summary>
        /// Number of notes which have been hit.
        /// </summary>
        public int NotesHit;

        /// <summary>
        /// Number of notes which have been missed.
        /// </summary>
        public int NotesMissed;

        /// <summary>
        /// Amount of Star Power/Overdrive the player currently has.
        /// </summary>
        public double StarPowerAmount;

        /// <summary>
        /// Amount of Star Power/Overdrive the player had as of the most recent SP/OD rebase
        /// (SP activation, time signature change, SP sustain whammy start).
        /// </summary>
        public double StarPowerBaseAmount;

        /// <summary>
        /// True if the player currently has Star Power/Overdrive active.
        /// </summary>
        public bool IsStarPowerActive;

        /// <summary>
        /// Whether or not Star Power/Overdrive can be activated.
        /// </summary>
        public bool CanStarPowerActivate => StarPowerAmount >= 0.5 && !IsStarPowerActive;

        /// <summary>
        /// Number of Star Power phrases which have been hit.
        /// </summary>
        public int PhrasesHit;

        /// <summary>
        /// Number of Star Power phrases which have been missed.
        /// </summary>
        public int PhrasesMissed;

        /// <summary>
        /// Amount of points earned from solo bonuses.
        /// </summary>
        public int SoloBonuses;

        /// <summary>
        /// The number of stars the player has achieved.
        /// </summary>
        public int Stars;

        protected BaseStats()
        {
        }

        protected BaseStats(BaseStats stats)
        {
            CommittedScore = stats.CommittedScore;
            PendingScore = stats.PendingScore;
            Combo = stats.Combo;
            MaxCombo = stats.MaxCombo;
            ScoreMultiplier = stats.ScoreMultiplier;
            NotesHit = stats.NotesHit;
            NotesMissed = stats.NotesMissed;

            StarPowerAmount = stats.StarPowerAmount;
            StarPowerBaseAmount = stats.StarPowerBaseAmount;
            IsStarPowerActive = stats.IsStarPowerActive;

            PhrasesHit = stats.PhrasesHit;
            PhrasesMissed = stats.PhrasesMissed;
            SoloBonuses = stats.SoloBonuses;
            Stars = stats.Stars;
        }

        public virtual void Reset()
        {
            CommittedScore = 0;
            PendingScore = 0;
            Combo = 0;
            MaxCombo = 0;
            ScoreMultiplier = 1;
            NotesHit = 0;
            NotesMissed = 0;

            StarPowerAmount = 0;
            StarPowerBaseAmount = 0;
            IsStarPowerActive = false;

            PhrasesHit = 0;
            PhrasesMissed = 0;
            SoloBonuses = 0;
            Stars = 0;
        }

        public virtual void Serialize(BinaryWriter writer)
        {
            writer.Write(CommittedScore);
            writer.Write(PendingScore);
            writer.Write(Combo);
            writer.Write(MaxCombo);
            writer.Write(ScoreMultiplier);
            writer.Write(NotesHit);
            writer.Write(NotesMissed);

            writer.Write(StarPowerAmount);
            writer.Write(StarPowerBaseAmount);
            writer.Write(IsStarPowerActive);

            writer.Write(PhrasesHit);
            writer.Write(PhrasesMissed);
            writer.Write(SoloBonuses);
        }

        public virtual void Deserialize(BinaryReader reader, int version = 0)
        {
            CommittedScore = reader.ReadInt32();
            PendingScore = reader.ReadInt32();
            Combo = reader.ReadInt32();
            MaxCombo = reader.ReadInt32();
            ScoreMultiplier = reader.ReadInt32();
            NotesHit = reader.ReadInt32();
            NotesMissed = reader.ReadInt32();

            StarPowerAmount = reader.ReadDouble();
            StarPowerBaseAmount = reader.ReadDouble();
            IsStarPowerActive = reader.ReadBoolean();

            PhrasesHit = reader.ReadInt32();
            PhrasesMissed = reader.ReadInt32();
            SoloBonuses = reader.ReadInt32();
        }
    }
}