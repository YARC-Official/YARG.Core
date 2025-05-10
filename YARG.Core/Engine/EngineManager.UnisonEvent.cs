using System;
using System.Collections.Generic;
using YARG.Core.Chart;
using YARG.Core.Engine.Drums;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.ProKeys;
using YARG.Core.Logging;
using YARG.Core.Extensions;

namespace YARG.Core.Engine
{
    public partial class EngineManager
    {
        public class UnisonEvent : IEquatable<UnisonEvent>
        {
            public double    Time           { get; }
            public double    TimeEnd        { get; }
            public int       PartCount      { get; private set; }
            public int       SuccessCount   { get; private set; }
            public bool      Awarded        { get; set; }
            public List<int> ParticipantIds { get; private set; }

            public bool Equals(UnisonEvent other) => Time.Equals(other.Time) && TimeEnd.Equals(other.TimeEnd);

            // public bool Equals(double startTime, double endTime) => Time.Equals(startTime) && TimeEnd.Equals(endTime);
            // public override bool Equals(object obj) => Equals(obj as UnisonEvent);
            public override int GetHashCode() => HashCode.Combine(Time, TimeEnd);

            public UnisonEvent(double time, double timeEnd)
            {
                Time = time;
                TimeEnd = timeEnd;
                PartCount = 0;
                SuccessCount = 0;
                Awarded = false;
                ParticipantIds = new List<int>();
            }

            public void AddPlayer(EngineContainer engineContainer)
            {
                if (ParticipantIds.Contains(engineContainer.EngineId))
                {
                    return;
                }

                ParticipantIds.Add(engineContainer.EngineId);
                PartCount++;
            }

            // Returns true if all players succesfully completed the unison
            public bool Success(EngineContainer engineContainer)
            {
                if (ParticipantIds.Contains(engineContainer.EngineId))
                {
                    YargLogger.LogFormatDebug("Player {0} successfully completed unison ending at time {1}",
                        engineContainer.EngineId, TimeEnd);
                    SuccessCount++;
                }

                if (SuccessCount == ParticipantIds.Count)
                {
                    YargLogger.LogFormatDebug("Unison phrase ending at time {0} successfully completed by all participants",
                        TimeEnd);
                    return true;
                }

                // If SuccessCount is ever greater than the number of players, something has gone seriously wrong
                YargLogger.Assert(SuccessCount <= ParticipantIds.Count);
                return false;
            }
        }

        private List<UnisonEvent> _unisonEvents = new();

        public struct StarPowerSection : IEquatable<StarPowerSection>
        {
            public double Time;
            public double TimeEnd;
            public Phrase PhraseRef;

            public StarPowerSection(double time, double timeEnd, Phrase phrase)
            {
                Time = time;
                TimeEnd = timeEnd;
                PhraseRef = phrase;
            }

            public bool Equals(StarPowerSection other) => Time.Equals(other.Time) && TimeEnd.Equals(other.TimeEnd);

            public override bool Equals(object obj) => obj is StarPowerSection other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(Time, TimeEnd);
        }

        public delegate void UnisonPhrasesReadyEvent(List<UnisonEvent> unisonEvents);

        public delegate void UnisonPhraseSuccessEvent();

        public  UnisonPhraseSuccessEvent?                       OnUnisonPhraseSuccess;
        private bool                                            _unisonsReady      = false;
        private int                                             _playerCount  = 0;

        // Instrument groups whose combination cannot be the source of a unison
        public static readonly List<List<Instrument>> InstrumentGroups = new List<List<Instrument>>()
        {
            new List<Instrument> { Instrument.FiveFretGuitar, Instrument.ProGuitar_17Fret, Instrument.ProGuitar_22Fret, Instrument.SixFretGuitar },
            new List<Instrument> { Instrument.FiveFretBass, Instrument.ProBass_17Fret, Instrument.ProBass_22Fret, Instrument.SixFretBass },
            new List<Instrument> { Instrument.FourLaneDrums, Instrument.FiveLaneDrums, Instrument.ProDrums, Instrument.EliteDrums },
            new List<Instrument> { Instrument.Keys, Instrument.ProKeys }
        };

        private void AddPlayerToUnisons(EngineContainer engineContainer)
        {
            foreach (var phrase in engineContainer.UnisonPhrases)
            {
                var unisonEvent = new UnisonEvent(phrase.Time, phrase.TimeEnd);
                if (!_unisonEvents.Contains(unisonEvent))
                {
                    _unisonEvents.Add(unisonEvent);
                    unisonEvent.AddPlayer(engineContainer);
                }
                else
                {
                    var idx = _unisonEvents.IndexOf(unisonEvent);
                    if (idx != -1)
                    {
                        _unisonEvents[idx].AddPlayer(engineContainer);
                    }
                }
            }
            // Subscribe the container to OnStarPowerPhraseHit so bonuses can be awarded as appropriate
            if (engineContainer.Engine is BaseEngine<GuitarNote,GuitarEngineParameters,GuitarStats> guitarEngine)
            {
                guitarEngine.OnStarPowerPhraseHit += engineContainer.OnStarPowerPhraseHit;
            }

            if (engineContainer.Engine is BaseEngine<DrumNote, DrumsEngineParameters, DrumsStats> drumEngine)
            {
                drumEngine.OnStarPowerPhraseHit += engineContainer.OnStarPowerPhraseHit;
            }

            if (engineContainer.Engine is BaseEngine<ProKeysNote, ProKeysEngineParameters, ProKeysStats>
                proKeysEngine)
            {
                proKeysEngine.OnStarPowerPhraseHit += engineContainer.OnStarPowerPhraseHit;
            }
            // Vocals don't participate in unisons, so they get left out.
        }

        /// <summary>
        /// Builds unison phrases for a combination of instrument and chart
        /// </summary>
        /// <param name="instrument">YARG.Core.Instrument</param>
        /// <param name="chart">YARG.Core.Chart.SongChart</param>
        /// <returns>List of Phrase objects with Type == PhraseType.StarPower
        /// <br />These Phrases have corresponding StarPower Phrases in other tracks,
        /// <br />which is what makes them unison phrases.
        /// </returns>
        public static List<Phrase> GetUnisonPhrases(Instrument instrument, SongChart chart)
        {
            var phrases = new List<Phrase>();
            // the list we will compare against to find unisons
            var sourceSpSections = new List<StarPowerSection>();
            var othersSpSections = new List<StarPowerSection>();
            var outSpSections = new List<StarPowerSection>();
            // This list should have the instruments with duplicate SP phrases removed
            var acceptedSpSections = new List<List<StarPowerSection>>();

            // Since vocals can't have unisons, we may as well pull the ripcord early
            if (instrument is Instrument.Vocals or Instrument.Harmony)
            {
                return phrases;
            }

            var foundSelf = false;

            // Find a track that corresponds to the player's instrument
            if(TryFindTrackForInstrument(instrument, chart.FiveFretTracks, out var fiveFretTrack))
            {
                // var selfInstrumentDifficulty = GetAnyInstrumentDifficulty(fiveFretTrack);
                // sourceSpSections = GetSpSectionsFromDifficulty(selfInstrumentDifficulty);
                sourceSpSections = fiveFretTrack.FirstDifficulty().GetStarpowerSections();
                foundSelf = true;
            }

            if (!foundSelf && TryFindTrackForInstrument(instrument, chart.DrumsTracks, out var drumsTrack))
            {
                sourceSpSections = drumsTrack.FirstDifficulty().GetStarpowerSections();
                foundSelf = true;
            }

            if (!foundSelf && TryFindTrackForInstrument(instrument, chart.SixFretTracks, out var sixFretTrack))
            {
                sourceSpSections = sixFretTrack.FirstDifficulty().GetStarpowerSections();
                foundSelf = true;
            }

            if (!foundSelf && TryFindTrackForInstrument(instrument, chart.ProGuitarTracks, out var proGuitarTrack))
            {
                sourceSpSections = proGuitarTrack.FirstDifficulty().GetStarpowerSections();
                foundSelf = true;
            }

            if (!foundSelf && chart.ProKeys.Instrument == instrument)
            {
                sourceSpSections = chart.ProKeys.FirstDifficulty().GetStarpowerSections();
                foundSelf = true;
            }

            if (!foundSelf && chart.Keys.Instrument == instrument)
            {
                sourceSpSections = chart.Keys.FirstDifficulty().GetStarpowerSections();
                foundSelf = true;
            }

            if (!foundSelf)
            {
                YargLogger.LogFormatError("Could not find any instrument difficulty for {0}", instrument);
                return phrases;
            }

            // Add ourselves to the beginning of the accepted list so any dupes with us will be filtered
            acceptedSpSections.Add(sourceSpSections);

            chart.FiveFretTracks.GetStarpowerSections(ref acceptedSpSections, instrument);
            chart.SixFretTracks.GetStarpowerSections(ref acceptedSpSections, instrument);
            chart.DrumsTracks.GetStarpowerSections(ref acceptedSpSections, instrument);
            chart.ProKeys.GetStarpowerSections(ref acceptedSpSections, instrument);
            chart.Keys.GetStarpowerSections(ref acceptedSpSections, instrument);

            // Now we delete self from the accepted list to ensure we don't match against self
            acceptedSpSections.Remove(sourceSpSections);

            // Unpack all the accepted sp sections into othersSpSections
            foreach (var section in acceptedSpSections)
            {
                othersSpSections.AddRange(section);
            }

            // Now that we have all the SP sections, compare them
            foreach (var section in othersSpSections)
            {
                if (sourceSpSections.Contains(section) && !outSpSections.Contains(section))
                {
                    outSpSections.Add(section);
                }
            }

            // Build the phrase list we actually want to return
            foreach (var section in outSpSections)
            {
                phrases.Add(section.PhraseRef);
            }

            return phrases;

            // Get the track for a given instrument, if it exists
            static bool TryFindTrackForInstrument<TNote>(Instrument instrument,
                IEnumerable<InstrumentTrack<TNote>> trackEnumerable, out InstrumentTrack<TNote> instrumentTrack) where TNote : Note<TNote>
            {
                foreach (var track in trackEnumerable)
                {
                    if (track.Instrument == instrument)
                    {
                        instrumentTrack = track;
                        return true;
                    }
                }

                instrumentTrack = null;
                return false;
            }
        }

        public void OnStarPowerPhraseHit(EngineContainer container, double time)
        {
            // Find the relevant unison and increment its SuccessCount
            foreach (var unison in _unisonEvents)
            {
                // The engine's conception of SP phrases for each instrument end at different times,
                // so an exact match is impossible even though the phrases have identical times
                if (unison.Time <= time && time <= unison.TimeEnd)
                {
                    if (unison.Success(container))
                    {
                        // Success returned true, so all the other players
                        // were also successful
                        YargLogger.LogDebug("EngineManager bonus SP award triggered");
                        AwardStarPowerBonus(unison);
                    }
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
            foreach (var id in unison.ParticipantIds)
            {
                YargLogger.LogFormatDebug("EngineManager awarding bonus SP to participant ID {0}", id);
                var engineContainer = _allEnginesById[id];
                engineContainer.SendCommand(EngineContainer.EngineCommandType.AwardUnisonBonus);
            }
            unison.Awarded = true;
        }

        private void IncreaseBandMultiplier()
        {
            foreach (var container in _allEngines)
            {
                // var input = new GameInput(container.Engine.CurrentTime, (int) BandAction.StepMultiplier, true);
            }
        }

        private void DecreaseBandMultiplier()
        {
            foreach (var container in _allEngines)
            {
                // var input = new GameInput(container.Engine.CurrentTime, (int) BandAction.StepMultiplier, false);
            }
        }

        public List<UnisonEvent>? GetUnisonEvents()
        {
            if (_unisonsReady)
            {
                return _unisonEvents;
            }

            return null;
        }
    }
}