using System.IO;
using System.Text;
using YARG.Core.Chart;
using YARG.Core.IO;
using YARG.Core.Song.Preparsers;

namespace YARG.Core.Song
{
    public partial struct AvailableParts
    {
        /// <summary>
        /// This not include drums as those must be handled by a dedicated DrumPreparseHandler object.
        /// </summary>
        public bool ParseMidi(byte[] file, DrumPreparseHandler drums)
        {
            using var stream = new MemoryStream(file, 0, file.Length, false, true);
            var midiFile = new YARGMidiFile(stream);
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
                    case MidiTrackType.Guitar_5: if (!_fiveFretGuitar.WasParsed())     _fiveFretGuitar.Difficulties      = Midi_FiveFret_Preparser.Parse(track); break;
                    case MidiTrackType.Bass_5:   if (!_fiveFretBass.WasParsed())       _fiveFretBass.Difficulties        = Midi_FiveFret_Preparser.Parse(track); break;
                    case MidiTrackType.Rhythm_5: if (!_fiveFretRhythm.WasParsed())     _fiveFretRhythm.Difficulties      = Midi_FiveFret_Preparser.Parse(track); break;
                    case MidiTrackType.Coop_5:   if (!_fiveFretCoopGuitar.WasParsed()) _fiveFretCoopGuitar.Difficulties  = Midi_FiveFret_Preparser.Parse(track); break;
                    case MidiTrackType.Keys:     if (!_keys.WasParsed())               _keys.Difficulties                = Midi_FiveFret_Preparser.Parse(track); break;

                    case MidiTrackType.Guitar_6: if (!_sixFretGuitar.WasParsed())      _sixFretGuitar.Difficulties       = Midi_SixFret_Preparser.Parse(track); break;
                    case MidiTrackType.Bass_6:   if (!_sixFretBass.WasParsed())        _sixFretBass.Difficulties         = Midi_SixFret_Preparser.Parse(track); break;
                    case MidiTrackType.Rhythm_6: if (!_sixFretRhythm.WasParsed())      _sixFretRhythm.Difficulties       = Midi_SixFret_Preparser.Parse(track); break;
                    case MidiTrackType.Coop_6:   if (!_sixFretCoopGuitar.WasParsed())  _sixFretCoopGuitar.Difficulties   = Midi_SixFret_Preparser.Parse(track); break;

                    case MidiTrackType.Drums: drums.ParseMidi(track); break;

                    case MidiTrackType.Pro_Guitar_17: if (!_proGuitar_17Fret.WasParsed())   _proGuitar_17Fret.Difficulties = Midi_ProGuitar_Preparser.Parse_17Fret(track); break;
                    case MidiTrackType.Pro_Guitar_22: if (!_proGuitar_22Fret.WasParsed())   _proGuitar_22Fret.Difficulties = Midi_ProGuitar_Preparser.Parse_22Fret(track); break;
                    case MidiTrackType.Pro_Bass_17:   if (!_proBass_17Fret.WasParsed())     _proBass_17Fret.Difficulties   = Midi_ProGuitar_Preparser.Parse_17Fret(track); break;
                    case MidiTrackType.Pro_Bass_22:   if (!_proBass_22Fret.WasParsed())     _proBass_22Fret.Difficulties   = Midi_ProGuitar_Preparser.Parse_22Fret(track); break;

                    case MidiTrackType.Pro_Keys_E: if (!_proKeys[Difficulty.Easy]   && Midi_ProKeys_Preparser.Parse(track)) _proKeys.SetDifficulty(Difficulty.Easy); break;
                    case MidiTrackType.Pro_Keys_M: if (!_proKeys[Difficulty.Medium] && Midi_ProKeys_Preparser.Parse(track)) _proKeys.SetDifficulty(Difficulty.Medium); break;
                    case MidiTrackType.Pro_Keys_H: if (!_proKeys[Difficulty.Hard]   && Midi_ProKeys_Preparser.Parse(track)) _proKeys.SetDifficulty(Difficulty.Hard); break;
                    case MidiTrackType.Pro_Keys_X: if (!_proKeys[Difficulty.Expert] && Midi_ProKeys_Preparser.Parse(track)) _proKeys.SetDifficulty(Difficulty.Expert); break;

                    case MidiTrackType.Vocals: if (!_leadVocals[0]    && Midi_Vocal_Preparser.ParseLeadTrack(track))    _leadVocals.SetSubtrack(0); break;
                    case MidiTrackType.Harm1:  if (!_harmonyVocals[0] && Midi_Vocal_Preparser.ParseLeadTrack(track))    _harmonyVocals.SetSubtrack(0); break;
                    case MidiTrackType.Harm2:  if (!_harmonyVocals[1] && Midi_Vocal_Preparser.ParseHarmonyTrack(track)) _harmonyVocals.SetSubtrack(1); break;
                    case MidiTrackType.Harm3:  if (!_harmonyVocals[2] && Midi_Vocal_Preparser.ParseHarmonyTrack(track)) _harmonyVocals.SetSubtrack(2); break;
                }
            }

            SetVocalsCount();
            return true;
        }
    }
}
