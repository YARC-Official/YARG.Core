// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System.Collections.Generic;

namespace MoonscraperChartEditor.Song
{
    internal class MoonChart
    {
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
        public List<MoonNote> notes { get; private set; } = new();
        /// <summary>
        /// Read only list of special phrases.
        /// </summary>
        public List<SpecialPhrase> specialPhrases { get; private set; } = new();
        /// <summary>
        /// Read only list of local events.
        /// </summary>
        public List<ChartEvent> events { get; private set; } = new();

        /// <summary>
        /// Creates a new chart object.
        /// </summary>
        /// <param name="_song">The song to associate this chart with.</param>
        public MoonChart(MoonSong _song, GameMode _gameMode)
        {
            song = _song;
            gameMode = _gameMode;
        }

        public MoonChart(MoonSong song, MoonSong.MoonInstrument Instrument) : this(song, MoonSong.InstumentToChartGameMode(Instrument))
        {
        }

        public void Clear()
        {
            notes.Clear();
            events.Clear();
            specialPhrases.Clear();
        }

        public int Add(MoonNote note)
        {
            return SongObjectHelper.Insert(note, notes);
        }

        public int Add(SpecialPhrase phrase)
        {
            return SongObjectHelper.Insert(phrase, specialPhrases);
        }

        public int Add(ChartEvent ev)
        {
            return SongObjectHelper.Insert(ev, events);
        }

        public bool Remove(MoonNote note)
        {
            return SongObjectHelper.Remove(note, notes);
        }

        public bool Remove(SpecialPhrase phrase)
        {
            return SongObjectHelper.Remove(phrase, specialPhrases);
        }

        public bool Remove(ChartEvent ev)
        {
            return SongObjectHelper.Remove(ev, events);
        }

        public bool IsOccupied()
        {
            return notes.Count > 0 || specialPhrases.Count > 0 || events.Count > 0;
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
