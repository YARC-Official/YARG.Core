using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using YARG.Core.Chart.Events;

namespace YARG.Core.Chart
{
    /// <summary>
    /// An instrument track and all of its difficulties.
    /// </summary>
    public class InstrumentTrack<TNote> : ICloneable<InstrumentTrack<TNote>>
        where TNote : Note<TNote>
    {
        public Instrument Instrument { get; }

        // TODO: Smerge these...
        public List<AnimationEvent> AnimationEvents { get; }      = new();
        public AnimationTrack       Animations      { get; }      = new();

        private Dictionary<Difficulty, InstrumentDifficulty<TNote>> _difficulties { get; } = new();

        /// <summary>
        /// Whether or not this track contains any data.
        /// </summary>
        public bool IsEmpty
        {
            get
            {
                foreach (var difficulty in _difficulties.Values)
                {
                    if (!difficulty.IsEmpty)
                        return false;
                }

                return true;
            }
        }

        public InstrumentTrack(Instrument instrument)
        {
            Instrument = instrument;
        }

        public InstrumentTrack(Instrument instrument, Dictionary<Difficulty, InstrumentDifficulty<TNote>> difficulties)
            : this(instrument)
        {
            _difficulties = difficulties;
        }

        public InstrumentTrack(Instrument instrument, Dictionary<Difficulty, InstrumentDifficulty<TNote>> difficulties,
            List<AnimationEvent> animationEvents) : this(instrument, difficulties)
        {
            AnimationEvents = animationEvents;
        }

        public InstrumentTrack(Instrument instrument, Dictionary<Difficulty, InstrumentDifficulty<TNote>> difficulties,
            AnimationTrack animations) : this(instrument, difficulties)
        {
            Animations = animations;
        }

        public InstrumentTrack(InstrumentTrack<TNote> other)
            : this(other.Instrument)
        {
            foreach (var (difficulty, diffTrack) in other._difficulties)
            {
                _difficulties.Add(difficulty, diffTrack.Clone());
            }

            foreach (var animationEvent in other.AnimationEvents)
            {
                AddAnimationEvent(animationEvent.Clone());
            }
        }

        public void AddAnimationEvent(AnimationEvent animationEvent) => AnimationEvents.Add(animationEvent);

        public void AddAnimationEvent(IEnumerable<AnimationEvent> animationEvents) => AnimationEvents.AddRange(animationEvents);

        public void AddDifficulty(Difficulty difficulty, InstrumentDifficulty<TNote> track)
            => _difficulties.Add(difficulty, track);

        public void RemoveDifficulty(Difficulty difficulty)
            => _difficulties.Remove(difficulty);

        public InstrumentDifficulty<TNote> GetDifficulty(Difficulty difficulty)
            => _difficulties[difficulty];

        public bool TryGetDifficulty(Difficulty difficulty, [NotNullWhen(true)] out InstrumentDifficulty<TNote>? track)
            => _difficulties.TryGetValue(difficulty, out track);

        // For unit tests
        internal InstrumentDifficulty<TNote> FirstDifficulty()
            => _difficulties.First().Value;

        public double GetStartTime()
        {
            double totalStartTime = 0;
            foreach (var difficulty in _difficulties.Values)
            {
                totalStartTime = Math.Min(difficulty.GetStartTime(), totalStartTime);
            }

            return totalStartTime;
        }

        public double GetEndTime()
        {
            double totalEndTime = 0;
            foreach (var difficulty in _difficulties.Values)
            {
                totalEndTime = Math.Max(difficulty.GetEndTime(), totalEndTime);
            }

            return totalEndTime;
        }

        public double GetFirstNoteStartTime()
        {
            double startTime = double.MaxValue;

            foreach (var difficulty in _difficulties.Values)
            {
                startTime = Math.Min(difficulty.GetFirstNoteStartTime(), startTime);
            }

            return startTime;
        }

        public double GetLastNoteEndTime()
        {
            double endTime = 0;

            foreach (var difficulty in _difficulties.Values)
            {
                endTime = Math.Max(difficulty.GetLastNoteEndTime(), endTime);
            }

            return endTime;
        }

        public uint GetFirstTick()
        {
            uint totalFirstTick = 0;
            foreach (var difficulty in _difficulties.Values)
            {
                totalFirstTick = Math.Min(difficulty.GetFirstTick(), totalFirstTick);
            }

            return totalFirstTick;
        }

        public uint GetLastTick()
        {
            uint totalLastTick = 0;
            foreach (var difficulty in _difficulties.Values)
            {
                totalLastTick = Math.Max(difficulty.GetLastTick(), totalLastTick);
            }

            return totalLastTick;
        }

        public InstrumentTrack<TNote> Clone()
        {
            return new(this);
        }
    }
}