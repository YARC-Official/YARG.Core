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

            // A tom marker makes the drums pro only when it covers a note of any difficulty, kicks included:
            // one on the marker's first tick, or after it and before its end. Which notes of a tick are covered
            // is known once all of the tick's events are read.
            Span<int> openToms = stackalloc int[GREEN_FLAG - YELLOW_FLAG + 1];
            long tick = -1;
            bool noteOnTick = false;
            bool tomOnTick = false;

            var note = default(MidiNote);
            var stats = default(MidiStats);
            while (track.ParseEvent(ref stats))
            {
                if (stats.Type != MidiEventType.Note_On && stats.Type != MidiEventType.Note_Off)
                {
                    continue;
                }

                track.ExtractMidiNote(ref note);
                // Anything greater than DrumsType.FiveLane, in bits, is a type no song.ini key or marker has set yet
                if (drumsType > DrumsType.FiveLane)
                {
                    if (stats.Position > tick)
                    {
                        if (CoversNote(openToms, noteOnTick, tomOnTick))
                        {
                            drumsType = DrumsType.ProDrums;
                        }
                        tick = stats.Position;
                        noteOnTick = false;
                        tomOnTick = false;
                    }

                    // Note Ons with no velocity equates to a note Off by spec
                    bool isNoteOn = stats.Type == MidiEventType.Note_On && note.Velocity > 0;
                    if (YELLOW_FLAG <= note.Value && note.Value <= GREEN_FLAG)
                    {
                        ref int open = ref openToms[note.Value - YELLOW_FLAG];
                        if (isNoteOn)
                        {
                            ++open;
                            tomOnTick = true;
                        }
                        else if (open > 0)
                        {
                            --open;
                        }
                    }
                    else if (isNoteOn && IsDrumNote(note.Value))
                    {
                        noteOnTick = true;
                    }
                }

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
                    if (laneIndex > FIVE_LANE_INDEX)
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
                            // A green tom in pro, or green in five lane: the drums can't be four lane.
                            // A tom marker anywhere still makes them pro, so the type stays open.
                            drumsType &= ~DrumsType.FourLane;
                        }
                    }
                    // NoteOff here
                    else if ((statusBitMask & statusMask) > 0)
                    {
                        validations |= diffMask;
                    }
                }

                if (validations == MidiPreparser_Constants.ALL_DIFFICULTIES_PLUS && drumsType is DrumsType.ProDrums or DrumsType.FiveLane)
                {
                    break;
                }
            }

            if (drumsType > DrumsType.FiveLane && CoversNote(openToms, noteOnTick, tomOnTick))
            {
                drumsType = DrumsType.ProDrums;
            }

            // Add beginner to the mask if easy is present (mask easy, shift right, or result with validations)
            validations |= (DifficultyMask)((int)(validations & DifficultyMask.Easy) >> 1);

            return validations;
        }

        // Once a tick's events are read: a note on it is covered by a marker that starts there, or by one still on
        private static bool CoversNote(Span<int> openToms, bool noteOnTick, bool tomOnTick)
        {
            return noteOnTick && (tomOnTick || openToms[0] > 0 || openToms[1] > 0 || openToms[2] > 0);
        }

        // A double kick, kick, pad or 5-lane green note of any difficulty: 59 to 65 on Easy, up to 95 to 101 on Expert
        private static bool IsDrumNote(int value)
        {
            const int EASY_DOUBLE_KICK_NOTE = 59;
            const int EXPERT_FIVE_LANE_NOTE = 101;
            const int DRUM_NOTES_PER_DIFFICULTY = 7;
            int offset = value - EASY_DOUBLE_KICK_NOTE;
            return offset >= 0 && value <= EXPERT_FIVE_LANE_NOTE
                && offset % MidiPreparser_Constants.NOTES_PER_DIFFICULTY < DRUM_NOTES_PER_DIFFICULTY;
        }
    }
}