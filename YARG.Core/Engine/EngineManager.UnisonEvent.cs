using System;
using System.Collections.Generic;
using YARG.Core.Chart;
using YARG.Core.Engine.Vocals;
using YARG.Core.Logging;
using YARG.Core.Extensions;

namespace YARG.Core.Engine
{
    public partial class EngineManager
    {
        public class UnisonEvent
        {
            public double                        Time                { get; }
            public double                        TimeEnd             { get; }
            public uint                          Tick                { get; }
            public uint                          TickEnd             { get; }
            public int                           PartCount           { get; private set; }
            public int                           SuccessCount        { get; private set; }
            public bool                          Awarded             { get; set; }
            public Dictionary<int, UnisonPhrase> ParticipantToPhrase { get; }

            public UnisonEvent(double time, double timeEnd, uint tick, uint tickEnd)
            {
                Time = time;
                TimeEnd = timeEnd;
                Tick = tick;
                TickEnd = tickEnd;
                PartCount = 0;
                SuccessCount = 0;
                Awarded = false;
                ParticipantToPhrase = new Dictionary<int, UnisonPhrase>();
            }

            public void AddPlayer(EngineContainer engineContainer, UnisonPhrase sourcePhrase)
            {
                if (!ParticipantToPhrase.TryAdd(engineContainer.EngineId, sourcePhrase))
                {
                    return;
                }

                PartCount++;
            }

            public void RemovePlayer(EngineContainer engineContainer)
            {
                if (ParticipantToPhrase.Remove(engineContainer.EngineId))
                {
                    PartCount--;
                }
            }

            // Returns true if all players succesfully completed the unison
            public bool Success(EngineContainer engineContainer)
            {
                if (ParticipantToPhrase.ContainsKey(engineContainer.EngineId))
                {
                    YargLogger.LogFormatDebug("Player {0} successfully completed unison ending at time {1}",
                        engineContainer.EngineId, TimeEnd);
                    SuccessCount++;
                }

                if (SuccessCount == ParticipantToPhrase.Count)
                {
                    YargLogger.LogFormatDebug("Unison phrase ending at time {0} successfully completed by all participants",
                        TimeEnd);
                    return true;
                }

                // If SuccessCount is ever greater than the number of players, something has gone seriously wrong
                YargLogger.Assert(SuccessCount <= ParticipantToPhrase.Count, "SuccessCount mismanagement detected");
                return false;
            }

            public void Reset()
            {
                Awarded = false;
                SuccessCount = 0;
            }
        }

        public class UnisonPhrase : Phrase
        {
            public int NoteCount { get; }
            public UnisonPhrase(double time, double timeLength, uint tick, uint tickLength, int noteCount) : base(PhraseType.StarPower, time, timeLength, tick, tickLength)
            {
                NoteCount = noteCount;
            }

            public UnisonPhrase(Phrase other, int noteCount) : base(PhraseType.StarPower, other.Time, other.TimeLength, other.Tick, other.TickLength)
            {
                NoteCount = noteCount;
            }
        }

        private readonly List<UnisonEvent> _unisonEvents = new();

        public IReadOnlyList<UnisonEvent> UnisonEvents => _unisonEvents.AsReadOnly();

        public struct StarPowerSection : IEquatable<StarPowerSection>
        {
            public double Time;
            public double TimeEnd;
            public uint Tick;
            public uint TickEnd;
            public Phrase PhraseRef;

            public StarPowerSection(double time, double timeEnd, uint tick, uint tickEnd, Phrase phrase)
            {
                Time = time;
                TimeEnd = timeEnd;
                Tick = tick;
                TickEnd = tickEnd;
                PhraseRef = phrase;
            }

            public StarPowerSection(double time, double timeEnd, Phrase phrase)
            {
                Time = time;
                TimeEnd = timeEnd;
                Tick = phrase.Tick;
                TickEnd = phrase.TickEnd;
                PhraseRef = phrase;
            }

            public bool Equals(StarPowerSection other) => Time.Equals(other.Time) && TimeEnd.Equals(other.TimeEnd);

            public override bool Equals(object obj) => obj is StarPowerSection other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(Time, TimeEnd);

            /// <summary>
            /// Checks if two StarPowerSection objects are within <b>ticks</b> tolerance of each other.
            ///
            /// Note: The intent here is that tolerance should normally be 1/16th of a measure, minus 1 tick,
            /// but it has to be passed in since we can't know the chart resolution here
            /// </summary>
            /// <param name="other"></param>
            /// <param name="tolerance">ticks of tolerance</param>
            /// <returns></returns>
            public bool TickAlmostEquals(StarPowerSection other, uint tolerance)
            {
                return TickWithinTolerance(Tick, other.Tick, tolerance) && TickWithinTolerance(TickEnd, other.TickEnd, tolerance);
            }
        }

        public static bool TickWithinTolerance(uint t1, uint t2, uint tolerance)
        {
            return t1 - t2 <= tolerance || t2 - t1 <= tolerance;
        }

        public delegate void UnisonPhrasesReadyEvent(List<UnisonEvent> unisonEvents);

        public delegate void UnisonPhraseSuccessEvent();

        public  UnisonPhraseSuccessEvent?                       OnUnisonPhraseSuccess;

        // Instrument groups whose combination cannot be the source of a unison
        public static readonly List<List<Instrument>> InstrumentGroups = new List<List<Instrument>>()
        {
            new List<Instrument> { Instrument.FiveFretGuitar, Instrument.ProGuitar_17Fret, Instrument.ProGuitar_22Fret, Instrument.SixFretGuitar, Instrument.FiveFretCoopGuitar, Instrument.FiveFretRhythm },
            new List<Instrument> { Instrument.FiveFretBass, Instrument.ProBass_17Fret, Instrument.ProBass_22Fret, Instrument.SixFretBass },
            new List<Instrument> { Instrument.FourLaneDrums, Instrument.FiveLaneDrums, Instrument.ProDrums, Instrument.EliteDrums },
            new List<Instrument> { Instrument.Keys, Instrument.ProKeys }
        };

        private void AddPlayerToUnisons(EngineContainer engineContainer, SongChart chart)
        {
            // Vocals don't participate in unisons, so don't add them to the list
            if (engineContainer is EngineContainer<VocalNote, VocalsEngineParameters, VocalsStats>)
            {
                return;
            }
            bool noEvents = _unisonEvents.Count == 0;
            foreach (var phrase in engineContainer.UnisonPhrases)
            {
                var tolerance = (chart.Resolution / 4) - 1; // 16th note minus one tick
                if (!noEvents) // No reason for the first player to be added to loop at all.
                {
                    bool found = false;
                    foreach (var ev in _unisonEvents)
                    {
                        // If a phrase is within the tolerance of the event's start and end tick...
                        if (!TickWithinTolerance(phrase.Tick, ev.Tick, tolerance) ||
                            !TickWithinTolerance(phrase.TickEnd, ev.TickEnd, tolerance))
                        {
                            continue;
                        }

                        // Add it to the event.
                        ev.AddPlayer(engineContainer, phrase);
                        found = true;
                        break;
                    }

                    if (found)
                    {
                        continue;
                    }
                }

                // No matching event has been found, so create a new one.
                var unisonEvent = new UnisonEvent(phrase.Time, phrase.TimeEnd, phrase.Tick, phrase.TickEnd);
                _unisonEvents.Add(unisonEvent);
                unisonEvent.AddPlayer(engineContainer, phrase);
            }
            // Subscribe the container to OnStarPowerPhraseHit so bonuses can be awarded as appropriate
            engineContainer.SubscribeToStarPowerPhraseHit();
        }

        private void RemovePlayerFromUnisons(EngineContainer engineContainer)
        {
            foreach (var unisonEvent in _unisonEvents)
            {
                unisonEvent.RemovePlayer(engineContainer);
            }

            engineContainer.UnsubscribeToStarPowerPhraseHit();
        }

        /// <summary>
        /// Builds unison phrases for a combination of instrument and chart
        /// </summary>
        /// <param name="instrumentDifficulty"><see cref="InstrumentDifficulty{TNote}"/></param>
        /// <param name="chart"><see cref="SongChart"/></param>
        /// <param name="includeChildNotesInNoteCount">Used to determine how to calculate note count in the phrase.</param>
        /// <returns>List of UnisonPhrase objects.
        /// <br />These Phrases have corresponding StarPower Phrases in other tracks,
        /// <br />which is what makes them unison phrases.
        /// </returns>
        public static List<UnisonPhrase> GetUnisonPhrases<TNoteType>(
            InstrumentDifficulty<TNoteType> instrumentDifficulty, SongChart chart, bool includeChildNotesInNoteCount)
            where TNoteType : Note<TNoteType>
        {
            // Unisons must have at least 2 participants.

            // A sixteenth note minus a tick
            var tickTolerance = (chart.Resolution / 4) - 1;

            // Since vocals can't have unisons, we may as well pull the ripcord early
            if (instrumentDifficulty.Instrument is Instrument.Vocals or Instrument.Harmony)
            {
                return new List<UnisonPhrase>();
            }

            var sourceSpSections = instrumentDifficulty.GetStarpowerSections();

            // Add ourselves to the beginning of the accepted list so any dupes with us will be filtered
            var acceptedSpSections = new List<List<StarPowerSection>> { sourceSpSections };

            chart.FiveFretTracks.GetStarpowerSections(ref acceptedSpSections, instrumentDifficulty.Instrument, tickTolerance);
            chart.SixFretTracks.GetStarpowerSections(ref acceptedSpSections, instrumentDifficulty.Instrument, tickTolerance);
            chart.DrumsTracks.GetStarpowerSections(ref acceptedSpSections, instrumentDifficulty.Instrument, tickTolerance);
            chart.ProKeys.GetStarpowerSections(ref acceptedSpSections, instrumentDifficulty.Instrument, tickTolerance);
            chart.Keys.GetStarpowerSections(ref acceptedSpSections, instrumentDifficulty.Instrument, tickTolerance);

            // Now we delete self from the accepted list to ensure we don't match against self
            acceptedSpSections.Remove(sourceSpSections);

            // Unpack all the accepted sp sections into a single list for easier comparison
            var othersSpSections = new List<StarPowerSection>();
            foreach (var sectionList in acceptedSpSections)
            {
                othersSpSections.AddRange(sectionList);
            }

            var phrases = new List<UnisonPhrase>();
            var potentialGroup = new List<StarPowerSection>();
            var finalParticipants = new List<StarPowerSection>();

            // For each of the player's SP phrases, see if it's part of a valid unison
            foreach (var sourceSection in sourceSpSections)
            {
                // Find all phrases that start at roughly the same time
                potentialGroup.Clear();
                potentialGroup.Add(sourceSection);

                foreach (var otherSection in othersSpSections)
                {
                    if (TickWithinTolerance(sourceSection.Tick, otherSection.Tick, tickTolerance))
                    {
                        potentialGroup.Add(otherSection);
                    }
                }

                // A unison needs at least two participants
                if (potentialGroup.Count < 2)
                {
                    continue;
                }

                // Find the benchmark phrase (the one that starts earliest)
                var benchmark = potentialGroup[0];
                for (int i = 1; i < potentialGroup.Count; i++)
                {
                    if (potentialGroup[i].Tick < benchmark.Tick)
                    {
                        benchmark = potentialGroup[i];
                    }
                }

                // Find all phrases in the group that end at roughly the same time as the benchmark
                finalParticipants.Clear();
                foreach (var participant in potentialGroup)
                {
                    if (TickWithinTolerance(participant.TickEnd, benchmark.TickEnd, tickTolerance))
                    {
                        finalParticipants.Add(participant);
                    }
                }

                // If we still have at least two, it's a valid unison.
                if (finalParticipants.Count >= 2)
                {
                    var count = 0;
                    foreach (var note in instrumentDifficulty.Notes)
                    {
                        if (note.Tick < sourceSection.Tick)
                        {
                            continue;
                        }
                        if (note.Tick >= sourceSection.TickEnd)
                        {
                            break;
                        }
                        count += includeChildNotesInNoteCount ? note.ChildNotes.Count + 1 : 1;
                    }
                    phrases.Add(new UnisonPhrase(sourceSection.PhraseRef, count));
                }
            }

            return phrases;
        }

        public void OnStarPowerPhraseHit(EngineContainer container, double time)
        {
            // Find the relevant unison and increment its SuccessCount
            foreach (var unison in _unisonEvents)
            {
                if (!unison.ParticipantToPhrase.TryGetValue(container.EngineId, out var phrase))
                {
                    continue;
                }
                // The engine's conception of SP phrases for each instrument end at different times,
                // so an exact match is impossible even though the phrases have identical times
                if (phrase.Time <= time && time <= phrase.TimeEnd)
                {
                    if (unison.Success(container))
                    {
                        // Success returned true, so all the other players
                        // were also successful
                        YargLogger.LogDebug("EngineManager bonus SP award triggered");
                        AwardStarPowerBonus(unison);
                    }

                    return;
                }
            }
        }

        private void AwardStarPowerBonus(UnisonEvent unison)
        {
            if (unison.Awarded)
            {
                YargLogger.LogDebug("Attempted to award bonus SP, but it was already awarded");
                return;
            }
            foreach (var id in unison.ParticipantToPhrase.Keys)
            {
                YargLogger.LogFormatDebug("EngineManager awarding bonus SP to participant ID {0}", id);
                var engineContainer = _allEnginesById[id];
                engineContainer.SendCommand(EngineCommandType.AwardUnisonBonus);
            }
            unison.Awarded = true;
            OnUnisonPhraseSuccess?.Invoke();
        }
    }
}
