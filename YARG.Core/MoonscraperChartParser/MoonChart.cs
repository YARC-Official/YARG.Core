// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System.Collections.Generic;

namespace MoonscraperChartEditor.Song
{
    public class MoonChart
    {
        private readonly MoonSong _song;
        private readonly List<ChartObject> _chartObjects;
        private int _note_count;
        private readonly GameMode _gameMode;

        /// <summary>
        /// Read only list of notes.
        /// </summary>
        public SongObjectCache<MoonNote> notes { get; private set; }
        /// <summary>
        /// Read only list of special phrases.
        /// </summary>
        public SongObjectCache<SpecialPhrase> specialPhrases { get; private set; }
        /// <summary>
        /// Read only list of local events.
        /// </summary>
        public SongObjectCache<ChartEvent> events { get; private set; }
        /// <summary>
        /// The song this chart is connected to.
        /// </summary>
        public MoonSong MoonSong => _song;
        /// <summary>
        /// The game mode the chart is designed for
        /// </summary>
        public GameMode gameMode => _gameMode;

        /// <summary>
        /// Read only list containing all chart notes, special phrases, and text events.
        /// </summary>
        public ReadOnlyList<ChartObject> chartObjects;

        /// <summary>
        /// The total amount of notes in the chart, counting chord (notes sharing the same tick position) as a single note.
        /// </summary>
        public int note_count => _note_count;

        /// <summary>
        /// Creates a new chart object.
        /// </summary>
        /// <param name="song">The song to associate this chart with.</param>
        /// <param name="name">The name of the chart (easy single, expert double guitar, etc.</param>
        public MoonChart(MoonSong song, GameMode gameMode)
        {
            _song = song;
            _chartObjects = new List<ChartObject>();
            chartObjects = new ReadOnlyList<ChartObject>(_chartObjects);
            _gameMode = gameMode;

            notes = new SongObjectCache<MoonNote>();
            specialPhrases = new SongObjectCache<SpecialPhrase>();
            events = new SongObjectCache<ChartEvent>();

            _note_count = 0;
        }

        public MoonChart(MoonSong song, MoonSong.MoonInstrument Instrument) : this(song, MoonSong.InstumentToChartGameMode(Instrument))
        {
        }

        public MoonChart(MoonChart chart, MoonSong song)
        {
            _song = song;
            _gameMode = chart.gameMode;

            _chartObjects = new List<ChartObject>();
            _chartObjects.AddRange(chart._chartObjects);

            chartObjects = new ReadOnlyList<ChartObject>(_chartObjects);
        }

        /// <summary>
        /// Updates all read-only values and the total note count.
        /// </summary>
        public void UpdateCache()
        {
            MoonSong.UpdateCacheList(notes, _chartObjects);
            MoonSong.UpdateCacheList(specialPhrases, _chartObjects);
            MoonSong.UpdateCacheList(events, _chartObjects);

            _note_count = GetNoteCount();
        }

        private int GetNoteCount()
        {
            if (notes.Count > 0)
            {
                int count = 1;

                uint previousPos = notes[0].tick;
                for (int i = 1; i < notes.Count; ++i)
                {
                    if (notes[i].tick > previousPos)
                    {
                        ++count;
                        previousPos = notes[i].tick;
                    }
                }

                return count;
            }
            else
                return 0;
        }

        public void SetCapacity(int size)
        {
            if (size > _chartObjects.Capacity)
                _chartObjects.Capacity = size;
        }

        public void Clear()
        {
            _chartObjects.Clear();
        }

        /// <summary>
        /// Adds a chart object (note, special phrase, and/or chart event) into the chart.
        /// </summary>
        /// <param name="chartObject">The item to add</param>
        /// <param name="update">Automatically update all read-only arrays? 
        /// If set to false, you must manually call the updateArrays() method, but is useful when adding multiple objects as it increases performance dramatically.</param>
        public int Add(ChartObject chartObject, bool update = true)
        {
            chartObject.chart = this;
            chartObject.song = _song;

            int pos = SongObjectHelper.Insert(chartObject, _chartObjects);

            if (update)
                UpdateCache();

            return pos;
        }

        /// <summary>
        /// Removes a chart object (note, special phrase, and/or chart event) from the chart.
        /// </summary>
        /// <param name="chartObject">Item to add.</param>
        /// <param name="update">Automatically update all read-only arrays? 
        /// If set to false, you must manually call the updateArrays() method, but is useful when removing multiple objects as it increases performance dramatically.</param>
        /// <returns>Returns whether the removal was successful or not (item may not have been found if false).</returns>
        public bool Remove(ChartObject chartObject, bool update = true)
        {
            bool success = SongObjectHelper.Remove(chartObject, _chartObjects);

            if (success)
            {
                chartObject.chart = null;
                chartObject.song = null;
            }

            if (update)
                UpdateCache();

            return success;
        }

        public enum GameMode
        {
            Guitar,
            Drums,
            GHLGuitar,
            ProGuitar,

            Unrecognised,
        }
    }
}
