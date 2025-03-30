using System.Text;
using YARG.Core.Chart;
using YARG.Core.IO;

namespace YARG.Core.Song
{
    public abstract partial class SongEntry
    {
        private protected static ScanExpected<long> ParseMidi(in FixedArray<byte> file, ref AvailableParts parts, ref DrumsType drumsType)
        {
            var midiFile = YARGMidiFile.Load(in file);
            if (midiFile.Resolution == 0)
            {
                return new ScanUnexpected(ScanResult.InvalidResolution);
            }

            bool harm2 = false;
            bool harm3 = false;
            while (midiFile.GetNextTrack(out var _, out var track))
            {
                if (!track.FindTrackName(out var trackname))
                {
                    return new ScanUnexpected(ScanResult.MultipleMidiTrackNames);
                }

                if (!YARGMidiTrack.TRACKNAMES.TryGetValue(trackname.GetString(Encoding.ASCII), out var type))
                {
                    continue;
                }

                switch (type)
                {
                    case MidiTrackType.Guitar_5: if (!parts.FiveFretGuitar.IsActive())     parts.FiveFretGuitar.Difficulties     = Midi_FiveFret_Preparser.Parse(track); break;
                    case MidiTrackType.Bass_5:   if (!parts.FiveFretBass.IsActive())       parts.FiveFretBass.Difficulties       = Midi_FiveFret_Preparser.Parse(track); break;
                    case MidiTrackType.Rhythm_5: if (!parts.FiveFretRhythm.IsActive())     parts.FiveFretRhythm.Difficulties     = Midi_FiveFret_Preparser.Parse(track); break;
                    case MidiTrackType.Coop_5:   if (!parts.FiveFretCoopGuitar.IsActive()) parts.FiveFretCoopGuitar.Difficulties = Midi_FiveFret_Preparser.Parse(track); break;
                    case MidiTrackType.Keys:     if (!parts.Keys.IsActive())               parts.Keys.Difficulties               = Midi_FiveFret_Preparser.Parse(track); break;

                    case MidiTrackType.Guitar_6: if (!parts.SixFretGuitar.IsActive())      parts.SixFretGuitar.Difficulties      = Midi_SixFret_Preparser.Parse(track); break;
                    case MidiTrackType.Bass_6:   if (!parts.SixFretBass.IsActive())        parts.SixFretBass.Difficulties        = Midi_SixFret_Preparser.Parse(track); break;
                    case MidiTrackType.Rhythm_6: if (!parts.SixFretRhythm.IsActive())      parts.SixFretRhythm.Difficulties      = Midi_SixFret_Preparser.Parse(track); break;
                    case MidiTrackType.Coop_6:   if (!parts.SixFretCoopGuitar.IsActive())  parts.SixFretCoopGuitar.Difficulties  = Midi_SixFret_Preparser.Parse(track); break;

                    case MidiTrackType.Drums:      if (!parts.FourLaneDrums.IsActive()) parts.FourLaneDrums.Difficulties = Midi_Drums_Preparser.Parse(track, ref drumsType); break;
                    case MidiTrackType.EliteDrums: if (!parts.EliteDrums.IsActive())    parts.EliteDrums.Difficulties    = Midi_EliteDrums_Preparser.Parse(track); break;

                    case MidiTrackType.Pro_Guitar_17: if (!parts.ProGuitar_17Fret.IsActive()) parts.ProGuitar_17Fret.Difficulties = Midi_ProGuitar_Preparser.Parse_17Fret(track); break;
                    case MidiTrackType.Pro_Guitar_22: if (!parts.ProGuitar_22Fret.IsActive()) parts.ProGuitar_22Fret.Difficulties = Midi_ProGuitar_Preparser.Parse_22Fret(track); break;
                    case MidiTrackType.Pro_Bass_17:   if (!parts.ProBass_17Fret.IsActive())   parts.ProBass_17Fret.Difficulties   = Midi_ProGuitar_Preparser.Parse_17Fret(track); break;
                    case MidiTrackType.Pro_Bass_22:   if (!parts.ProBass_22Fret.IsActive())   parts.ProBass_22Fret.Difficulties   = Midi_ProGuitar_Preparser.Parse_22Fret(track); break;

                    case MidiTrackType.Pro_Keys_E: if (!parts.ProKeys[Difficulty.Easy]   && Midi_ProKeys_Preparser.Parse(track)) parts.ProKeys.ActivateDifficulty(Difficulty.Easy); break;
                    case MidiTrackType.Pro_Keys_M: if (!parts.ProKeys[Difficulty.Medium] && Midi_ProKeys_Preparser.Parse(track)) parts.ProKeys.ActivateDifficulty(Difficulty.Medium); break;
                    case MidiTrackType.Pro_Keys_H: if (!parts.ProKeys[Difficulty.Hard]   && Midi_ProKeys_Preparser.Parse(track)) parts.ProKeys.ActivateDifficulty(Difficulty.Hard); break;
                    case MidiTrackType.Pro_Keys_X: if (!parts.ProKeys[Difficulty.Expert] && Midi_ProKeys_Preparser.Parse(track)) parts.ProKeys.ActivateDifficulty(Difficulty.Expert); break;

                    case MidiTrackType.Vocals: if (!parts.LeadVocals[0]    && Midi_Vocal_Preparser.Parse(track, true))  parts.LeadVocals.ActivateSubtrack(0); break;
                    case MidiTrackType.Harm1:  if (!parts.HarmonyVocals[0] && Midi_Vocal_Preparser.Parse(track, true))  parts.HarmonyVocals.ActivateSubtrack(0); break;
                    case MidiTrackType.Harm2:  if (!harm2) harm2 = Midi_Vocal_Preparser.Parse(track, false); break;
                    case MidiTrackType.Harm3:  if (!harm3) harm3 = Midi_Vocal_Preparser.Parse(track, false); break;
                }
            }

            // HARM 2/3 are not playable without HARM1 phrases
            if (parts.HarmonyVocals[0])
            {
                if (harm2)
                {
                    parts.HarmonyVocals.ActivateSubtrack(1);
                }
                if (harm3)
                {
                    parts.HarmonyVocals.ActivateSubtrack(2);
                }
            }
            return midiFile.Resolution;
        }

        protected static void FinalizeDrums(ref AvailableParts parts, DrumsType drumsType)
        {
            if ((drumsType & DrumsType.FourLane) != DrumsType.FourLane)
            {
                if ((drumsType & DrumsType.ProDrums) == DrumsType.ProDrums)
                {
                    parts.ProDrums.Difficulties = parts.FourLaneDrums.Difficulties;
                }
                else
                {
                    parts.FiveLaneDrums.Difficulties = parts.FourLaneDrums.Difficulties;
                    parts.FourLaneDrums.Difficulties = DifficultyMask.None;
                }
            }
        }

        protected static bool IsValid(in AvailableParts parts)
        {
            return parts.FiveFretGuitar.IsActive()
                || parts.FiveFretBass.IsActive()
                || parts.FiveFretRhythm.IsActive()
                || parts.FiveFretCoopGuitar.IsActive()
                || parts.Keys.IsActive()
                || parts.SixFretGuitar.IsActive()
                || parts.SixFretBass.IsActive()
                || parts.SixFretRhythm.IsActive()
                || parts.SixFretCoopGuitar.IsActive()
                || parts.FourLaneDrums.IsActive()
                || parts.ProDrums.IsActive()
                || parts.FiveLaneDrums.IsActive()
                || parts.EliteDrums.IsActive()
                || parts.ProGuitar_17Fret.IsActive()
                || parts.ProGuitar_22Fret.IsActive()
                || parts.ProBass_17Fret.IsActive()
                || parts.ProBass_22Fret.IsActive()
                || parts.ProKeys.IsActive()
                || parts.LeadVocals.IsActive()
                || parts.HarmonyVocals.IsActive();
        }
    }
}
