using System;
using System.Collections.Generic;

namespace YARG.Core.Chart.Parsing
{
    internal static partial class DrumsHandler
    {
        private static int GetFiveLaneDrumPad(List<IntermediateDrumsNote> notes, int index, in ParseSettings settings)
        {
            var pad = settings.DrumsType switch
            {
                DrumsType.FourLane => GetFiveLaneFromFourLane(notes, index),
                DrumsType.FiveLane => GetFiveLaneFromFiveLane(notes, index),
                _ => throw new InvalidOperationException($"Unexpected drums type {settings.DrumsType}! (Drums type should have been calculated by now)")
            };

            return (int) pad;
        }

        private static FiveLaneDrumPad GetFiveLaneFromFiveLane(List<IntermediateDrumsNote> notes, int index)
        {
            var note = notes[index];
            return note.Pad switch
            {
                IntermediateDrumPad.Kick => FiveLaneDrumPad.Kick,
                IntermediateDrumPad.Lane1 => FiveLaneDrumPad.Red,
                IntermediateDrumPad.Lane2 => FiveLaneDrumPad.Yellow,
                IntermediateDrumPad.Lane3 => FiveLaneDrumPad.Blue,
                IntermediateDrumPad.Lane4 => FiveLaneDrumPad.Orange,
                IntermediateDrumPad.Lane5 => FiveLaneDrumPad.Green,
                _ => throw new InvalidOperationException($"Invalid intermediate drum pad {note.Pad}!")
            };
        }

        private static FiveLaneDrumPad GetFiveLaneFromFourLane(List<IntermediateDrumsNote> notes, int index)
        {
            // Conversion table:
            // | 4-lane Pro    | 5-lane |
            // | :---------    | :----- |
            // | Red           | Red    |
            // | Yellow cymbal | Yellow |
            // | Yellow tom    | Blue   |
            // | Blue cymbal   | Orange |
            // | Blue tom      | Blue   |
            // | Green cymbal  | Orange |
            // | Green tom     | Green  |
            // | Y tom + B tom | R + B  |
            // | B cym + G cym | Y + O  |

            var fourLanePad = GetFourLaneFromFourLane(notes, index, pro: true);
            var pad = fourLanePad switch
            {
                FourLaneDrumPad.Kick         => FiveLaneDrumPad.Kick,
                FourLaneDrumPad.RedDrum      => FiveLaneDrumPad.Red,
                FourLaneDrumPad.YellowCymbal => FiveLaneDrumPad.Yellow,
                FourLaneDrumPad.YellowDrum   => FiveLaneDrumPad.Blue,
                FourLaneDrumPad.BlueCymbal   => FiveLaneDrumPad.Orange,
                FourLaneDrumPad.BlueDrum     => FiveLaneDrumPad.Blue,
                FourLaneDrumPad.GreenCymbal  => FiveLaneDrumPad.Orange,
                FourLaneDrumPad.GreenDrum    => FiveLaneDrumPad.Green,
                _ => throw new InvalidOperationException($"Invalid four lane drum pad {fourLanePad}!")
            };

            // Handle special cases
            if (pad is FiveLaneDrumPad.Blue or FiveLaneDrumPad.Orange)
            {
                var (start, end) = TrackHandler.GetEventChord(notes, index);
                for (int i = start; i < end; i++)
                {
                    if (i == index)
                        continue;

                    var otherPad = GetFourLaneFromFourLane(notes, i, pro: true);
                    pad = (pad, otherPad) switch
                    {
                        // (Calculated pad, other note in chord) => corrected pad to prevent same-color overlapping
                        (FiveLaneDrumPad.Blue, FourLaneDrumPad.BlueDrum) => FiveLaneDrumPad.Red,
                        (FiveLaneDrumPad.Orange, FourLaneDrumPad.GreenCymbal) => FiveLaneDrumPad.Yellow,
                        _ => pad
                    };
                }
            }

            return pad;
        }
    }
}