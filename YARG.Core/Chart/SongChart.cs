using System;
using System.Collections.Generic;
using System.Linq;
using Melanchall.DryWetMidi.Core;

namespace YARG.Core.Chart
{
    /// <summary>
    /// The chart data for a song.
    /// </summary>
    public class SongChart
    {
        public uint Resolution => SyncTrack.Resolution;

        public SongMetadata Metadata { get; set; }

        public List<TextEvent> GlobalEvents { get; set; } = new();
        public SyncTrack SyncTrack { get; set; } = new();

        public InstrumentTrack<GuitarNote> FiveFretGuitar { get; set; } = new(Instrument.FiveFretGuitar);
        public InstrumentTrack<GuitarNote> FiveFretCoop { get; set; } = new(Instrument.FiveFretCoopGuitar);
        public InstrumentTrack<GuitarNote> FiveFretRhythm { get; set; } = new(Instrument.FiveFretRhythm);
        public InstrumentTrack<GuitarNote> FiveFretBass { get; set; } = new(Instrument.FiveFretBass);
        public InstrumentTrack<GuitarNote> Keys { get; set; } = new(Instrument.Keys);

        public IEnumerable<InstrumentTrack<GuitarNote>> FiveFretTracks
        {
            get
            {
                yield return FiveFretGuitar;
                yield return FiveFretCoop;
                yield return FiveFretRhythm;
                yield return FiveFretBass;
                yield return Keys;
            }
        }

        // Not supported yet
        public InstrumentTrack<GuitarNote> SixFretGuitar { get; set; } = new(Instrument.SixFretGuitar);
        public InstrumentTrack<GuitarNote> SixFretCoop { get; set; } = new(Instrument.SixFretCoopGuitar);
        public InstrumentTrack<GuitarNote> SixFretRhythm { get; set; } = new(Instrument.SixFretRhythm);
        public InstrumentTrack<GuitarNote> SixFretBass { get; set; } = new(Instrument.SixFretBass);

        public IEnumerable<InstrumentTrack<GuitarNote>> SixFretTracks
        {
            get
            {
                yield return SixFretGuitar;
                yield return SixFretCoop;
                yield return SixFretRhythm;
                yield return SixFretBass;
            }
        }

        public InstrumentTrack<DrumNote> FourLaneDrums { get; set; } = new(Instrument.FourLaneDrums);
        public InstrumentTrack<DrumNote> ProDrums { get; set; } = new(Instrument.ProDrums);
        public InstrumentTrack<DrumNote> FiveLaneDrums { get; set; } = new(Instrument.FiveLaneDrums);

        // public InstrumentTrack<DrumNote> TrueDrums { get; set; } = new(Instrument.TrueDrums);

        public IEnumerable<InstrumentTrack<DrumNote>> DrumsTracks
        {
            get
            {
                yield return FourLaneDrums;
                yield return ProDrums;
                yield return FiveLaneDrums;
            }
        }

        public InstrumentTrack<ProGuitarNote> ProGuitar_17Fret { get; set; } = new(Instrument.ProGuitar_17Fret);
        public InstrumentTrack<ProGuitarNote> ProGuitar_22Fret { get; set; } = new(Instrument.ProGuitar_22Fret);
        public InstrumentTrack<ProGuitarNote> ProBass_17Fret { get; set; } = new(Instrument.ProBass_17Fret);
        public InstrumentTrack<ProGuitarNote> ProBass_22Fret { get; set; } = new(Instrument.ProBass_22Fret);

        public IEnumerable<InstrumentTrack<ProGuitarNote>> ProGuitarTracks
        {
            get
            {
                yield return ProGuitar_17Fret;
                yield return ProGuitar_22Fret;
                yield return ProBass_17Fret;
                yield return ProBass_22Fret;
            }
        }

        // public InstrumentTrack<ProKeysNote> ProKeys { get; set; } = new(Instrument.ProKeys);

        public VocalsTrack Vocals { get; set; } = new(Instrument.Vocals);
        public VocalsTrack Harmony { get; set; } = new(Instrument.Harmony);

        public IEnumerable<VocalsTrack> VocalsTracks
        {
            get
            {
                yield return Vocals;
                yield return Harmony;
            }
        }

        // public InstrumentTrack<DjNote> Dj { get; set; } = new(Instrument.Dj);

        // To explicitly allow creation without going through a file
        public SongChart() { }

        internal SongChart(SongMetadata metadata, ISongLoader loader)
        {
            loader.CompleteMetadata(metadata);
            Metadata = metadata;

            GlobalEvents = loader.LoadGlobalEvents();
            SyncTrack = loader.LoadSyncTrack();

            FiveFretGuitar = loader.LoadGuitarTrack(Instrument.FiveFretGuitar);
            FiveFretCoop = loader.LoadGuitarTrack(Instrument.FiveFretCoopGuitar);
            FiveFretRhythm = loader.LoadGuitarTrack(Instrument.FiveFretRhythm);
            FiveFretBass = loader.LoadGuitarTrack(Instrument.FiveFretBass);
            Keys = loader.LoadGuitarTrack(Instrument.Keys);

            SixFretGuitar = loader.LoadGuitarTrack(Instrument.SixFretGuitar);
            SixFretCoop = loader.LoadGuitarTrack(Instrument.SixFretCoopGuitar);
            SixFretRhythm = loader.LoadGuitarTrack(Instrument.SixFretRhythm);
            SixFretBass = loader.LoadGuitarTrack(Instrument.SixFretBass);

            FourLaneDrums = loader.LoadDrumsTrack(Instrument.FourLaneDrums);
            ProDrums = loader.LoadDrumsTrack(Instrument.ProDrums);
            FiveLaneDrums = loader.LoadDrumsTrack(Instrument.FiveLaneDrums);

            // TrueDrums = loader.LoadDrumsTrack(Instrument.TrueDrums);

            ProGuitar_17Fret = loader.LoadProGuitarTrack(Instrument.ProGuitar_17Fret);
            ProGuitar_22Fret = loader.LoadProGuitarTrack(Instrument.ProGuitar_22Fret);
            ProBass_17Fret = loader.LoadProGuitarTrack(Instrument.ProBass_17Fret);
            ProBass_22Fret = loader.LoadProGuitarTrack(Instrument.ProBass_22Fret);

            // ProKeys = loader.LoadProKeysTrack(Instrument.ProKeys);

            Vocals = loader.LoadVocalsTrack(Instrument.Vocals);
            Harmony = loader.LoadVocalsTrack(Instrument.Harmony);

            // Dj = loader.LoadDjTrack(Instrument.Dj);
        }

        public static SongChart FromFile(SongMetadata metadata, string filePath)
        {
            ISongLoader loader = new MoonSongLoader();
            loader.LoadSong(metadata.ParseSettings, filePath);
            return new SongChart(metadata, loader);
        }

        public static SongChart FromMidi(SongMetadata metadata, MidiFile midi)
        {
            ISongLoader loader = new MoonSongLoader();
            loader.LoadMidi(metadata.ParseSettings, midi);
            return new SongChart(metadata, loader);
        }

        public static SongChart FromDotChart(SongMetadata metadata, string chartText)
        {
            ISongLoader loader = new MoonSongLoader();
            loader.LoadDotChart(metadata.ParseSettings, chartText);
            return new SongChart(metadata, loader);
        }

        public double TickToTime(uint tick)
        {
            return SyncTrack.TickToTime(tick);
        }

        public uint TimeToTick(double time)
        {
            return SyncTrack.TimeToTick(time);
        }

        public double TickToTime(uint tick, TempoChange currentTempo)
        {
            return SyncTrack.TickToTime(tick, currentTempo);
        }

        public uint TimeToTick(double time, TempoChange currentTempo)
        {
            return SyncTrack.TimeToTick(time, currentTempo);
        }

        public double TickRangeToTimeDelta(uint tickStart, uint tickEnd, float currentBpm)
        {
            return SyncTrack.TickRangeToTimeDelta(tickStart, tickEnd, currentBpm);
        }

        public uint TimeRangeToTickDelta(double timeStart, double timeEnd, float currentBpm)
        {
            return SyncTrack.TimeRangeToTickDelta(timeStart, timeEnd, currentBpm);
        }

        public static double TickRangeToTimeDelta(uint tickStart, uint tickEnd, uint resolution, float currentBpm)
        {
            return SyncTrack.TickRangeToTimeDelta(tickStart, tickEnd, resolution, currentBpm);
        }

        public static uint TimeRangeToTickDelta(double timeStart, double timeEnd, uint resolution, float currentBpm)
        {
            return SyncTrack.TimeRangeToTickDelta(timeStart, timeEnd, resolution, currentBpm);
        }

        public uint GetFirstTick()
        {
            static uint TrackMin<TNote>(IEnumerable<InstrumentTrack<TNote>> tracks) where TNote : Note<TNote>
                => tracks.Min((track) => track.GetFirstTick());
            static uint VoxMin(IEnumerable<VocalsTrack> tracks)
                => tracks.Min((track) => track.GetFirstTick());

            uint totalFirstTick = 0;

            // Tracks
            totalFirstTick = Math.Min(TrackMin(FiveFretTracks), totalFirstTick);
            totalFirstTick = Math.Min(TrackMin(SixFretTracks), totalFirstTick);
            totalFirstTick = Math.Min(TrackMin(DrumsTracks), totalFirstTick);
            totalFirstTick = Math.Min(TrackMin(ProGuitarTracks), totalFirstTick);
            totalFirstTick = Math.Min(VoxMin(VocalsTracks), totalFirstTick);

            // Global
            totalFirstTick = Math.Min(GlobalEvents.GetFirstTick(), totalFirstTick);

            return totalFirstTick;
        }

        public uint GetLastTick()
        {
            static uint TrackMax<TNote>(IEnumerable<InstrumentTrack<TNote>> tracks) where TNote : Note<TNote>
                => tracks.Max((track) => track.GetLastTick());
            static uint VoxMax(IEnumerable<VocalsTrack> tracks)
                => tracks.Max((track) => track.GetLastTick());

            uint totalLastTick = 0;

            // Tracks
            totalLastTick = Math.Max(TrackMax(FiveFretTracks), totalLastTick);
            totalLastTick = Math.Max(TrackMax(SixFretTracks), totalLastTick);
            totalLastTick = Math.Max(TrackMax(DrumsTracks), totalLastTick);
            totalLastTick = Math.Max(TrackMax(ProGuitarTracks), totalLastTick);
            totalLastTick = Math.Max(VoxMax(VocalsTracks), totalLastTick);

            // Global
            totalLastTick = Math.Max(GlobalEvents.GetLastTick(), totalLastTick);

            return totalLastTick;
        }
    }
}