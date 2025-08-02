using System;
using YARG.Core.Chart;
using YARG.Core.IO;

namespace YARG.Core.Song
{
    internal static class Midi_Drums_Preparser
    {
        private static readonly int[] INDICES = new int[MidiPreparser_Constants.NUM_DIFFICULTIES * MidiPreparser_Constants.NOTES_PER_DIFFICULTY]
        {
            0, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12,
            0, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12,
            0, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12,
            0, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12,
        };

        public static unsafe DifficultyMask Parse(YARGMidiTrack track, ref DrumsType drumsType)
        {
            const int MAX_NUMPADS = 7;
            const int DRUMNOTE_MAX = 101;
            const int DOUBLE_KICK_NOTE = 95;
            const int DOUBLE_KICK_MASK = 1 << (3 * MAX_NUMPADS + 1);
            const int FIVE_LANE_INDEX = 6;
            const int YELLOW_FLAG = 110;
            const int GREEN_FLAG = 112;

            var validations = DifficultyMask.None;
            int statusBitMask = 0;
            var note = default(MidiNote);
            var stats = default(MidiStats);
            while (track.ParseEvent(ref stats))
            {
                if (stats.Type != MidiEventType.Note_On && stats.Type != MidiEventType.Note_Off)
                {
                    continue;
                }

                track.ExtractMidiNote(ref note);
                // Must be checked first as it still resides in the normal note range window
                if (note.Value == DOUBLE_KICK_NOTE)
                {
                    if ((validations & DifficultyMask.ExpertPlus) > 0)
                    {
                        continue;
                    }

                    // Note Ons with no velocity equates to a note Off by spec
                    if (stats.Type == MidiEventType.Note_On && note.Velocity > 0)
                    {
                        statusBitMask |= DOUBLE_KICK_MASK;
                    }
                    // NoteOff here
                    else if ((statusBitMask & DOUBLE_KICK_MASK) > 0)
                    {
                        validations |= DifficultyMask.Expert | DifficultyMask.ExpertPlus;
                    }
                }
                else if (MidiPreparser_Constants.DEFAULT_NOTE_MIN <= note.Value && note.Value <= DRUMNOTE_MAX)
                {
                    int noteOffset = note.Value - MidiPreparser_Constants.DEFAULT_NOTE_MIN;
                    int diffIndex = MidiPreparser_Constants.DIFF_INDICES[noteOffset];
                    var diffMask = (DifficultyMask) (1 << (diffIndex + 1));
                    // Necessary to account for undetermined five lane
                    // Anything greater than DrumsType.FiveLane, in bits, *contains* the DrumsType.FiveLane bit
                    if ((validations & diffMask) > DifficultyMask.None && drumsType <= DrumsType.FiveLane)
                    {
                        continue;
                    }

                    int laneIndex = INDICES[noteOffset];
                    // The double "greater than" check against FIVE_LANE_INDEX keeps the number of comparisons performed
                    // to ONE when laneIndex is less than that value.
                    //
                    // And if drumsType is less than DrumsType.FiveLane, the DrumsType.FiveLane bit is not set.
                    // Testing FIVE_LANE_INDEX would thereby produce no results.
                    if (laneIndex >= FIVE_LANE_INDEX && (laneIndex > FIVE_LANE_INDEX || drumsType < DrumsType.FiveLane))
                    {
                        continue;
                    }

                    int statusMask = 1 << (diffIndex * MAX_NUMPADS + laneIndex);
                    // Note Ons with no velocity equates to a note Off by spec
                    if (stats.Type == MidiEventType.Note_On && note.Velocity > 0)
                    {
                        statusBitMask |= statusMask;
                        if (laneIndex == FIVE_LANE_INDEX)
                        {
                            drumsType = DrumsType.FiveLane;
                        }
                    }
                    // NoteOff here
                    else if ((statusBitMask & statusMask) > 0)
                    {
                        validations |= diffMask;
                    }
                }
                else if (YELLOW_FLAG <= note.Value && note.Value <= GREEN_FLAG && drumsType.Has(DrumsType.ProDrums))
                {
                    drumsType = DrumsType.ProDrums;
                }

                if (validations == MidiPreparser_Constants.ALL_DIFFICULTIES_PLUS && (drumsType == DrumsType.FourLane || drumsType == DrumsType.ProDrums || drumsType == DrumsType.FiveLane))
                {
                    break;
                }
            }
            return validations;
        }
    }
}