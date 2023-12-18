// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using YARG.Core.Extensions;

namespace MoonscraperChartEditor.Song
{
    internal class MoonSong
    {
        // Song properties
        public Metadata metaData = new();

        public string name
        {
            get => metaData.name;
            set => metaData.name = value;
        }

        public float resolution;
        public float hopoThreshold;
        public float offset = 0;

        public float? manualLength = null;

        // Charts
        private readonly MoonChart[] charts;

        public IReadOnlyList<MoonChart> Charts => charts.ToList();

        /// <summary>
        /// Read only list of song events.
        /// </summary>
        public List<TextEvent> events { get; private set; } = new();
        /// <summary>
        /// Read only list of song sections.
        /// </summary>
        public List<TextEvent> sections { get; private set; } = new();
        /// <summary>
        /// Read only list of venue events.
        /// </summary>
        public List<VenueEvent> venue { get; private set; } = new();

        /// <summary>
        /// Read only list of a song's bpm changes.
        /// </summary>
        public List<BPM> bpms { get; private set; } = new();
        /// <summary>
        /// Read only list of a song's time signature changes.
        /// </summary>
        public List<TimeSignature> timeSignatures { get; private set; } = new();
        /// <summary>
        /// Read only list of a song's beats.
        /// </summary>
        public List<Beat> beats { get; private set; } = new();

        /// <summary>
        /// Default constructor for a new chart. Initialises all lists and adds locked bpm and timesignature objects.
        /// </summary>
        public MoonSong()
        {
            // Chart initialisation
            charts = new MoonChart[EnumExtensions<MoonInstrument>.Count * EnumExtensions<Difficulty>.Count];
            for (int i = 0; i < charts.Length; ++i)
            {
                var instrument = (MoonInstrument)(i / EnumExtensions<Difficulty>.Count);
                charts[i] = new MoonChart(this, instrument);
            }
        }

        public MoonChart GetChart(MoonInstrument instrument, Difficulty difficulty)
        {
            return charts[(int)instrument * EnumExtensions<Difficulty>.Count + (int)difficulty];
        }

        public bool ChartExistsForInstrument(MoonInstrument instrument)
        {
            foreach (var difficulty in EnumExtensions<Difficulty>.Values)
            {
                var chart = GetChart(instrument, difficulty);
                if (chart.IsOccupied())
                {
                    return true;
                }
            }

            return false;
        }

        public bool DoesChartExist(MoonInstrument instrument, Difficulty difficulty)
        {
            return GetChart(instrument, difficulty).IsOccupied();
        }

        /// <summary>
        /// Converts a time value into a tick position value. May be inaccurate due to interger rounding.
        /// </summary>
        /// <param name="time">The time (in seconds) to convert.</param>
        /// <param name="resolution">Ticks per beat, usually provided from the resolution song of a Song class.</param>
        /// <returns>Returns the calculated tick position.</returns>
        public uint TimeToTick(double time, float resolution)
        {
            if (time < 0)
                time = 0;

            var prevBPM = bpms[0];

            // Search for the last bpm
            for (int i = 0; i < bpms.Count; ++i)
            {
                var bpmInfo = bpms[i];
                if (bpmInfo.assignedTime >= time)
                    break;
                else
                    prevBPM = bpmInfo;
            }

            uint position = prevBPM.tick;
            position += TickFunctions.TimeToDis(prevBPM.assignedTime, time, resolution, prevBPM.value);

            return position;
        }

        /// <summary>
        /// Finds the value of the first bpm that appears before or on the specified tick position.
        /// </summary>
        /// <param name="position">The tick position</param>
        /// <returns>Returns the value of the bpm that was found.</returns>
        public BPM? GetPrevBPM(uint position)
        {
            return SongObjectHelper.GetPrevious(bpms, position);
        }

        /// <summary>
        /// Finds the value of the first time signature that appears before the specified tick position.
        /// </summary>
        /// <param name="position">The tick position</param>
        /// <returns>Returns the value of the time signature that was found.</returns>
        public TimeSignature? GetPrevTS(uint position)
        {
            return SongObjectHelper.GetPrevious(timeSignatures, position);
        }

        public TextEvent? GetPrevSection(uint position)
        {
            return SongObjectHelper.GetPrevious(sections, position);
        }

        /// <summary>
        /// Converts a tick position into the time it will appear in the song.
        /// </summary>
        /// <param name="position">Tick position.</param>
        /// <returns>Returns the time in seconds.</returns>
        public double TickToTime(uint position)
        {
            return TickToTime(position, resolution);
        }

        /// <summary>
        /// Converts a tick position into the time it will appear in the song.
        /// </summary>
        /// <param name="position">Tick position.</param>
        /// <param name="resolution">Ticks per beat, usually provided from the resolution song of a Song class.</param>
        /// <returns>Returns the time in seconds.</returns>
        public double TickToTime(uint position, float resolution)
        {
            int previousBPMPos = SongObjectHelper.FindClosestPosition(position, bpms);
            if (bpms[previousBPMPos].tick > position)
                --previousBPMPos;

            var prevBPM = bpms[previousBPMPos];
            double time = prevBPM.assignedTime;
            time += TickFunctions.DisToTime(prevBPM.tick, position, resolution, prevBPM.value);

            return time;
        }

        public void Add(Beat beat)
        {
            SongObjectHelper.Insert(beat, beats);
        }

        public void Add(TimeSignature timeSig)
        {
            SongObjectHelper.Insert(timeSig, timeSignatures);
        }

        public void Add(BPM bpm)
        {
            SongObjectHelper.Insert(bpm, bpms);
        }

        public bool Remove(Beat beat)
        {
            return beat.tick > 0 && SongObjectHelper.Remove(beat, beats);
        }

        public bool Remove(TimeSignature timeSig)
        {
            return timeSig.tick > 0 && SongObjectHelper.Remove(timeSig, timeSignatures);
        }

        public bool Remove(BPM bpm)
        {
            return bpm.tick > 0 && SongObjectHelper.Remove(bpm, bpms);
        }

        public void Add(TextEvent ev)
        {
            SongObjectHelper.Insert(ev, events);
        }

        public void AddSection(TextEvent section)
        {
            SongObjectHelper.Insert(section, sections);
        }

        public void Add(VenueEvent venueEvent)
        {
            SongObjectHelper.Insert(venueEvent, venue);
        }

        public bool Remove(TextEvent ev)
        {
            return SongObjectHelper.Remove(ev, events);
        }

        public bool RemoveSection(TextEvent section)
        {
            return SongObjectHelper.Remove(section, sections);
        }

        public bool Remove(VenueEvent venueEvent)
        {
            return SongObjectHelper.Remove(venueEvent, venue);
        }

        /// <summary>
        /// Dramatically speeds up calculations of songs with lots of bpm changes.
        /// </summary>
        public void UpdateBPMTimeValues()
        {
            /*
             * Essentially just an optimised version of this, as this was n^2 and bad
             * foreach (var bpm in bpms)
             * {
             *     bpm.assignedTime = LiveTickToTime(bpm.tick, resolution);
             * }
            */

            if (bpms.Count == 0 || bpms[0].tick != 0)
                bpms.Insert(0, new BPM());

            if (timeSignatures.Count == 0 || timeSignatures[0].tick != 0)
                timeSignatures.Insert(0, new TimeSignature());

            double time = 0;
            var prevBPM = bpms[0];
            prevBPM.assignedTime = 0;

            foreach (var bpm in bpms)
            {
                time += TickFunctions.DisToTime(prevBPM.tick, bpm.tick, resolution, prevBPM.value);
                bpm.assignedTime = time;
                prevBPM = bpm;
            }
        }

        public double LiveTickToTime(uint position, float resolution)
        {
            return LiveTickToTime(position, resolution, bpms[0], bpms);
        }

        public static double LiveTickToTime(uint position, float resolution, BPM initialBpm, List<BPM> synctrack)
        {
            double time = 0;
            var prevBPM = initialBpm;

            foreach (var syncTrack in synctrack)
            {
                var bpmInfo = syncTrack as BPM;

                if (bpmInfo == null)
                    continue;

                if (bpmInfo.tick > position)
                {
                    break;
                }
                else
                {
                    time += TickFunctions.DisToTime(prevBPM.tick, bpmInfo.tick, resolution, prevBPM.value);
                    prevBPM = bpmInfo;
                }
            }

            time += TickFunctions.DisToTime(prevBPM.tick, position, resolution, prevBPM.value);

            return time;
        }

        public float ResolutionScaleRatio(float targetResoltion)
        {
            return targetResoltion / resolution;
        }

        public static MoonChart.GameMode InstumentToChartGameMode(MoonInstrument instrument)
        {
            switch (instrument)
            {
                case MoonInstrument.Guitar:
                case MoonInstrument.GuitarCoop:
                case MoonInstrument.Bass:
                case MoonInstrument.Rhythm:
                case MoonInstrument.Keys:
                    return MoonChart.GameMode.Guitar;

                case MoonInstrument.Drums:
                    return MoonChart.GameMode.Drums;

                case MoonInstrument.GHLiveGuitar:
                case MoonInstrument.GHLiveBass:
                case MoonInstrument.GHLiveRhythm:
                case MoonInstrument.GHLiveCoop:
                    return MoonChart.GameMode.GHLGuitar;

                case MoonInstrument.ProGuitar_17Fret:
                case MoonInstrument.ProGuitar_22Fret:
                case MoonInstrument.ProBass_17Fret:
                case MoonInstrument.ProBass_22Fret:
                    return MoonChart.GameMode.ProGuitar;

                case MoonInstrument.Vocals:
                case MoonInstrument.Harmony1:
                case MoonInstrument.Harmony2:
                case MoonInstrument.Harmony3:
                    return MoonChart.GameMode.Vocals;

                default:
                    throw new NotImplementedException($"Unhandled instrument {instrument}!");
            }
        }

        public enum Difficulty
        {
            Expert = 0,
            Hard = 1,
            Medium = 2,
            Easy = 3
        }

        public enum MoonInstrument
        {
            Guitar,
            GuitarCoop,
            Bass,
            Rhythm,
            Keys,
            Drums,
            GHLiveGuitar,
            GHLiveBass,
            GHLiveRhythm,
            GHLiveCoop,
            ProGuitar_17Fret,
            ProGuitar_22Fret,
            ProBass_17Fret,
            ProBass_22Fret,
            Vocals,
            Harmony1,
            Harmony2,
            Harmony3,
        }

        public enum AudioInstrument
        {
            // Keep these in numerical order, there are a few places we're looping over these by casting to avoid GC allocs
            Song = 0,
            Guitar = 1,
            Bass = 2,
            Rhythm = 3,
            Drum = 4,
            Drums_2 = 5,
            Drums_3 = 6,
            Drums_4 = 7,
            Vocals = 8,
            Keys = 9,
            Crowd = 10
        }
    }

    internal class ReadOnlyList<T> : IList<T>, IEnumerable<T>
    {
        private readonly List<T> _realListHandle;
        public ReadOnlyList(List<T> realListHandle)
        {
            _realListHandle = realListHandle;
        }

        public T this[int index] { get { return _realListHandle[index]; } set { _realListHandle[index] = value; } }

        public int Count
        {
            get { return _realListHandle.Count; }
        }

        public bool IsReadOnly
        {
            get { return true; }
        }

        public void Add(T item)
        {
            throw new NotSupportedException();
        }

        public void Clear()
        {
            throw new NotSupportedException();
        }

        public bool Contains(T item)
        {
            return _realListHandle.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {

        }

        public IEnumerator<T> GetEnumerator()
        {
            return _realListHandle.GetEnumerator();
        }

        public int IndexOf(T item)
        {
            return _realListHandle.IndexOf(item);
        }

        public void Insert(int index, T item)
        {
            throw new NotSupportedException();
        }

        public bool Remove(T item)
        {
            throw new NotSupportedException();
        }

        public void RemoveAt(int index)
        {
            throw new NotSupportedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _realListHandle.GetEnumerator();
        }

        public T[] ToArray()
        {
            return _realListHandle.ToArray();
        }
    }
}
