using System.Collections.Generic;
using Melanchall.DryWetMidi.Core;

namespace YARG.Core.Chart
{
    /// <summary>
    /// Interface used for loading chart files.
    /// </summary>
    internal interface ISongLoader
    {
        void LoadSong(ParseSettings settings, string filePath);
        void LoadMidi(ParseSettings settings, MidiFile midi);
        void LoadDotChart(ParseSettings settings, string chartText);

        List<TextEvent> LoadGlobalEvents();
        SyncTrack LoadSyncTrack();
        VenueTrack LoadVenueTrack();

        InstrumentTrack<GuitarNote> LoadGuitarTrack(Instrument instrument);
        InstrumentTrack<ProGuitarNote> LoadProGuitarTrack(Instrument instrument);
        InstrumentTrack<DrumNote> LoadDrumsTrack(Instrument instrument);
        VocalsTrack LoadVocalsTrack(Instrument instrument);
    }
}