using System;
using System.Collections.Generic;

namespace YARG.Core.Chart
{
    /// <summary>
    /// A single difficulty of an instrument track.
    /// </summary>
    public class InstrumentDifficulty<TNote>
        where TNote : Note<TNote>
    {
        public Instrument Instrument { get; }
        public Difficulty Difficulty { get; }

        public List<TNote> Notes { get; } = new();
        public List<Phrase> Phrases { get; } = new();
        public List<TextEvent> TextEvents { get; } = new();

        public InstrumentDifficulty(Instrument instrument, Difficulty difficulty)
        {
            Instrument = instrument;
            Difficulty = difficulty;
        }

        public InstrumentDifficulty(Instrument instrument, Difficulty difficulty,
            List<TNote> notes, List<Phrase> phrases, List<TextEvent> text)
            : this(instrument, difficulty)
        {
            Notes = notes;
            Phrases = phrases;
            TextEvents = text;
        }

        public InstrumentDifficulty(Instrument instrument, Difficulty difficulty, List<ChartEvent> events)
            : this(instrument, difficulty)
        {
            foreach (var ev in events)
            {
                switch (ev)
                {
                    case TNote note:
                        Notes.Add(note);
                        break;
                    case Phrase phrase:
                        Phrases.Add(phrase);
                        break;
                    case TextEvent text:
                        TextEvents.Add(text);
                        break;
                    default:
                        YargTrace.DebugWarning($"Unrecognized event type {ev.GetType()}!");
                        continue;
                }
            }
        }

        public uint GetFirstTick()
        {
            uint totalFirstTick = 0;

            totalFirstTick = Math.Min(Notes.GetFirstTick(), totalFirstTick);
            totalFirstTick = Math.Min(Phrases.GetFirstTick(), totalFirstTick);
            totalFirstTick = Math.Min(TextEvents.GetFirstTick(), totalFirstTick);

            return totalFirstTick;
        }

        public uint GetLastTick()
        {
            uint totalLastTick = 0;

            totalLastTick = Math.Max(Notes.GetLastTick(), totalLastTick);
            totalLastTick = Math.Max(Phrases.GetLastTick(), totalLastTick);
            totalLastTick = Math.Max(TextEvents.GetLastTick(), totalLastTick);

            return totalLastTick;
        }
    }
}