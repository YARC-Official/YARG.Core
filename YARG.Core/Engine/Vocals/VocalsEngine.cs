using System;
using System.Linq;
using YARG.Core.Chart;
using YARG.Core.Logging;

namespace YARG.Core.Engine.Vocals
{
    public abstract class VocalsEngine :
        BaseEngine<VocalNote, VocalsEngineParameters, VocalsStats, VocalsEngineState>
    {
        protected const int POINTS_PER_PERCUSSION = 100;

        public delegate void TargetNoteChangeEvent(VocalNote targetNote);

        public delegate void PhraseHitEvent(double hitPercentAfterParams, bool fullPoints);

        public TargetNoteChangeEvent? OnTargetNoteChanged;

        public Action<bool>? OnSing;
        public Action<bool>? OnHit;

        public PhraseHitEvent? OnPhraseHit;

        protected VocalsEngine(InstrumentDifficulty<VocalNote> chart, SyncTrack syncTrack,
            VocalsEngineParameters engineParameters, bool isBot)
            : base(chart, syncTrack, engineParameters, false, isBot)
        {
        }

        protected override void GenerateQueuedUpdates(double nextTime)
        {
            base.GenerateQueuedUpdates(nextTime);
            var previousTime = State.CurrentTime;

            // For bots, queue up updates every approximate vocal input frame to simulate
            // a stream of inputs. Make sure that the previous time has been properly set.
            if (IsBot && previousTime > 0.0)
            {
                double timeForFrame = 1.0 / EngineParameters.ApproximateVocalFps;
                int nextUpdateIndex = (int) Math.Floor(previousTime / timeForFrame) + 1;
                double nextUpdateTime = nextUpdateIndex * EngineParameters.ApproximateVocalFps;

                for (double time = nextUpdateTime; time < nextTime; time += timeForFrame)
                {
                    QueueUpdateTime(time, "Bot Input");
                }
            }
        }

        protected override void HitNote(VocalNote note)
        {
            note.SetHitState(true, false);

            if (note.IsPercussion)
            {
                AddScore(note);
                OnNoteHit?.Invoke(State.NoteIndex, note);
            }
            else
            {
                if (note.IsStarPower)
                {
                    AwardStarPower(note);
                    EngineStats.StarPowerPhrasesHit++;
                }

                if (note.IsSoloStart)
                {
                    StartSolo();
                }

                if (State.IsSoloActive)
                {
                    Solos[State.CurrentSoloIndex].NotesHit++;
                }

                if (note.IsSoloEnd)
                {
                    EndSolo();
                }

                // If there aren't any ticks in the phrase, then don't add
                // any score or update the multiplier.
                var ticks = GetTicksInPhrase(note);
                if (ticks != 0)
                {
                    EngineStats.Combo++;

                    if (EngineStats.Combo > EngineStats.MaxCombo)
                    {
                        EngineStats.MaxCombo = EngineStats.Combo;
                    }

                    AddScore(note);

                    UpdateMultiplier();
                }

                // No matter what, we still wanna count this as a phrase hit though
                EngineStats.NotesHit++;

                OnNoteHit?.Invoke(State.NoteIndex, note);

                // I want to call base.HitNote here, but I have no idea how vocals handles hit state so I'm scared to
                State.NoteIndex++;
            }
        }

        protected override void MissNote(VocalNote note)
        {
            if (note.IsPercussion)
            {
                note.SetMissState(true, false);
                OnNoteMissed?.Invoke(State.NoteIndex, note);
            }
            else
            {
                MissNote(note, 0);
            }
        }

        protected void MissNote(VocalNote note, double hitPercent)
        {
            note.SetMissState(true, false);

            if (note.IsStarPower)
            {
                StripStarPower(note);
            }

            if (note.IsSoloEnd)
            {
                EndSolo();
            }
            if (note.IsSoloStart)
            {
                StartSolo();
            }

            EngineStats.Combo = 0;

            AddPartialScore(hitPercent);

            UpdateMultiplier();

            OnNoteMissed?.Invoke(State.NoteIndex, note);

            // I want to call base.MissNote here, but I have no idea how vocals handles miss state so I'm scared to
            State.NoteIndex++;
        }

        /// <summary>
        /// Checks if the given vocal note can be hit with the current input state.
        /// </summary>
        /// <param name="note">The note to attempt to hit.</param>
        /// <param name="hitPercent">The hit percent of the note (0 to 1).</param>
        protected abstract bool CanVocalNoteBeHit(VocalNote note, out float hitPercent);

        /// <returns>
        /// Gets the amount of ticks in the phrase.
        /// </returns>
        protected static uint GetTicksInPhrase(VocalNote phrase)
        {
            uint totalTime = 0;
            foreach (var phraseNote in phrase.ChildNotes)
            {
                if (phraseNote.IsPercussion)
                {
                    continue;
                }

                totalTime += phraseNote.TotalTickLength;
            }

            return totalTime;
        }

        /// <returns>
        /// The note in the specified <paramref name="phrase"/> at the specified song <paramref name="tick"/>.
        /// </returns>
        protected static VocalNote? GetNoteInPhraseAtSongTick(VocalNote phrase, uint tick)
        {
            return phrase
                .ChildNotes
                .FirstOrDefault(phraseNote =>
                    !phraseNote.IsPercussion &&
                    tick >= phraseNote.Tick &&
                    tick <= phraseNote.TotalTickEnd);
        }

        protected static VocalNote? GetNextPercussionNote(VocalNote phrase, uint tick)
        {
            foreach (var note in phrase.ChildNotes)
            {
                // Skip sang vocal notes
                if (!note.IsPercussion && note.Tick < tick)
                {
                    continue;
                }

                // Skip hit/missed percussion notes
                if (note.IsPercussion && (note.WasHit || note.WasMissed))
                {
                    continue;
                }

                // If the next note in the phrase is not a percussion note, then
                // we can't hit the note until the note before it is done.
                if (!note.IsPercussion)
                {
                    return null;
                }

                // Otherwise, we found it!
                return note;
            }

            return null;
        }

        protected override void AddScore(VocalNote note)
        {
            if (note.IsPercussion)
            {
                AddScore(POINTS_PER_PERCUSSION);
            }
            else
            {
                AddScore(EngineParameters.PointsPerPhrase);
            }
        }

        protected void AddPartialScore(double hitPercent)
        {
            int score = (int) Math.Round(EngineParameters.PointsPerPhrase * hitPercent);
            AddScore(score);
        }

        protected override void UpdateMultiplier()
        {
            EngineStats.ScoreMultiplier = Math.Min(EngineStats.Combo + 1, 4);

            if (EngineStats.IsStarPowerActive)
            {
                EngineStats.ScoreMultiplier *= 2;
            }
        }

        protected sealed override int CalculateBaseScore()
        {
            return Notes.Where(note => note.ChildNotes.Count > 0).Sum(_ => EngineParameters.PointsPerPhrase);
        }
    }
}
