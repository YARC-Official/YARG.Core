using Melanchall.DryWetMidi.Core;

namespace YARG.Core.Chart
{
    /// <summary>
    /// Interface used for loading chart files.
    /// </summary>
    internal interface ISongLoader
    {
        void LoadSong(string filePath);
        void LoadMidi(MidiFile midi);
        void LoadDotChart(string chartText);

        InstrumentTrack<GuitarNote> LoadGuitarTrack(Instrument instrument);
        InstrumentTrack<ProGuitarNote> LoadProGuitarTrack(Instrument instrument);
        InstrumentTrack<DrumNote> LoadDrumsTrack(Instrument instrument);
        VocalsTrack LoadVocalsTrack(Instrument instrument);
    }
}