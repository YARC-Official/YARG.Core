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
        public InstrumentTrack<GuitarNote> FiveFretGuitar { get; set; } = new(Instrument.FiveFretGuitar);
        public InstrumentTrack<GuitarNote> FiveFretCoop { get; set; } = new(Instrument.FiveFretCoopGuitar);
        public InstrumentTrack<GuitarNote> FiveFretRhythm { get; set; } = new(Instrument.FiveFretRhythm);
        public InstrumentTrack<GuitarNote> FiveFretBass { get; set; } = new(Instrument.FiveFretBass);
        public InstrumentTrack<GuitarNote> Keys { get; set; } = new(Instrument.Keys);

        // Not supported yet
        // public InstrumentTrack<GuitarNote> SixFretGuitar { get; set; } = new(Instrument.SixFretGuitar);
        // public InstrumentTrack<GuitarNote> SixFretCoop { get; set; } = new(Instrument.SixFretCoopGuitar);
        // public InstrumentTrack<GuitarNote> SixFretRhythm { get; set; } = new(Instrument.SixFretRhythm);
        // public InstrumentTrack<GuitarNote> SixFretBass { get; set; } = new(Instrument.SixFretBass);

        public InstrumentTrack<DrumNote> FourLaneDrums { get; set; } = new(Instrument.FourLaneDrums);
        public InstrumentTrack<DrumNote> ProDrums { get; set; } = new(Instrument.ProDrums);

        public InstrumentTrack<DrumNote> FiveLaneDrums { get; set; } = new(Instrument.FiveLaneDrums);

        // public InstrumentTrack<DrumNote> TrueDrums { get; set; } = new(Instrument.TrueDrums);

        // public InstrumentTrack<ProGuitarNote> ProGuitar_17Fret { get; set; } = new(Instrument.ProGuitar_17Fret);
        // public InstrumentTrack<ProGuitarNote> ProGuitar_22Fret { get; set; } = new(Instrument.ProGuitar_22Fret);
        // public InstrumentTrack<ProGuitarNote> ProBass_17Fret { get; set; } = new(Instrument.ProBass_17Fret);
        // public InstrumentTrack<ProGuitarNote> ProBass_22Fret { get; set; } = new(Instrument.ProBass_22Fret);

        // public InstrumentTrack<ProKeysNote> ProKeys { get; set; } = new(Instrument.ProKeys);

        public InstrumentTrack<VocalNote> Vocals { get; set; } = new(Instrument.Vocals);
        public List<InstrumentTrack<VocalNote>> Harmonies { get; set; } = new();

        // public InstrumentTrack<DjNote> Dj { get; set; } = new(Instrument.Dj);

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