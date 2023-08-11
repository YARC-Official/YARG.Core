using Melanchall.DryWetMidi.Core;

namespace YARG.Core.Extensions
{
    public static class MidiExtensions
    {
        public static void Merge(this MidiFile targetFile, MidiFile file)
        {
            foreach (var track in file.GetTrackChunks())
            {
                // Replace any existing tracks first
                bool isExisting = false;
                for (int targetIndex = 0; targetIndex < targetFile.Chunks.Count; targetIndex++)
                {
                    var chunk = targetFile.Chunks[targetIndex];
                    if (chunk is not TrackChunk existingTrack)
                        continue;

                    string newName = track.GetTrackName();
                    string existingName = existingTrack.GetTrackName();
                    if (newName != existingName)
                        continue;

                    targetFile.Chunks[targetIndex] = track;
                }

                // Otherwise, add it
                if (!isExisting)
                    targetFile.Chunks.Add(track);
            }
        }

        public static string GetTrackName(this TrackChunk track)
        {
            // This should almost always only go through one iteration, but in the case that
            // the track name isn't the first event exactly, it's probably good to have a bit of leniency
            const int MAX_SEARCH = 5;
            for (int i = 0; i < track.Events.Count && i < MAX_SEARCH; i++)
            {
                var midiEvent = track.Events[i];
                if (midiEvent.EventType == MidiEventType.SequenceTrackName &&
                    midiEvent is SequenceTrackNameEvent trackName)
                    return trackName.Text;
            }

            return "";
        }
    }
}