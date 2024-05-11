using System;
using System.Collections.Generic;

namespace YARG.Core.Chart.Parsing
{
    internal static partial class DrumsHandler
    {
        private static int GetFourLaneDrumPad(List<IntermediateDrumsNote> notes, int index, in ParseSettings settings)
        {
            var pad = settings.DrumsType switch
            {
                DrumsType.FourLane => GetFourLaneFromFourLane(notes, index, pro: false),
                DrumsType.FiveLane => GetFourLaneFromFiveLane(notes, index, pro: false),
                _ => throw new InvalidOperationException($"Unexpected drums type {settings.DrumsType}! (Drums type should have been calculated by now)")
            };

            return (int) pad;
        }

        private static int GetFourLaneProDrumPad(List<IntermediateDrumsNote> notes, int index, in ParseSettings settings)
        {
            var pad = settings.DrumsType switch
            {
                DrumsType.FourLane => GetFourLaneFromFourLane(notes, index, pro: true),
                DrumsType.FiveLane => GetFourLaneFromFiveLane(notes, index, pro: true),
                _ => throw new InvalidOperationException($"Unexpected drums type {settings.DrumsType}! (Drums type should have been calculated by now)")
            };

            return (int) pad;
        }

        private static FourLaneDrumPad GetFourLaneFromFourLane(List<IntermediateDrumsNote> notes, int index, bool pro)
        {
            var note = notes[index];
            var pad = note.Pad switch
            {
                IntermediateDrumPad.Kick => FourLaneDrumPad.Kick,
                IntermediateDrumPad.Lane1 => FourLaneDrumPad.RedDrum,
                IntermediateDrumPad.Lane2 => FourLaneDrumPad.YellowDrum,
                IntermediateDrumPad.Lane3 => FourLaneDrumPad.BlueDrum,
                IntermediateDrumPad.Lane4 => FourLaneDrumPad.GreenDrum,
                IntermediateDrumPad.Lane5 => FourLaneDrumPad.GreenDrum,
                _ => throw new InvalidOperationException($"Invalid intermediate drum pad {note.Pad}!")
            };

            if (pro)
            {
                // Disco flip
                if ((note.Flags & IntermediateDrumsNoteFlags.DiscoFlip) != 0)
                {
                    if (pad == FourLaneDrumPad.RedDrum)
                    {
                        // Red drums in disco flip are turned into yellow cymbals
                        pad = FourLaneDrumPad.YellowDrum;
                        note.Flags |= IntermediateDrumsNoteFlags.Cymbal;
                    }
                    else if (pad == FourLaneDrumPad.YellowDrum)
                    {
                        // Both yellow cymbals and yellow drums are turned into red drums in disco flip
                        pad = FourLaneDrumPad.RedDrum;
                        note.Flags &= IntermediateDrumsNoteFlags.Cymbal;
                    }
                }

                // Cymbal marking
                if ((note.Flags & IntermediateDrumsNoteFlags.Cymbal) != 0)
                {
                    pad = pad switch
                    {
                        FourLaneDrumPad.YellowDrum => FourLaneDrumPad.YellowCymbal,
                        FourLaneDrumPad.BlueDrum   => FourLaneDrumPad.BlueCymbal,
                        FourLaneDrumPad.GreenDrum  => FourLaneDrumPad.GreenCymbal,
                        _ => throw new InvalidOperationException($"Cannot mark pad {pad} as a cymbal!")
                    };
                }
            }

            return pad;
        }

        private static FourLaneDrumPad GetFourLaneFromFiveLane(List<IntermediateDrumsNote> notes, int index, bool pro)
        {
            // Conversion table:
            // | 5-lane | 4-lane Pro    |
            // | :----- | :---------    |
            // | Red    | Red           |
            // | Yellow | Yellow cymbal |
            // | Blue   | Blue tom      |
            // | Orange | Green cymbal  |
            // | Green  | Green tom     |
            // | O + G  | G cym + B tom |

            var fiveLanePad = GetFiveLaneFromFiveLane(notes, index);
            var pad = fiveLanePad switch
            {
                FiveLaneDrumPad.Kick   => FourLaneDrumPad.Kick,
                FiveLaneDrumPad.Red    => FourLaneDrumPad.RedDrum,
                FiveLaneDrumPad.Yellow => FourLaneDrumPad.YellowCymbal,
                FiveLaneDrumPad.Blue   => FourLaneDrumPad.BlueDrum,
                FiveLaneDrumPad.Orange => FourLaneDrumPad.GreenCymbal,
                FiveLaneDrumPad.Green  => FourLaneDrumPad.GreenDrum,
                _ => throw new InvalidOperationException($"Invalid five lane drum pad {fiveLanePad}!")
            };

            // Handle potential overlaps
            if (pad is FourLaneDrumPad.GreenCymbal)
            {
                var (start, end) = TrackHandler.GetEventChord(notes, index);
                for (int i = start; i < end; i++)
                {
                    if (i == index)
                        continue;

                    var otherPad = GetFiveLaneFromFiveLane(notes, i);
                    pad = (pad, otherPad) switch
                    {
                        // (Calculated pad, other note in chord) => corrected pad to prevent same-color overlapping
                        (FourLaneDrumPad.GreenCymbal, FiveLaneDrumPad.Green) => FourLaneDrumPad.BlueCymbal,
                        _ => pad
                    };
                }
            }

            // Down-convert to standard 4-lane
            if (!pro)
            {
                pad = pad switch
                {
                    FourLaneDrumPad.YellowCymbal => FourLaneDrumPad.YellowDrum,
                    FourLaneDrumPad.BlueCymbal   => FourLaneDrumPad.BlueDrum,
                    FourLaneDrumPad.GreenCymbal  => FourLaneDrumPad.GreenDrum,
                    _ => pad
                };
            }

            return pad;
        }
    }
}