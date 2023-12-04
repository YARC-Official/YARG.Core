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
            // The first event is not always the track name,
            // so we need to search through everything at tick 0
            for (int i = 0; i < track.Events.Count; i++)
            {
                var midiEvent = track.Events[i];

                // Search until the first event that's not at position 0,
                // indicated by the first non-zero delta-time
                if (midiEvent.DeltaTime != 0)
                    break;

                if (midiEvent.EventType == MidiEventType.SequenceTrackName &&
                    midiEvent is SequenceTrackNameEvent trackName)
                    return trackName.Text;
            }

            return "";
        }
    }
}