using System.Text;
using YARG.Core.Song.Deserialization;
using YARG.Core.Song.Preparsers;

namespace YARG.Core.Song
{
    public sealed partial class AvailableParts
    {
        /// <summary>
        /// This not include drums as those must be handled by a dedicated DrumPreparseHandler object.
        /// </summary>
        public DrumType ParseMidi(byte[] file, DrumType drumType)
        {
            YARGMidiReader reader = new(file);
            DrumPreparseHandler drums = new(drumType);
            while (reader.StartTrack())
            {
                if (reader.GetTrackNumber() == 1 || reader.GetEvent().type != MidiEventType.Text_TrackName)
                    continue;

                string name = Encoding.ASCII.GetString(reader.ExtractTextOrSysEx());
                if (YARGMidiReader.TRACKNAMES.TryGetValue(name, out var type) && type != MidiTrackType.Events && type != MidiTrackType.Beat)
                {
                    if (type != MidiTrackType.Drums)
                    {
                        switch (type)
                        {
                            case MidiTrackType.Guitar_5:      if (!FiveFretGuitar.WasParsed())     FiveFretGuitar.subTracks     |= MidiInstrumentPreparser.Parse<Midi_FiveFret>(reader); break;
                            case MidiTrackType.Bass_5:        if (!FiveFretBass.WasParsed())       FiveFretBass.subTracks       |= MidiInstrumentPreparser.Parse<Midi_FiveFret>(reader); break;
                            case MidiTrackType.Rhythm_5:      if (!FiveFretRhythm.WasParsed())     FiveFretRhythm.subTracks     |= MidiInstrumentPreparser.Parse<Midi_FiveFret>(reader); break;
                            case MidiTrackType.Coop_5:        if (!FiveFretCoopGuitar.WasParsed()) FiveFretCoopGuitar.subTracks |= MidiInstrumentPreparser.Parse<Midi_FiveFret>(reader); break;
                            case MidiTrackType.Guitar_6:      if (!SixFretGuitar.WasParsed())      SixFretGuitar.subTracks      |= MidiInstrumentPreparser.Parse<Midi_SixFret>(reader); break;
                            case MidiTrackType.Bass_6:        if (!SixFretBass.WasParsed())        SixFretBass.subTracks        |= MidiInstrumentPreparser.Parse<Midi_SixFret>(reader); break;
                            case MidiTrackType.Rhythm_6:      if (!SixFretRhythm.WasParsed())      SixFretRhythm.subTracks      |= MidiInstrumentPreparser.Parse<Midi_SixFret>(reader); break;
                            case MidiTrackType.Coop_6:        if (!SixFretCoopGuitar.WasParsed())  SixFretCoopGuitar.subTracks  |= MidiInstrumentPreparser.Parse<Midi_FiveFret>(reader); break;
                            case MidiTrackType.Pro_Guitar_17: if (!ProGuitar_17Fret.WasParsed())   ProGuitar_17Fret.subTracks   |= MidiInstrumentPreparser.Parse<Midi_ProGuitar17>(reader); break;
                            case MidiTrackType.Pro_Guitar_22: if (!ProGuitar_22Fret.WasParsed())   ProGuitar_22Fret.subTracks   |= MidiInstrumentPreparser.Parse<Midi_ProGuitar22>(reader); break;
                            case MidiTrackType.Pro_Bass_17:   if (!ProBass_17Fret.WasParsed())     ProBass_17Fret.subTracks     |= MidiInstrumentPreparser.Parse<Midi_ProGuitar17>(reader); break;
                            case MidiTrackType.Pro_Bass_22:   if (!ProBass_22Fret.WasParsed())     ProBass_22Fret.subTracks     |= MidiInstrumentPreparser.Parse<Midi_ProGuitar22>(reader); break;
                            case MidiTrackType.Keys:          if (!Keys.WasParsed())               Keys.subTracks               |= MidiInstrumentPreparser.Parse<Midi_Keys>(reader); break;
                            case MidiTrackType.Pro_Keys_X:    if (!ProKeys[3])                     ProKeys.subTracks            |= MidiInstrumentPreparser.Parse<Midi_ProKeysX>(reader); break;
                            case MidiTrackType.Pro_Keys_H:    if (!ProKeys[2])                     ProKeys.subTracks            |= MidiInstrumentPreparser.Parse<Midi_ProKeysH>(reader); break;
                            case MidiTrackType.Pro_Keys_M:    if (!ProKeys[1])                     ProKeys.subTracks            |= MidiInstrumentPreparser.Parse<Midi_ProKeysM>(reader); break;
                            case MidiTrackType.Pro_Keys_E:    if (!ProKeys[0])                     ProKeys.subTracks            |= MidiInstrumentPreparser.Parse<Midi_ProKeysE>(reader); break;

                            case MidiTrackType.Vocals: if (!LeadVocals[0]    && MidiPreparser.Parse<Midi_Vocal>(reader))   LeadVocals.Set(0); break;
                            case MidiTrackType.Harm1:  if (!HarmonyVocals[0] && MidiPreparser.Parse<Midi_Vocal>(reader))   HarmonyVocals.Set(0); break;
                            case MidiTrackType.Harm2:  if (!HarmonyVocals[1] && MidiPreparser.Parse<Midi_Harmony>(reader)) HarmonyVocals.Set(1); break;
                            case MidiTrackType.Harm3:  if (!HarmonyVocals[2] && MidiPreparser.Parse<Midi_Harmony>(reader)) HarmonyVocals.Set(2); break;
                        }
                    }
                    else
                        drums.ParseMidi(reader);
                }
            }

            if (drums.Type == DrumType.FIVE_LANE)
                FiveLaneDrums.subTracks = drums.ValidatedDiffs;
            else
            {
                FourLaneDrums.subTracks = drums.ValidatedDiffs;
                if (drums.Type == DrumType.FOUR_PRO)
                    ProDrums.subTracks = drums.ValidatedDiffs;
            }
            return drums.Type;
        }
    }
}
