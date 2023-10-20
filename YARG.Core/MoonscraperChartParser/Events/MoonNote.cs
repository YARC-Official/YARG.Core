﻿// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;

namespace MoonscraperChartEditor.Song
{
    [Serializable]
    internal class MoonNote : ChartObject
    {
        public enum GuitarFret
        {
            // Assign to the sprite array position
            Green = 0,
            Red = 1,
            Yellow = 2,
            Blue = 3,
            Orange = 4,
            Open = 5
        }

        public enum DrumPad
        {
            // Wrapper to account for how the frets change colours between the drums and guitar tracks from the GH series  
            Red = GuitarFret.Green,
            Yellow = GuitarFret.Red,
            Blue = GuitarFret.Yellow,
            Orange = GuitarFret.Blue,
            Green = GuitarFret.Orange,
            Kick = GuitarFret.Open,
        }

        public enum GHLiveGuitarFret
        {
            // Assign to the sprite array position
            Black1,
            Black2,
            Black3,
            White1,
            White2,
            White3,
            Open
        }

        public enum ProGuitarString
        {
            Red,
            Green,
            Orange,
            Blue,
            Yellow,
            Purple
        }

        private const int PRO_GUITAR_FRET_OFFSET = 0;
        private const int PRO_GUITAR_FRET_MASK = 0x1F << PRO_GUITAR_FRET_OFFSET;
        private const int PRO_GUITAR_STRING_OFFSET = 5;
        private const int PRO_GUITAR_STRING_MASK = 0x07 << PRO_GUITAR_STRING_OFFSET;

        public enum MoonNoteType
        {
            Natural,
            Strum,
            Hopo,
            Tap,
            Cymbal,
        }

        [Flags]
        // TODO: These need to be organized a little better down the line
        public enum Flags
        {
            None = 0,

            // Guitar
            Forced = 1 << 0,
            Tap = 1 << 1,

            // Pro Guitar
            ProGuitar_Muted = 1 << 2,

            // Vocals
            Vocals_Percussion = 1 << 3,

            // RB Pro Drums
            ProDrums_Cymbal = 1 << 6,

            // Generic flag that mainly represents mechanics from Guitar Hero's Expert+ filtered drum notes such as Double Kick. This may apply to any difficulty now though.
            InstrumentPlus = 1 << 7,
            DoubleKick = InstrumentPlus,

            // FoF/PS Pro Drums
            ProDrums_Accent = 1 << 12,
            ProDrums_Ghost = 1 << 13,
        }

        private readonly ID _classID = ID.Note;
        public override int classID => (int)_classID;

        public uint length;
        public int rawNote;
        public GuitarFret guitarFret
        {
            get => (GuitarFret)rawNote;
            set => rawNote = (int)value;
        }

        public DrumPad drumPad => (DrumPad)guitarFret;

        public GHLiveGuitarFret ghliveGuitarFret
        {
            get => (GHLiveGuitarFret)rawNote;
            set => rawNote = (int)value;
        }

        public int proGuitarFret
        {
            get => (rawNote & PRO_GUITAR_FRET_MASK) >> PRO_GUITAR_FRET_OFFSET;
            set => rawNote = MakeProGuitarRawNote(proGuitarString, value);
        }

        public ProGuitarString proGuitarString
        {
            get => (ProGuitarString)((rawNote & PRO_GUITAR_STRING_MASK) >> PRO_GUITAR_STRING_OFFSET);
            set => rawNote = MakeProGuitarRawNote(value, proGuitarFret);
        }

        /// <summary>
        /// MIDI note of the vocals pitch, typically ranging from C2 (36) to C6 (84).
        /// </summary>
        public int vocalsPitch
        {
            get => rawNote;
            set => rawNote = Math.Clamp(value, 0, 127);
        }

        /// <summary>
        /// Properties, such as forced or taps, are stored here in a bitwise format.
        /// </summary>
        public Flags flags;

        /// <summary>
        /// The previous note in the linked-list.
        /// </summary>
        public MoonNote? previous;
        /// <summary>
        /// The next note in the linked-list.
        /// </summary>
        public MoonNote? next;

        public Chord chord => new(this);

        public MoonNote(uint _position, int _rawNote, uint _sustain = 0, Flags _flags = Flags.None) : base(_position)
        {
            length = _sustain;
            flags = _flags;
            rawNote = _rawNote;

            previous = null;
            next = null;
        }

        public bool forced
        {
            get
            {
                return (flags & Flags.Forced) == Flags.Forced;
            }
            set
            {
                if (value)
                    flags |= Flags.Forced;
                else
                    flags &= ~Flags.Forced;
            }
        }

        /// <summary>
        /// Gets the next note in the linked-list that's not part of this note's chord.
        /// </summary>
        public MoonNote? NextSeperateMoonNote
        {
            get
            {
                var nextMoonNote = next;
                while (nextMoonNote != null && nextMoonNote.tick == tick)
                    nextMoonNote = nextMoonNote.next;
                return nextMoonNote;
            }
        }

        /// <summary>
        /// Gets the previous note in the linked-list that's not part of this note's chord.
        /// </summary>
        public MoonNote? PreviousSeperateMoonNote
        {
            get
            {
                var previousMoonNote = previous;
                while (previousMoonNote != null && previousMoonNote.tick == tick)
                    previousMoonNote = previousMoonNote.previous;
                return previousMoonNote;
            }
        }

        protected override bool Equals(SongObject b)
        {
            if (b.GetType() == typeof(MoonNote))
            {
                var realB = (MoonNote) b;
                if (tick == realB.tick && rawNote == realB.rawNote)
                    return true;
                else
                    return false;
            }
            else
                return base.Equals(b);
        }

        protected override bool LessThan(SongObject b)
        {
            if (b.GetType() == typeof(MoonNote))
            {
                var realB = (MoonNote) b;
                if (tick < b.tick)
                    return true;
                else if (tick == b.tick)
                {
                    if (rawNote < realB.rawNote)
                        return true;
                }

                return false;
            }
            else
                return base.LessThan(b);
        }

        public bool isChord => (previous != null && previous.tick == tick) || (next != null && next.tick == tick);

        /// <summary>
        /// Ignores the note's forced flag when determining whether it would be a hopo or not
        /// </summary>
        public bool IsNaturalHopo(float hopoThreshold)
        {
            // Checking state in this order is important
            return !isChord &&
                previous != null &&
                (previous.isChord || rawNote != previous.rawNote) &&
                tick - previous.tick <= hopoThreshold;
        }

        /// <summary>
        /// Would this note be a hopo or not? (Ignores whether the note's tap flag is set or not.)
        /// </summary>
        public bool IsHopo(float hopoThreshold)
        {
            // F + F || T + T = strum
            return IsNaturalHopo(hopoThreshold) != forced;
        }

        /// <summary>
        /// Returns a bit mask representing the whole note's chord. For example, a green, red and blue chord would have a mask of 0000 1011. A yellow and orange chord would have a mask of 0001 0100. 
        /// Shifting occurs accoring the values of the Fret_Type enum, so open notes currently output with a mask of 0010 0000.
        /// </summary>
        public int mask
        {
            get
            {
                // Don't interate using chord, as chord will get messed up for the tool notes which override their linked list references. 
                int mask = 1 << rawNote;

                var note = this;
                while (note.previous != null && note.previous.tick == tick)
                {
                    note = note.previous;
                    mask |= (1 << note.rawNote);
                }

                note = this;
                while (note.next != null && note.tick == note.next.tick)
                {
                    note = note.next;
                    mask |= (1 << note.rawNote);
                }

                return mask;
            }
        }

        public int GetMaskWithRequiredFlags(Flags flags)
        {
            int mask = 0;

            foreach (var note in chord)
            {
                if (note.flags == flags)
                    mask |= 1 << note.rawNote;
            }

            return mask;
        }

        /// <summary>
        /// Live calculation of what Note_Type this note would currently be. 
        /// </summary>
        public MoonNoteType GetGuitarType(float hopoThreshold)
        {
            if (!IsOpenNote(MoonChart.GameMode.Guitar) && (flags & Flags.Tap) != 0)
            {
                return MoonNoteType.Tap;
            }
            return IsHopo(hopoThreshold) ? MoonNoteType.Hopo : MoonNoteType.Strum;
        }

        public MoonNoteType drumType
        {
            get
            {
                if (drumPad is DrumPad.Yellow or DrumPad.Blue or DrumPad.Orange &&
                           (flags & Flags.ProDrums_Cymbal) != 0)
                {
                    return MoonNoteType.Cymbal;
                }
                return MoonNoteType.Strum;
            }
        }

        public MoonNoteType type => MoonNoteType.Natural;

        public class Chord : IEnumerable<MoonNote>
        {
            private readonly MoonNote _baseMoonNote;
            public Chord(MoonNote note) : base()
            {
                _baseMoonNote = note;
            }

            public IEnumerator<MoonNote> GetEnumerator()
            {
                var note = _baseMoonNote;

                while (note.previous != null && note.previous.tick == note.tick)
                {
                    note = note.previous;
                }

                yield return note;

                while (note.next != null && note.tick == note.next.tick)
                {
                    note = note.next;
                    yield return note;
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        public bool IsOpenNote(MoonChart.GameMode gameMode)
        {
            return gameMode switch
            {
                MoonChart.GameMode.Guitar => guitarFret == GuitarFret.Open,
                MoonChart.GameMode.GHLGuitar => ghliveGuitarFret == GHLiveGuitarFret.Open,
                MoonChart.GameMode.ProGuitar => proGuitarFret == 0,
                MoonChart.GameMode.Drums => drumPad == DrumPad.Kick,
                _ => false
            };
        }

        public static int MakeProGuitarRawNote(ProGuitarString proString, int fret)
        {
            fret = Math.Clamp(fret, 0, 22);
            int rawNote = (fret << PRO_GUITAR_FRET_OFFSET) & PRO_GUITAR_FRET_MASK;
            rawNote |= ((int)proString << PRO_GUITAR_STRING_OFFSET) & PRO_GUITAR_STRING_MASK;
            return rawNote;
        }

        protected override ChartObject ChartClone() => Clone();

        public new MoonNote Clone()
        {
            return new MoonNote(tick, rawNote, length, flags);
        }

        public override string ToString()
        {
            return $"Note at tick {tick} with value {rawNote} and length {length}";
        }
    }
}
