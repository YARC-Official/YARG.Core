using System;
using System.Collections.Generic;
using MoonscraperChartEditor.Song;

// TODO: Disco flip

namespace YARG.Core.Chart
{
    internal partial class MoonSongLoader : ISongLoader
    {
        public InstrumentTrack<DrumNote> LoadDrumsTrack(Instrument instrument)
        {
            return instrument.ToGameMode() switch
            {
                GameMode.FourLaneDrums => LoadDrumsTrack(instrument, (note, phrases) => CreateFourLaneDrumNote(instrument, note, phrases)),
                GameMode.FiveLaneDrums => LoadDrumsTrack(instrument, CreateFiveLaneDrumNote),
                _ => throw new ArgumentException($"Instrument {instrument} is not a drums instrument!", nameof(instrument))
            };
        }

        private InstrumentTrack<DrumNote> LoadDrumsTrack(Instrument instrument, CreateNoteDelegate<DrumNote> createNote)
        {
            var difficulties = new Dictionary<Difficulty, InstrumentDifficulty<DrumNote>>()
            {
                { Difficulty.Easy, LoadDifficulty(instrument, Difficulty.Easy, createNote) },
                { Difficulty.Medium, LoadDifficulty(instrument, Difficulty.Medium, createNote) },
                { Difficulty.Hard, LoadDifficulty(instrument, Difficulty.Hard, createNote) },
                { Difficulty.Expert, LoadDifficulty(instrument, Difficulty.Expert, createNote) },
                { Difficulty.ExpertPlus, LoadDifficulty(instrument, Difficulty.ExpertPlus, createNote) },
            };
            return new(instrument, difficulties);
        }

        private DrumNote CreateFourLaneDrumNote(Instrument instrument, MoonNote moonNote,
            Dictionary<SpecialPhrase.Type, SpecialPhrase> currentPhrases)
        {
            var pad = GetFourLaneDrumPad(instrument, moonNote);
            var noteType = GetDrumNoteType(moonNote);
            var generalFlags = GetGeneralFlags(moonNote, currentPhrases);
            var drumFlags = GetDrumNoteFlags(moonNote, currentPhrases);

            return new DrumNote(pad, noteType, drumFlags, generalFlags,
                moonNote.time, moonNote.tick);
        }

        private DrumNote CreateFiveLaneDrumNote(MoonNote moonNote, Dictionary<SpecialPhrase.Type, SpecialPhrase> currentPhrases)
        {
            var pad = GetFiveLaneDrumPad(moonNote);
            var noteType = GetDrumNoteType(moonNote);
            var generalFlags = GetGeneralFlags(moonNote, currentPhrases);
            var drumFlags = GetDrumNoteFlags(moonNote, currentPhrases);

            return new DrumNote(pad, noteType, drumFlags, generalFlags,
                moonNote.time, moonNote.tick);
        }

        private FourLaneDrumPad GetFourLaneDrumPad(Instrument instrument, MoonNote moonNote)
        {
            var pad = _settings.DrumsType switch
            {
                DrumsType.FourLane => MoonNoteToFourLane(moonNote),
                DrumsType.FiveLane => FromFiveLane(moonNote),
                _ => throw new InvalidOperationException($"Unexpected drums type {_settings.DrumsType}! (Drums type should have been calculated by now)")
            };

            // Un-mark cymbals on standard
            if (instrument != Instrument.ProDrums)
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

            static FourLaneDrumPad FromFiveLane(MoonNote moonNote)
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

                var fiveLanePad = MoonNoteToFiveLane(moonNote);
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
                    foreach (var note in moonNote.chord)
                    {
                        if (note == moonNote)
                            continue;

                        var otherPad = MoonNoteToFiveLane(note);
                        pad = (pad, otherPad) switch
                        {
                            // (Calculated pad, other note in chord) => corrected pad to prevent same-color overlapping
                            (FourLaneDrumPad.GreenCymbal, FiveLaneDrumPad.Green) => FourLaneDrumPad.BlueCymbal,
                            _ => pad
                        };
                    }
                }

                return pad;
            }
        }

        private FiveLaneDrumPad GetFiveLaneDrumPad(MoonNote moonNote)
        {
            return _settings.DrumsType switch
            {
                DrumsType.FiveLane => MoonNoteToFiveLane(moonNote),
                DrumsType.FourLane => FromFourLane(moonNote),
                _ => throw new InvalidOperationException($"Unexpected drums type {_settings.DrumsType}! (Drums type should have been calculated by now)")
            };

            static FiveLaneDrumPad FromFourLane(MoonNote moonNote)
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

                var fourLanePad = MoonNoteToFourLane(moonNote);
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
                    foreach (var note in moonNote.chord)
                    {
                        if (note == moonNote)
                            continue;

                        var otherPad = MoonNoteToFourLane(note);
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

        private static FourLaneDrumPad MoonNoteToFourLane(MoonNote moonNote)
        {
            var pad = moonNote.drumPad switch
            {
                MoonNote.DrumPad.Kick   => FourLaneDrumPad.Kick,
                MoonNote.DrumPad.Red    => FourLaneDrumPad.RedDrum,
                MoonNote.DrumPad.Yellow => FourLaneDrumPad.YellowDrum,
                MoonNote.DrumPad.Blue   => FourLaneDrumPad.BlueDrum,
                MoonNote.DrumPad.Orange => FourLaneDrumPad.GreenDrum,
                MoonNote.DrumPad.Green  => FourLaneDrumPad.GreenDrum,
                _ => throw new ArgumentException($"Invalid Moonscraper drum pad {moonNote.drumPad}!", nameof(moonNote))
            };

            // Cymbal marking
            if ((moonNote.flags & MoonNote.Flags.ProDrums_Cymbal) != 0)
            {
                pad = pad switch
                {
                    FourLaneDrumPad.YellowDrum => FourLaneDrumPad.YellowCymbal,
                    FourLaneDrumPad.BlueDrum   => FourLaneDrumPad.BlueCymbal,
                    FourLaneDrumPad.GreenDrum  => FourLaneDrumPad.GreenCymbal,
                    _ => throw new InvalidOperationException($"Cannot mark pad {pad} as a cymbal!")
                };
            }

            return pad;
        }

        private static FiveLaneDrumPad MoonNoteToFiveLane(MoonNote moonNote)
        {
            var pad = moonNote.drumPad switch
            {
                MoonNote.DrumPad.Kick   => FiveLaneDrumPad.Kick,
                MoonNote.DrumPad.Red    => FiveLaneDrumPad.Red,
                MoonNote.DrumPad.Yellow => FiveLaneDrumPad.Yellow,
                MoonNote.DrumPad.Blue   => FiveLaneDrumPad.Blue,
                MoonNote.DrumPad.Orange => FiveLaneDrumPad.Orange,
                MoonNote.DrumPad.Green  => FiveLaneDrumPad.Green,
                _ => throw new ArgumentException($"Invalid Moonscraper drum pad {moonNote.drumPad}!", nameof(moonNote))
            };

            return pad;
        }

        private DrumNoteType GetDrumNoteType(MoonNote moonNote)
        {
            var noteType = DrumNoteType.Neutral;

            // Accents/ghosts
            if ((moonNote.flags & MoonNote.Flags.ProDrums_Accent) != 0)
                noteType = DrumNoteType.Accent;
            else if ((moonNote.flags & MoonNote.Flags.ProDrums_Ghost) != 0)
                noteType = DrumNoteType.Ghost;

            return noteType;
        }

        private DrumNoteFlags GetDrumNoteFlags(MoonNote moonNote, Dictionary<SpecialPhrase.Type, SpecialPhrase> currentPhrases)
        {
            var flags = DrumNoteFlags.None;

            // SP activator
            if (currentPhrases.TryGetValue(SpecialPhrase.Type.ProDrums_Activation, out var activationPhrase) &&
                IsNoteClosestToEndOfPhrase(moonNote, activationPhrase))
            {
                flags |= DrumNoteFlags.StarPowerActivator;
            }

            return flags;
        }
    }
}