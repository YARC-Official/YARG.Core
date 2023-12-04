using System.Text;
using YARG.Core.Chart;
using YARG.Core.IO;
using YARG.Core.Song.Preparsers;

namespace YARG.Core.Song
{
    public sealed partial class AvailableParts
    {
        /// <summary>
        /// This not include drums as those must be handled by a dedicated DrumPreparseHandler object.
        /// </summary>
        public void ParseMidi(byte[] file, DrumPreparseHandler drums)
        {
            YARGMidiFile midiFile = new(file);
            foreach (var track in midiFile)
            {
                if (midiFile.TrackNumber == 1 || !track.FindTrackType(Encoding.ASCII, out var type))
                    continue;

                if (type is MidiTrackType.Events or MidiTrackType.Beat)
                    continue;

                switch (type)
                {
                    case MidiTrackType.Guitar_5: if (!FiveFretGuitar.WasParsed())     FiveFretGuitar.Difficulties      = Midi_FiveFret_Preparser.Parse(track); break;
                    case MidiTrackType.Bass_5:   if (!FiveFretBass.WasParsed())       FiveFretBass.Difficulties        = Midi_FiveFret_Preparser.Parse(track); break;
                    case MidiTrackType.Rhythm_5: if (!FiveFretRhythm.WasParsed())     FiveFretRhythm.Difficulties      = Midi_FiveFret_Preparser.Parse(track); break;
                    case MidiTrackType.Coop_5:   if (!FiveFretCoopGuitar.WasParsed()) FiveFretCoopGuitar.Difficulties  = Midi_FiveFret_Preparser.Parse(track); break;
                    case MidiTrackType.Keys:     if (!Keys.WasParsed())               Keys.Difficulties                = Midi_FiveFret_Preparser.Parse(track); break;

                    case MidiTrackType.Guitar_6: if (!SixFretGuitar.WasParsed())      SixFretGuitar.Difficulties       = Midi_SixFret_Preparser.Parse(track); break;
                    case MidiTrackType.Bass_6:   if (!SixFretBass.WasParsed())        SixFretBass.Difficulties         = Midi_SixFret_Preparser.Parse(track); break;
                    case MidiTrackType.Rhythm_6: if (!SixFretRhythm.WasParsed())      SixFretRhythm.Difficulties       = Midi_SixFret_Preparser.Parse(track); break;
                    case MidiTrackType.Coop_6:   if (!SixFretCoopGuitar.WasParsed())  SixFretCoopGuitar.Difficulties   = Midi_SixFret_Preparser.Parse(track); break;

                    case MidiTrackType.Drums: drums.ParseMidi(track); break;

                    case MidiTrackType.Pro_Guitar_17: if (!ProGuitar_17Fret.WasParsed())   ProGuitar_17Fret.Difficulties    = Midi_ProGuitar_Preparser.Parse_17Fret(track); break;
                    case MidiTrackType.Pro_Guitar_22: if (!ProGuitar_22Fret.WasParsed())   ProGuitar_22Fret.Difficulties    = Midi_ProGuitar_Preparser.Parse_22Fret(track); break;
                    case MidiTrackType.Pro_Bass_17:   if (!ProBass_17Fret.WasParsed())     ProBass_17Fret.Difficulties      = Midi_ProGuitar_Preparser.Parse_17Fret(track); break;
                    case MidiTrackType.Pro_Bass_22:   if (!ProBass_22Fret.WasParsed())     ProBass_22Fret.Difficulties      = Midi_ProGuitar_Preparser.Parse_22Fret(track); break;

                    case MidiTrackType.Pro_Keys_E: if (!ProKeys[Difficulty.Easy]   && Midi_ProKeys_Preparser.Parse(track)) ProKeys.SetDifficulty(Difficulty.Easy); break;
                    case MidiTrackType.Pro_Keys_M: if (!ProKeys[Difficulty.Medium] && Midi_ProKeys_Preparser.Parse(track)) ProKeys.SetDifficulty(Difficulty.Medium); break;
                    case MidiTrackType.Pro_Keys_H: if (!ProKeys[Difficulty.Hard]   && Midi_ProKeys_Preparser.Parse(track)) ProKeys.SetDifficulty(Difficulty.Hard); break;
                    case MidiTrackType.Pro_Keys_X: if (!ProKeys[Difficulty.Expert] && Midi_ProKeys_Preparser.Parse(track)) ProKeys.SetDifficulty(Difficulty.Expert); break;

                    case MidiTrackType.Vocals: if (!LeadVocals[0]    && Midi_Vocal_Preparser.ParseLeadTrack(track))    LeadVocals.SetSubtrack(0); break;
                    case MidiTrackType.Harm1:  if (!HarmonyVocals[0] && Midi_Vocal_Preparser.ParseLeadTrack(track))    HarmonyVocals.SetSubtrack(0); break;
                    case MidiTrackType.Harm2:  if (!HarmonyVocals[1] && Midi_Vocal_Preparser.ParseHarmonyTrack(track)) HarmonyVocals.SetSubtrack(1); break;
                    case MidiTrackType.Harm3:  if (!HarmonyVocals[2] && Midi_Vocal_Preparser.ParseHarmonyTrack(track)) HarmonyVocals.SetSubtrack(2); break;
                }
            }

            SetVocalsCount();
        }
    }
}
