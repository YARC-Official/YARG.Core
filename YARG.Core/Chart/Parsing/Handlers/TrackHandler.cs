using System;
using System.Collections.Generic;

namespace YARG.Core.Chart.Parsing
{
    internal static class TrackHandler
    {
        public static void AddNote<TNote>(List<TNote> notes, TNote newNote, in ParseSettings settings)
            where TNote : Note<TNote>
        {
            // The parent of all notes on the current tick
            var currentParent = notes.Count > 0 ? notes[^1] : null;
            // Previous parent note (on a different tick)
            var previousParent = notes.Count > 1 ? notes[^2] : null;

            // Determine if this is part of a chord
            if (currentParent != null)
            {
                if (newNote.Tick == currentParent.Tick)
                {
                    // Same chord, assign previous and add as child
                    newNote.PreviousNote = previousParent;
                    currentParent.AddChildNote(newNote);
                    return;
                }
                else if ((newNote.Tick - currentParent.Tick) <= settings.NoteSnapThreshold)
                {
                    // Chord needs to be snapped, copy values
                    newNote.CopyValuesFrom(currentParent);

                    newNote.PreviousNote = previousParent;
                    currentParent.AddChildNote(newNote);
                    return;
                }
            }

            // New chord
            previousParent = currentParent;
            currentParent = newNote;

            // Assign next/previous note references
            if (previousParent is not null)
            {
                previousParent.NextNote = currentParent;
                foreach (var child in previousParent.ChildNotes)
                    child.NextNote = currentParent;

                currentParent.PreviousNote = previousParent;
            }

            notes.Add(newNote);
        }

        public static void UpdatePhrases(Dictionary<PhraseType, Phrase> currentPhrases, List<Phrase> phrases,
            ref int phraseIndex, uint currentTick)
        {
            while (phraseIndex < phrases.Count)
            {
                var phrase = phrases[phraseIndex];
                if (phrase.Tick > currentTick)
                    break;

                currentPhrases[phrase.Type] = phrase;
                phraseIndex++;
            }
        }

        public static NoteFlags GetGeneralFlags<TEvent>(List<TEvent> events, int index,
            Dictionary<PhraseType, Phrase> currentPhrases)
            where TEvent : IntermediateEvent
        {
            var flags = NoteFlags.None;

            var current = events[index];
            var next = GetNextSeparateEvent(events, index);
            var previous = GetPreviousSeparateEvent(events, index);

            // Star power
            if (currentPhrases.TryGetValue(PhraseType.StarPower, out var starPower) && IsInPhrase(current, starPower))
            {
                flags |= NoteFlags.StarPower;

                if (previous == null || !IsInPhrase(previous, starPower))
                    flags |= NoteFlags.StarPowerStart;

                if (next == null || !IsInPhrase(next, starPower))
                    flags |= NoteFlags.StarPowerEnd;
            }

            // Solos
            if (currentPhrases.TryGetValue(PhraseType.Solo, out var solo) && IsInPhrase(current, solo))
            {
                if (previous == null || !IsInPhrase(previous, solo))
                    flags |= NoteFlags.SoloStart;

                if (next == null || !IsInPhrase(next, solo))
                    flags |= NoteFlags.SoloEnd;
            }

            return flags;
        }

        public static TEvent? GetNextSeparateEvent<TEvent>(List<TEvent> events, int index)
            where TEvent : IntermediateEvent
        {
            var current = events[index];

            int next = index;
            while (next + 1 < events.Count && events[next + 1].Tick == current.Tick)
                next++;

            return next >= events.Count ? null : events[next];
        }

        public static TEvent? GetPreviousSeparateEvent<TEvent>(List<TEvent> events, int index)
            where TEvent : IntermediateEvent
        {
            var current = events[index];

            int previous = index;
            while (previous - 1 >= 0 && events[previous - 1].Tick == current.Tick)
                previous--;

            return previous < 0 ? null : events[previous];
        }

        public static (int start, int end) GetEventChord<TEvent>(List<TEvent> events, int index)
            where TEvent : IntermediateEvent
        {
            var current = events[index];

            int start = index;
            while (start - 1 > 0 && events[start - 1].Tick == current.Tick)
                start--;

            int end = index;
            while (end + 1 < events.Count && events[end + 1].Tick == current.Tick)
                end++;

            return (start, end);
        }

        public static bool IsInPhrase<TEvent>(TEvent ev, Phrase phrase)
            where TEvent : IntermediateEvent
        {
            // Ensure 0-length phrases still take effect
            // (e.g. the SP phrases at the end of ExileLord - Hellidox)
            if (phrase.TickLength == 0)
                return ev.Tick == phrase.Tick;

            return ev.Tick >= phrase.Tick && ev.Tick < (phrase.Tick + phrase.TickLength);
        }

        public static bool IsClosestToEnd<TEvent>(SongChart chart, List<TEvent> events, int index, Phrase phrase)
            where TEvent : IntermediateEvent
        {
            var current = events[index];
            int endTick = (int) (phrase.Tick + phrase.TickLength);

            // Find the event to compare against
            TEvent other;
            {
                var next = GetNextSeparateEvent(events, index);
                var previous = GetPreviousSeparateEvent(events, index);

                if (IsInPhrase(current, phrase))
                {
                    // Event is in the phrase, check if this is the last in the phrase
                    if (next is not null && !IsInPhrase(next, phrase))
                    {
                        // The phrase ends between the given event and the next
                        other = next;
                    }
                    else
                    {
                        // This is either the last event in the chart, or not the last event of the phrase
                        return next is null;
                    }
                }
                else
                {
                    // Event is not in the phrase, check if the previous is the last in the phrase
                    if (previous is null)
                    {
                        // This is the first event in the chart, check by distance
                        float tickThreshold = chart.Resolution / 3; // 1/12th note
                        return Math.Abs((int) current.Tick - endTick) < tickThreshold;
                    }
                    else if (current.Tick >= endTick && previous.Tick < endTick)
                    {
                        // The phrase ends between the previous event and the given one
                        // IsInPhrase() is not used here since cases such as drum activations at the end of breaks
                        // can possibly make it so that neither the previous nor given event are in the phrase
                        other = previous;
                    }
                    else
                    {
                        // The phrase is not applicable to the given event
                        return false;
                    }
                }
            }

            // Compare the distance of each event
            // If the distances are equal, the previous event wins
            int currentDistance = Math.Abs((int) (current.Tick - endTick));
            int otherDistance = Math.Abs((int) (other.Tick - endTick));
            return currentDistance < otherDistance || (currentDistance == otherDistance && current.Tick < other.Tick);
        }
    }
}