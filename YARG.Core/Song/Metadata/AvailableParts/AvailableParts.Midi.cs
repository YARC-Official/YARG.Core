using System.Text;
using YARG.Core.Chart;
using YARG.Core.Song.Deserialization;
using YARG.Core.Song.Preparsers;

namespace YARG.Core.Song
{
    public sealed partial class AvailableParts
    {
        /// <summary>
        /// This not include drums as those must be handled by a dedicated DrumPreparseHandler object.
        /// </summary>
        public DrumsType ParseMidi(byte[] file, DrumsType drumType)
        {
            YARGMidiReader reader = new(file);
            DrumPreparseHandler drums = new(drumType);
            while (reader.StartTrack())
            {
                if (reader.GetTrackNumber() == 1 || reader.GetEventType() != MidiEventType.Text_TrackName)
                    continue;

                string name = Encoding.ASCII.GetString(reader.ExtractTextOrSysEx());
                if (YARGMidiReader.TRACKNAMES.TryGetValue(name, out var type) && type != MidiTrackType.Events && type != MidiTrackType.Beat)
                {
                    if (type != MidiTrackType.Drums)
                    {
                        switch (type)
                        {
                            case MidiTrackType.Guitar_5:      if (!FiveFretGuitar.WasParsed())     FiveFretGuitar.subTracks      = Midi_FiveFret.Parse(reader); break;
                            case MidiTrackType.Bass_5:        if (!FiveFretBass.WasParsed())       FiveFretBass.subTracks        = Midi_FiveFret.Parse(reader); break;
                            case MidiTrackType.Rhythm_5:      if (!FiveFretRhythm.WasParsed())     FiveFretRhythm.subTracks      = Midi_FiveFret.Parse(reader); break;
                            case MidiTrackType.Coop_5:        if (!FiveFretCoopGuitar.WasParsed()) FiveFretCoopGuitar.subTracks  = Midi_FiveFret.Parse(reader); break;
                            case MidiTrackType.Guitar_6:      if (!SixFretGuitar.WasParsed())      SixFretGuitar.subTracks       = Midi_SixFret.Parse(reader); break;
                            case MidiTrackType.Bass_6:        if (!SixFretBass.WasParsed())        SixFretBass.subTracks         = Midi_SixFret.Parse(reader); break;
                            case MidiTrackType.Rhythm_6:      if (!SixFretRhythm.WasParsed())      SixFretRhythm.subTracks       = Midi_SixFret.Parse(reader); break;
                            case MidiTrackType.Coop_6:        if (!SixFretCoopGuitar.WasParsed())  SixFretCoopGuitar.subTracks   = Midi_SixFret.Parse(reader); break;
                            case MidiTrackType.Pro_Guitar_17: if (!ProGuitar_17Fret.WasParsed())   ProGuitar_17Fret.subTracks    = Midi_ProGuitar.Parse_17Fret(reader); break;
                            case MidiTrackType.Pro_Guitar_22: if (!ProGuitar_22Fret.WasParsed())   ProGuitar_22Fret.subTracks    = Midi_ProGuitar.Parse_22Fret(reader); break;
                            case MidiTrackType.Pro_Bass_17:   if (!ProBass_17Fret.WasParsed())     ProBass_17Fret.subTracks      = Midi_ProGuitar.Parse_17Fret(reader); break;
                            case MidiTrackType.Pro_Bass_22:   if (!ProBass_22Fret.WasParsed())     ProBass_22Fret.subTracks      = Midi_ProGuitar.Parse_22Fret(reader); break;
                            case MidiTrackType.Keys:          if (!Keys.WasParsed())               Keys.subTracks                = Midi_Keys.Parse(reader); break;
                            case MidiTrackType.Pro_Keys_E:
                            case MidiTrackType.Pro_Keys_M:
                            case MidiTrackType.Pro_Keys_H:
                            case MidiTrackType.Pro_Keys_X:
                                {
                                    int index = type - MidiTrackType.Pro_Keys_E;
                                    if (!ProKeys[index] && Midi_ProKeys.Parse(reader))
                                        ProKeys.Set(index);
                                    break;
                                }
                            case MidiTrackType.Vocals: if (!LeadVocals[0]    && Midi_Vocal.ParseLeadTrack(reader))    LeadVocals.Set(0); break;
                            case MidiTrackType.Harm1:  if (!HarmonyVocals[0] && Midi_Vocal.ParseLeadTrack(reader))    HarmonyVocals.Set(0); break;
                            case MidiTrackType.Harm2:  if (!HarmonyVocals[1] && Midi_Vocal.ParseHarmonyTrack(reader)) HarmonyVocals.Set(1); break;
                            case MidiTrackType.Harm3:  if (!HarmonyVocals[2] && Midi_Vocal.ParseHarmonyTrack(reader)) HarmonyVocals.Set(2); break;
                        }
                    }
                    else
                        drums.ParseMidi(reader);
                }
            }

            SetDrums(drums);
            SetVocalsCount();
            return drums.Type;
        }
    }
}
