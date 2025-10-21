using System;
using System.Collections.Generic;
using System.Text;
using YARG.Core.Logging;

namespace YARG.Core.IO
{
    public class MiloAnimation : IDisposable
    {
        private const    string           MILO_ANIMATION_FILE = "song.anim";
        private readonly FixedArray<byte> _data;
        private          bool             _disposed;

        public MiloAnimation(FixedArray<byte> miloFile)
        {
            _data = YARGMiloReader.GetMiloFile(miloFile, MILO_ANIMATION_FILE);
        }

        // Thanks to AddyMills' RB-Tools parser for the general inspiration and file format
        public List<MiloAnimationEvent> GetMiloAnimation()
        {
            var events = new List<MiloAnimationEvent>();

            // The milo file was not found
            if (_data.Length == 0)
            {
                return events;
            }

            var identifiersByFirstByte = new Dictionary<byte, List<MiloAnimationParams>>();
            foreach (var track in MiloAnimationTracks)
            {
                var firstByte = track.Identifier[0];
                if (!identifiersByFirstByte.TryGetValue(firstByte, out var list))
                {
                    list = new List<MiloAnimationParams>();
                    identifiersByFirstByte[firstByte] = list;
                }

                list.Add(track);
            }

            // Create a span of the unique first bytes to search for.
            var keyCount = identifiersByFirstByte.Count;
            var firstBytes = keyCount <= 256 ? stackalloc byte[keyCount] : new byte[keyCount];
            var k = 0;
            foreach (var key in identifiersByFirstByte.Keys)
            {
                firstBytes[k++] = key;
            }

            var position = 0;
            while (position < _data.Length)
            {
                // Find the next candidate identifier in the file
                var searchSlice = _data[position..];
                var nextMatchIndex = searchSlice.IndexOfAny(firstBytes);

                // If no potential matches are left in the file, we're done.
                if (nextMatchIndex == -1)
                {
                    break;
                }

                // The absolute position of the potential identifier in the original data.
                var absoluteIndex = position + nextMatchIndex;
                MiloAnimationParams? foundTrack = null;

                // We found a matching first byte. Check if any full identifiers match at this position.
                var possibleSlice = _data[absoluteIndex..];
                foreach (var track in identifiersByFirstByte[_data[absoluteIndex]])
                {
                    if (possibleSlice.StartsWith(track.Identifier))
                    {
                        foundTrack = track;
                        break; // Matched a track, so go process that track
                    }
                }

                // False alarm
                if (foundTrack is null)
                {
                    position = absoluteIndex + 1;
                    continue;
                }

                // Advance our main position to start the *next* search right after this found track.
                position = absoluteIndex + foundTrack.Value.Identifier.Length;

                // Create a slice for the found track
                var trackStart = absoluteIndex + foundTrack.Value.Identifier.Length + foundTrack.Value.Offset;
                var trackData = _data[trackStart..];
                var trackPosition = 0;

                try
                {
                    var eventCount = ReadUInt32BE(trackData.Slice(trackPosition));
                    trackPosition += 4;

                    var previousEventName = string.Empty;

                    // Read all of the events in this track
                    for (var i = 0; i < eventCount; i++)
                    {
                        if (foundTrack.Value.Type == MiloAnimationType.PostProcessing)
                        {
                            // Skip 4 unknown bytes for postproc
                            trackPosition += 4;
                        }

                        var nameLength = ReadUInt32BE(trackData.Slice(trackPosition));
                        trackPosition += 4;
                        var eventNameBytes = trackData.Slice(trackPosition, (int) nameLength);
                        trackPosition += (int) nameLength;

                        string eventName;

                        // If the first event has a zero-length name, skip it and the next 4 bytes (weird format)
                        if (i == 0 && eventNameBytes.Length == 0)
                        {
                            trackPosition += 4; // Discard the 4-byte value.
                            continue;           // Skip to the next iteration.
                        }

                        if (eventNameBytes.Length > 0)
                        {
                            eventName = Encoding.UTF8.GetString(eventNameBytes);
                        }
                        else
                        {
                            // Later events without a name reuse the previous name (did I mention that the format is weird)
                            eventName = previousEventName;
                        }

                        previousEventName = eventName;

                        var timeFrames = ReadSingleBE(trackData[trackPosition..]);
                        trackPosition += 4;
                        var timeSeconds = timeFrames / 30.0;
                        timeSeconds = Math.Max(0.0, timeSeconds);

                        events.Add(new MiloAnimationEvent(foundTrack.Value.Type, eventName, timeSeconds));
                    }
                }
                catch (ArgumentOutOfRangeException)
                {
                    YargLogger.LogError("Unexpected end of milo animation data");
                }
            }

            return events;
        }

        private static byte[] CreateIdentifier(byte prefix, string text)
        {
            var textBytes = Encoding.ASCII.GetBytes(text);
            var identifier = new byte[1 + textBytes.Length];
            identifier[0] = prefix;
            Array.Copy(textBytes, 0, identifier, 1, textBytes.Length);
            return identifier;
        }

        // ReSharper disable once InconsistentNaming
        private static uint ReadUInt32BE(ReadOnlySpan<byte> data)
        {
            if (data.Length < 4)
                throw new ArgumentOutOfRangeException(nameof(data), "Span is too short to read a UInt32.");

            return (uint)((data[0] << 24) | (data[1] << 16) | (data[2] << 8) | data[3]);
        }

        // ReSharper disable once InconsistentNaming
        private static float ReadSingleBE(ReadOnlySpan<byte> data)
        {
            if (data.Length < 4)
            {
                throw new ArgumentOutOfRangeException(nameof(data), "Span is too short to read a Single.");
            }

            var value = ReadUInt32BE(data);
            unsafe
            {
                // This is a common and efficient way to reinterpret the bits of a uint as a float.
                return *(float*)&value;
            }
        }


        public struct MiloAnimationEvent
        {
            public readonly MiloAnimationType Type;
            public readonly string            Name;
            public readonly double            Time;

            public MiloAnimationEvent(MiloAnimationType type, string name, double time)
            {
                Type = type;
                Name = name;
                Time = time;
            }
        }

        public struct MiloAnimationParams
        {
            public readonly MiloAnimationType Type;
            public readonly byte[]            Identifier;
            public readonly int               Offset;

            public MiloAnimationParams(MiloAnimationType type, string identifier, int offset)
            {
                Type = type;
                Identifier = Encoding.ASCII.GetBytes(identifier);
                Offset = offset;
            }

            public MiloAnimationParams(MiloAnimationType type, byte[] identifier, int offset)
            {
                Type = type;
                Identifier = identifier;
                Offset = offset;
            }
        }

        // We're reading all of this, but only keeping the stuff that would be in the venue track in older versions
        private static readonly List<MiloAnimationParams> MiloAnimationTracks = new List<MiloAnimationParams>
        {
            new(MiloAnimationType.BassIntensity, "bass_intensity", 13),
            new(MiloAnimationType.GuitarIntensity, "guitar_intensity", 13),
            new(MiloAnimationType.DrumIntensity, "drum_intensity", 13),
            new(MiloAnimationType.MicIntensity, "mic_intensity", 13),
            new(MiloAnimationType.KeyboardIntensity, "keyboard_intensity", 13),
            new(MiloAnimationType.ShotBassGuitar, "shot_bg", 13),
            new(MiloAnimationType.ShotBassKeys, "shot_bk", 13),
            new(MiloAnimationType.ShotGuitarKeys, "shot_gk", 13),
            new(MiloAnimationType.Shot5, "shot_5", 13),
            // Why some of these have a prefix, I do not know, but that's how the python parser did it
            new(MiloAnimationType.Crowd, CreateIdentifier(0x05, "crowd"), 13),
            new(MiloAnimationType.PostProcessing, "postproc_interp", 5),
            new(MiloAnimationType.Fog, "stagekit_fog", 13),
            new(MiloAnimationType.Lights, "lightpreset_interp", 5),
            new(MiloAnimationType.Keyframe, "lightpreset_keyframe_interp", 5),
            new(MiloAnimationType.SpotGuitar, "spot_guitar", 13),
            new(MiloAnimationType.SpotBass, "spot_bass", 13),
            new(MiloAnimationType.SpotDrums, "spot_drums", 13),
            new(MiloAnimationType.SpotVocal, "spot_vocal", 13),
            new(MiloAnimationType.SpotKeyboard, "spot_keyboard", 13),
            new(MiloAnimationType.Part2Sing, "part2_sing", 13),
            new(MiloAnimationType.Part3Sing, "part3_sing", 13),
            new(MiloAnimationType.Part4Sing, "part4_sing", 13),
            new(MiloAnimationType.WorldEvent, "world_event", 13),

            // TBRB stuff we don't actually support (yet....)
            new(MiloAnimationType.Shot, CreateIdentifier(0x04, "shot"), 13),
            new(MiloAnimationType.DreamOutfit, "dream_outfit", 13),
            new(MiloAnimationType.SceneTrigger, "scenetrigger", 13),
            new(MiloAnimationType.BodyPaul, "body_paul", 13),
            new(MiloAnimationType.BodyJohn, "body_john", 13),
            new(MiloAnimationType.BodyRingo, "body_ringo", 13),
            new(MiloAnimationType.BodyGeorge, "body_george", 13),
        };

        public enum MiloAnimationType
        {
            // ReSharper disable InconsistentNaming
            BassIntensity,
            GuitarIntensity,
            DrumIntensity,
            MicIntensity,
            KeyboardIntensity,
            ShotBassGuitar,
            ShotBassKeys,
            ShotGuitarKeys,
            Shot5,
            Crowd,
            PostProcessing,
            Fog,
            Lights,
            Keyframe,
            SpotGuitar,
            SpotBass,
            SpotDrums,
            SpotVocal,
            SpotKeyboard,
            Part2Sing,
            Part3Sing,
            Part4Sing,
            WorldEvent,
            // Below here are TBRB-specific that we aren't actually supporting yet
            Shot,
            DreamOutfit,
            SceneTrigger,
            BodyPaul,
            BodyJohn,
            BodyRingo,
            BodyGeorge,
            // ReSharper restore InconsistentNaming
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // In case we need it later
                }

                // This is treated as if it is unmanaged since it is wrapping unmanaged memory
                _data.Dispose();

                _disposed = true;
            }
        }

        ~MiloAnimation()
        {
            Dispose(disposing: false);
        }
    }
}