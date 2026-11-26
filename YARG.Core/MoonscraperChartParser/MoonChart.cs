// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System.Collections;
using System.Collections.Generic;

namespace MoonscraperChartEditor.Song
{
    internal class MoonChart
    {
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
        public List<MoonPhrase> specialPhrases { get; private set; } = new();
        /// <summary>
        /// Read only list of local events.
        /// </summary>
        public List<MoonText> events { get; private set; } = new();

        /// <summary>
        /// Read only list of animation events.
        /// </summary>
        public List<MoonAnimation> animations { get; private set; } = new();

        public bool IsEmpty => notes.Count == 0 && specialPhrases.Count == 0 && events.Count == 0;

        /// <summary>
        /// Creates a new chart object.
        /// </summary>
        public MoonChart(GameMode _gameMode)
        {
            gameMode = _gameMode;
        }

        public MoonChart(MoonSong.MoonInstrument Instrument)
            : this(MoonSong.InstrumentToChartGameMode(Instrument))
        {
        }

        public void Clear()
        {
            notes.Clear();
            events.Clear();
            specialPhrases.Clear();
        }

        public void Add(MoonNote note)
        {
            MoonObjectHelper.AddToEnd(note, notes);
        }

        public void Add(MoonPhrase phrase)
        {
            MoonObjectHelper.AddToEnd(phrase, specialPhrases);
        }

        public void Add(MoonText ev)
        {
            MoonObjectHelper.AddToEnd(ev, events);
        }

        public int Insert(MoonNote note)
        {
            return MoonObjectHelper.Insert(note, notes);
        }

        public int Insert(MoonPhrase phrase)
        {
            return MoonObjectHelper.Insert(phrase, specialPhrases);
        }

        public int Insert(MoonText ev)
        {
            return MoonObjectHelper.Insert(ev, events);
        }

        public int Add(MoonAnimation ev)
        {
            return MoonObjectHelper.Insert(ev, animations);
        }

        public bool Remove(MoonNote note)
        {
            return MoonObjectHelper.Remove(note, notes);
        }

        public bool Remove(MoonPhrase phrase)
        {
            return MoonObjectHelper.Remove(phrase, specialPhrases);
        }

        public bool Remove(MoonText ev)
        {
            return MoonObjectHelper.Remove(ev, events);
        }

        /// <summary>
        /// In pro drums, N 4 is a green cymbal or tom and N 5 is a green tom, so they can't share a tick.
        /// An N 5 on the tick of an N 4 becomes a blue tom, with its own length, accent or ghost, or is dropped
        /// when the tick has an N 3 (a blue cymbal or tom), which keeps its own type, flags and length.
        /// Of several N 5 notes on such a tick without an N 3, the longest becomes the blue tom (the first of the
        /// longest), as two notes of one pad keep the longer.
        /// </summary>
        public void ResolveProDrumsGreenNotes()
        {
            int start = 0;
            while (start < notes.Count)
            {
                // Notes of one tick are adjacent, but in the order they were read, not sorted by pad
                uint tick = notes[start].tick;
                int end = start;
                bool green = false;
                bool blue = false;
                for (; end < notes.Count && notes[end].tick == tick; ++end)
                {
                    green |= notes[end].drumPad is MoonNote.DrumPad.Orange;
                    blue |= notes[end].drumPad is MoonNote.DrumPad.Blue;
                }

                // The N 5 that becomes the blue tom, if the tick has no N 3
                int keep = -1;
                for (int i = start; green && !blue && i < end; ++i)
                {
                    if (notes[i].drumPad is MoonNote.DrumPad.Green && (keep < 0 || notes[i].length > notes[keep].length))
                        keep = i;
                }

                for (int i = start; green && i < end;)
                {
                    var note = notes[i];
                    if (note.drumPad is not MoonNote.DrumPad.Green)
                    {
                        ++i;
                    }
                    else if (i == keep)
                    {
                        // A tom whatever the blue markers say
                        note.drumPad = MoonNote.DrumPad.Blue;
                        note.flags &= ~MoonNote.Flags.ProDrums_Cymbal;
                        ++i;
                    }
                    else
                    {
                        if (note.previous != null)
                            note.previous.next = note.next;
                        if (note.next != null)
                            note.next.previous = note.previous;
                        notes.RemoveAt(i);
                        --end;
                        if (keep > i)
                            --keep;
                    }
                }

                start = end;
            }
        }

        public enum GameMode
        {
            Guitar,
            Drums,
            GHLGuitar,
            ProGuitar,
            ProKeys,
            Vocals,
            EliteDrums,
        }
    }
}
