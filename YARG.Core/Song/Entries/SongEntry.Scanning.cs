using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YARG.Core.Chart;
using YARG.Core.IO;
using YARG.Core.IO.Disposables;
using YARG.Core.Song.Preparsers;

namespace YARG.Core.Song
{
    public abstract partial class SongEntry
    {
        protected static bool ParseMidi(FixedArray<byte> file, DrumPreparseHandler drums, ref AvailableParts parts)
        {
            var midiFile = new YARGMidiFile(file.ToStream());
            foreach (var track in midiFile)
            {
                if (midiFile.TrackNumber == 1)
                    continue;

                var trackname = track.FindTrackName(Encoding.ASCII);
                if (trackname == null)
                    return false;

                if (!YARGMidiTrack.TRACKNAMES.TryGetValue(trackname, out var type))
                    continue;

                switch (type)
                {
                    case MidiTrackType.Guitar_5: if (!parts.FiveFretGuitar.WasParsed())     parts.FiveFretGuitar.Difficulties      = Midi_FiveFret_Preparser.Parse(track); break;
                    case MidiTrackType.Bass_5:   if (!parts.FiveFretBass.WasParsed())       parts.FiveFretBass.Difficulties        = Midi_FiveFret_Preparser.Parse(track); break;
                    case MidiTrackType.Rhythm_5: if (!parts.FiveFretRhythm.WasParsed())     parts.FiveFretRhythm.Difficulties      = Midi_FiveFret_Preparser.Parse(track); break;
                    case MidiTrackType.Coop_5:   if (!parts.FiveFretCoopGuitar.WasParsed()) parts.FiveFretCoopGuitar.Difficulties  = Midi_FiveFret_Preparser.Parse(track); break;
                    case MidiTrackType.Keys:     if (!parts.Keys.WasParsed()) parts.Keys.Difficulties                = Midi_FiveFret_Preparser.Parse(track); break;

                    case MidiTrackType.Guitar_6: if (!parts.SixFretGuitar.WasParsed())      parts.SixFretGuitar.Difficulties       = Midi_SixFret_Preparser.Parse(track); break;
                    case MidiTrackType.Bass_6:   if (!parts.SixFretBass.WasParsed())        parts.SixFretBass.Difficulties         = Midi_SixFret_Preparser.Parse(track); break;
                    case MidiTrackType.Rhythm_6: if (!parts.SixFretRhythm.WasParsed())      parts.SixFretRhythm.Difficulties       = Midi_SixFret_Preparser.Parse(track); break;
                    case MidiTrackType.Coop_6:   if (!parts.SixFretCoopGuitar.WasParsed()) parts.SixFretCoopGuitar.Difficulties   = Midi_SixFret_Preparser.Parse(track); break;

                    case MidiTrackType.Drums: drums.ParseMidi(track); break;

                    case MidiTrackType.Pro_Guitar_17: if (!parts.ProGuitar_17Fret.WasParsed())   parts.ProGuitar_17Fret.Difficulties = Midi_ProGuitar_Preparser.Parse_17Fret(track); break;
                    case MidiTrackType.Pro_Guitar_22: if (!parts.ProGuitar_22Fret.WasParsed())   parts.ProGuitar_22Fret.Difficulties = Midi_ProGuitar_Preparser.Parse_22Fret(track); break;
                    case MidiTrackType.Pro_Bass_17:   if (!parts.ProBass_17Fret.WasParsed())     parts.ProBass_17Fret.Difficulties   = Midi_ProGuitar_Preparser.Parse_17Fret(track); break;
                    case MidiTrackType.Pro_Bass_22:   if (!parts.ProBass_22Fret.WasParsed()) parts.ProBass_22Fret.Difficulties   = Midi_ProGuitar_Preparser.Parse_22Fret(track); break;

                    case MidiTrackType.Pro_Keys_E: if (!parts.ProKeys[Difficulty.Easy]   && Midi_ProKeys_Preparser.Parse(track)) parts.ProKeys.SetDifficulty(Difficulty.Easy); break;
                    case MidiTrackType.Pro_Keys_M: if (!parts.ProKeys[Difficulty.Medium] && Midi_ProKeys_Preparser.Parse(track)) parts.ProKeys.SetDifficulty(Difficulty.Medium); break;
                    case MidiTrackType.Pro_Keys_H: if (!parts.ProKeys[Difficulty.Hard]   && Midi_ProKeys_Preparser.Parse(track)) parts.ProKeys.SetDifficulty(Difficulty.Hard); break;
                    case MidiTrackType.Pro_Keys_X: if (!parts.ProKeys[Difficulty.Expert] && Midi_ProKeys_Preparser.Parse(track)) parts.ProKeys.SetDifficulty(Difficulty.Expert); break;

                    case MidiTrackType.Vocals: if (!parts.LeadVocals[0]    && Midi_Vocal_Preparser.ParseLeadTrack(track))    parts.LeadVocals.SetSubtrack(0); break;
                    case MidiTrackType.Harm1:  if (!parts.HarmonyVocals[0] && Midi_Vocal_Preparser.ParseLeadTrack(track))    parts.HarmonyVocals.SetSubtrack(0); break;
                    case MidiTrackType.Harm2:  if (!parts.HarmonyVocals[1] && Midi_Vocal_Preparser.ParseHarmonyTrack(track)) parts.HarmonyVocals.SetSubtrack(1); break;
                    case MidiTrackType.Harm3:  if (!parts.HarmonyVocals[2] && Midi_Vocal_Preparser.ParseHarmonyTrack(track)) parts.HarmonyVocals.SetSubtrack(2); break;
                }
            }
            return true;
        }

        protected static void ParseChart<TChar, TDecoder, TBase>(YARGChartFileReader<TChar, TDecoder, TBase> reader, DrumPreparseHandler drums, ref AvailableParts parts)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
            where TDecoder : IStringDecoder<TChar>, new()
            where TBase : unmanaged, IDotChartBases<TChar>
        {
            while (reader.IsStartOfTrack())
            {
                if (!reader.ValidateDifficulty() || !reader.ValidateInstrument())
                    reader.SkipTrack();
                else if (reader.Instrument != NoteTracks_Chart.Drums)
                    ParseChartTrack(reader, ref parts);
                else
                    drums.ParseChart(reader);
            }
        }

        private static void ParseChartTrack<TChar, TDecoder, TBase>(YARGChartFileReader<TChar, TDecoder, TBase> reader, ref AvailableParts parts)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
            where TDecoder : IStringDecoder<TChar>, new()
            where TBase : unmanaged, IDotChartBases<TChar>
        {
            bool skip = reader.Instrument switch
            {
                NoteTracks_Chart.Single =>       ChartPreparser.Preparse(reader, ref parts.FiveFretGuitar,     ChartPreparser.ValidateFiveFret),
                NoteTracks_Chart.DoubleBass =>   ChartPreparser.Preparse(reader, ref parts.FiveFretBass,       ChartPreparser.ValidateFiveFret),
                NoteTracks_Chart.DoubleRhythm => ChartPreparser.Preparse(reader, ref parts.FiveFretRhythm,     ChartPreparser.ValidateFiveFret),
                NoteTracks_Chart.DoubleGuitar => ChartPreparser.Preparse(reader, ref parts.FiveFretCoopGuitar, ChartPreparser.ValidateFiveFret),
                NoteTracks_Chart.GHLGuitar =>    ChartPreparser.Preparse(reader, ref parts.SixFretGuitar,      ChartPreparser.ValidateSixFret),
                NoteTracks_Chart.GHLBass =>      ChartPreparser.Preparse(reader, ref parts.SixFretBass,        ChartPreparser.ValidateSixFret),
                NoteTracks_Chart.GHLRhythm =>    ChartPreparser.Preparse(reader, ref parts.SixFretRhythm,      ChartPreparser.ValidateSixFret),
                NoteTracks_Chart.GHLCoop =>      ChartPreparser.Preparse(reader, ref parts.SixFretCoopGuitar,  ChartPreparser.ValidateSixFret),
                NoteTracks_Chart.Keys =>         ChartPreparser.Preparse(reader, ref parts.Keys,               ChartPreparser.ValidateFiveFret),
                _ => true,
            };

            if (skip)
                reader.SkipTrack();
        }

        protected static void SetDrums(ref AvailableParts parts, DrumPreparseHandler drumTracker)
        {
            if (drumTracker.Type == DrumsType.FiveLane)
                parts.FiveLaneDrums.Difficulties = drumTracker.ValidatedDiffs;
            else
            {
                parts.FourLaneDrums.Difficulties = drumTracker.ValidatedDiffs;
                if (drumTracker.Type == DrumsType.ProDrums)
                    parts.ProDrums.Difficulties = drumTracker.ValidatedDiffs;
            }
        }

        protected static bool CheckScanValidity(in AvailableParts parts)
        {
            return parts.FiveFretGuitar.SubTracks > 0 ||
                   parts.FiveFretBass.SubTracks > 0 ||
                   parts.FiveFretRhythm.SubTracks > 0 ||
                   parts.FiveFretCoopGuitar.SubTracks > 0 ||
                   parts.Keys.SubTracks > 0 ||

                   parts.SixFretGuitar.SubTracks > 0 ||
                   parts.SixFretBass.SubTracks > 0 ||
                   parts.SixFretRhythm.SubTracks > 0 ||
                   parts.SixFretCoopGuitar.SubTracks > 0 ||

                   parts.FourLaneDrums.SubTracks > 0 ||
                   parts.ProDrums.SubTracks > 0 ||
                   parts.FiveLaneDrums.SubTracks > 0 ||
                   // parts.TrueDrums.subTracks > 0 ||
                   parts.ProGuitar_17Fret.SubTracks > 0 ||
                   parts.ProGuitar_22Fret.SubTracks > 0 ||
                   parts.ProBass_17Fret.SubTracks > 0 ||
                   parts.ProBass_22Fret.SubTracks > 0 ||

                   parts.ProKeys.SubTracks > 0 ||

                   // parts.DJ.subTracks > 0 ||

                   parts.LeadVocals.SubTracks > 0 ||
                   parts.HarmonyVocals.SubTracks > 0;
        }
    }
}
