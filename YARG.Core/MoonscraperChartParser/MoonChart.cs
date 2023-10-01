// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System.Collections.Generic;

namespace MoonscraperChartEditor.Song
{
    internal class MoonChart
    {
        private readonly List<ChartObject> _chartObjects = new();

        /// <summary>
        /// The song this chart is connected to.
        /// </summary>
        public MoonSong song { get; private set; }
        /// <summary>
        /// The game mode the chart is designed for
        /// </summary>
        public GameMode gameMode { get; private set; }

        /// <summary>
        /// Read only list of notes.
        /// </summary>
        public SongObjectCache<MoonNote> notes { get; private set; } = new();
        /// <summary>
        /// Read only list of special phrases.
        /// </summary>
        public SongObjectCache<SpecialPhrase> specialPhrases { get; private set; } = new();
        /// <summary>
        /// Read only list of local events.
        /// </summary>
        public SongObjectCache<ChartEvent> events { get; private set; } = new();

        /// <summary>
        /// Read only list containing all chart notes, special phrases, and text events.
        /// </summary>
        public ReadOnlyList<ChartObject> chartObjects { get; private set; }

        /// <summary>
        /// Creates a new chart object.
        /// </summary>
        /// <param name="_song">The song to associate this chart with.</param>
        public MoonChart(MoonSong _song, GameMode _gameMode)
        {
            song = _song;
            gameMode = _gameMode;

            chartObjects = new ReadOnlyList<ChartObject>(_chartObjects);
        }

        public MoonChart(MoonSong song, MoonSong.MoonInstrument Instrument) : this(song, MoonSong.InstumentToChartGameMode(Instrument))
        {
        }

        /// <summary>
        /// Updates all read-only values and the total note count.
        /// </summary>
        public void UpdateCache()
        {
            MoonSong.UpdateCacheList(notes, _chartObjects);
            MoonSong.UpdateCacheList(specialPhrases, _chartObjects);
            MoonSong.UpdateCacheList(events, _chartObjects);
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
            chartObject.song = song;

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
            Vocals,
        }
    }
}
