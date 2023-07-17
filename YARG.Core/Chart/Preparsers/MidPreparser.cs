using System;
using System.Collections.Generic;
using System.IO;
using Melanchall.DryWetMidi.Core;
using MoonscraperChartEditor.Song.IO;

namespace YARG.Core.Chart
{
    public static class MidPreparser
    {
        private static readonly ReadingSettings ReadSettings = new()
        {
            InvalidChunkSizePolicy = InvalidChunkSizePolicy.Ignore,
            NotEnoughBytesPolicy = NotEnoughBytesPolicy.Ignore,
            NoHeaderChunkPolicy = NoHeaderChunkPolicy.Ignore,
            InvalidChannelEventParameterValuePolicy = InvalidChannelEventParameterValuePolicy.ReadValid,
        };

        private static readonly Dictionary<string, Instrument> PartLookup = new()
        {
            { MidIOHelper.GUITAR_TRACK,      Instrument.FiveFretGuitar },
            { MidIOHelper.GH1_GUITAR_TRACK,  Instrument.FiveFretGuitar },
            { MidIOHelper.GUITAR_COOP_TRACK, Instrument.FiveFretCoopGuitar },
            { MidIOHelper.BASS_TRACK,        Instrument.FiveFretBass },
            { MidIOHelper.RHYTHM_TRACK,      Instrument.FiveFretRhythm },
            { MidIOHelper.KEYS_TRACK,        Instrument.Keys },

            { MidIOHelper.GHL_GUITAR_TRACK,      Instrument.SixFretGuitar },
            { MidIOHelper.GHL_GUITAR_COOP_TRACK, Instrument.SixFretCoopGuitar },
            { MidIOHelper.GHL_BASS_TRACK,        Instrument.SixFretBass },
            { MidIOHelper.GHL_RHYTHM_TRACK,      Instrument.SixFretRhythm },
    
            { MidIOHelper.DRUMS_TRACK,      Instrument.FourLaneDrums },
            { MidIOHelper.DRUMS_TRACK_2,    Instrument.FourLaneDrums },
            { MidIOHelper.DRUMS_REAL_TRACK, Instrument.FourLaneDrums },

            { MidIOHelper.PRO_GUITAR_17_FRET_TRACK, Instrument.ProGuitar_17Fret },
            { MidIOHelper.PRO_GUITAR_22_FRET_TRACK, Instrument.ProGuitar_22Fret },
            { MidIOHelper.PRO_BASS_17_FRET_TRACK,   Instrument.ProBass_17Fret },
            { MidIOHelper.PRO_BASS_22_FRET_TRACK,   Instrument.ProBass_22Fret },

            { MidIOHelper.VOCALS_TRACK,      Instrument.Vocals },
            { MidIOHelper.HARMONY_1_TRACK,   Instrument.Harmony },
            { MidIOHelper.HARMONY_2_TRACK,   Instrument.Harmony },
            { MidIOHelper.HARMONY_3_TRACK,   Instrument.Harmony },
            { MidIOHelper.HARMONY_1_TRACK_2, Instrument.Harmony },
            { MidIOHelper.HARMONY_2_TRACK_2, Instrument.Harmony },
            { MidIOHelper.HARMONY_3_TRACK_2, Instrument.Harmony },
        };

        public static bool GetAvailableTracks(byte[] chartData, out AvailableParts tracks)
        {
            try
            {
                using var stream = new MemoryStream(chartData);
                var midi = MidiFile.Read(stream, ReadSettings);
                tracks = PreparseMidi(midi);
                return true;
            }
            catch (Exception e)
            {
                YargTrace.LogException(e, "Error reading available .mid tracks!");
                tracks = new();
                return false;
            }
        }

        public static bool GetAvailableTracks(string filePath, out AvailableParts tracks)
        {
            try
            {
                var midi = MidiFile.Read(filePath, ReadSettings);
                tracks = PreparseMidi(midi);
                return true;
            }
            catch (Exception e)
            {
                YargTrace.LogException(e, "Error reading available .mid tracks!");
                tracks = new();
                return false;
            }
        }

        private static AvailableParts PreparseMidi(MidiFile midi)
        {
            var parts = new AvailableParts();

            foreach (var chunk in midi.GetTrackChunks())
            {
                foreach (var trackEvent in chunk.Events)
                {
                    if (trackEvent is not SequenceTrackNameEvent trackName)
                        continue;

                    string trackNameKey = trackName.Text.ToUpper();
                    if (!PartLookup.TryGetValue(trackNameKey, out var instrument))
                        continue;

                    parts.SetInstrumentAvailable(instrument, true);
                }
            }

            return parts;
        }
    }
}