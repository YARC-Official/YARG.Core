using System;
using System.Collections.Generic;
using System.IO;
using Melanchall.DryWetMidi.Core;
using MoonscraperChartEditor.Song;
using MoonscraperChartEditor.Song.IO;

namespace YARG.Core.Chart
{
    /// <summary>
    /// The chart data for a song.
    /// </summary>
    public class SongChart 
    {
        public InstrumentTrack<GuitarNote> FiveFretGuitar { get; }
        public InstrumentTrack<GuitarNote> FiveFretCoop { get; }
        public InstrumentTrack<GuitarNote> FiveFretRhythm { get; }
        public InstrumentTrack<GuitarNote> FiveFretBass { get; }
        public InstrumentTrack<GuitarNote> Keys { get; }

        // Not supported yet
        // public InstrumentTrack<GuitarNote> SixFretGuitar { get; }
        // public InstrumentTrack<GuitarNote> SixFretCoop { get; }
        // public InstrumentTrack<GuitarNote> SixFretRhythm { get; }
        // public InstrumentTrack<GuitarNote> SixFretBass { get; }

        public InstrumentTrack<DrumNote> FourLaneDrums { get; }
        public InstrumentTrack<DrumNote> ProDrums { get; }

        public InstrumentTrack<DrumNote> FiveLaneDrums { get; }

        // public InstrumentTrack<DrumNote> TrueDrums { get; }

        // public InstrumentTrack<ProGuitarNote> ProGuitar_17Fret { get; }
        // public InstrumentTrack<ProGuitarNote> ProGuitar_22Fret { get; }
        // public InstrumentTrack<ProGuitarNote> ProBass_17Fret { get; }
        // public InstrumentTrack<ProGuitarNote> ProBass_22Fret { get; }

        // public InstrumentTrack<ProKeysNote> ProKeys { get; }

        public InstrumentTrack<VocalNote> Vocals { get; }
        public List<InstrumentTrack<VocalNote>> Harmonies { get; }

        // public InstrumentTrack<DjNote> Dj { get; }

        public static SongChart FromFile(string filePath)
        {
            return Path.GetExtension(filePath).ToLower() switch
            {
                ".mid" => FromMidiPath(filePath),
                ".chart" => FromDotChartPath(filePath),
                _ => throw new ArgumentException($"Unrecognized file extension for chart path '{filePath}'!", nameof(filePath))
            };
        }

        public static SongChart FromMidiPath(string filePath)
        {
            var moonSong = MidReader.ReadMidi(filePath);
            return FromMoonSong(moonSong);
        }

        public static SongChart FromMidi(MidiFile midi)
        {
            var moonSong = MidReader.ReadMidi(midi);
            return FromMoonSong(moonSong);
        }

        public static SongChart FromDotChartPath(string filePath)
        {
            var moonSong = ChartReader.ReadChart(filePath);
            return FromMoonSong(moonSong);
        }

        public static SongChart FromDotChartText(string fileText)
        {
            var moonSong = ChartReader.ReadChart(new StringReader(fileText));
            return FromMoonSong(moonSong);
        }

        public static SongChart FromMoonSong(MoonSong moonSong)
        {
            // TODO
            return new SongChart();
        }
    }
}