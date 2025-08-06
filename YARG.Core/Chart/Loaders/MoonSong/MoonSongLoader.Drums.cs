using System;
using System.Collections.Generic;
using System.Linq;
using MoonscraperChartEditor.Song;
using YARG.Core.Parsing;
using static YARG.Core.Chart.EliteDrumNote;

namespace YARG.Core.Chart
{
    internal partial class MoonSongLoader : ISongLoader
    {
        private bool _discoFlip = false;

        public InstrumentTrack<DrumNote> LoadDrumsTrack(Instrument instrument, InstrumentTrack<EliteDrumNote>? eliteDrumsFallback)
        {
            _discoFlip = false;
            return instrument.ToGameMode() switch
            {
                GameMode.FourLaneDrums => LoadDrumsTrack(instrument, CreateFourLaneDrumNote, eliteDrumsFallback),
                GameMode.FiveLaneDrums => LoadDrumsTrack(instrument, CreateFiveLaneDrumNote, eliteDrumsFallback),
                _ => throw new ArgumentException($"Instrument {instrument} is not a drums instrument!", nameof(instrument))
            };
        }

        private InstrumentTrack<DrumNote> LoadDrumsTrack(Instrument instrument, CreateNoteDelegate<DrumNote> createNote, InstrumentTrack<EliteDrumNote>? eliteDrumsFallback)
        {
            var difficulties = new Dictionary<Difficulty, InstrumentDifficulty<DrumNote>>()
            {
                { Difficulty.Easy, LoadDifficulty(instrument, Difficulty.Easy, createNote, HandleTextEvent) },
                { Difficulty.Medium, LoadDifficulty(instrument, Difficulty.Medium, createNote, HandleTextEvent) },
                { Difficulty.Hard, LoadDifficulty(instrument, Difficulty.Hard, createNote, HandleTextEvent) },
                { Difficulty.Expert, LoadDifficulty(instrument, Difficulty.Expert, createNote, HandleTextEvent) },
                { Difficulty.ExpertPlus, LoadDifficulty(instrument, Difficulty.ExpertPlus, createNote, HandleTextEvent) },
            };

            if (eliteDrumsFallback is not null && difficulties.All(keyval => keyval.Value.Notes.Count == 0))
            {
                return DownchartEliteDrumsTrack(instrument, eliteDrumsFallback);
            }

            return new(instrument, difficulties);
        }

        private InstrumentTrack<DrumNote> DownchartEliteDrumsTrack(Instrument instrument, InstrumentTrack<EliteDrumNote> eliteDrumsTrack)
        {
            var difficulties = new Dictionary<Difficulty, InstrumentDifficulty<DrumNote>>()
            {
                {  Difficulty.Easy, DownchartEliteDrumsDifficulty(instrument, eliteDrumsTrack, Difficulty.Easy) },
                {  Difficulty.Medium, DownchartEliteDrumsDifficulty(instrument, eliteDrumsTrack, Difficulty.Medium) },
                {  Difficulty.Hard, DownchartEliteDrumsDifficulty(instrument, eliteDrumsTrack, Difficulty.Hard) },
                {  Difficulty.Expert, DownchartEliteDrumsDifficulty(instrument, eliteDrumsTrack, Difficulty.Expert) },
                {  Difficulty.ExpertPlus, DownchartEliteDrumsDifficulty(instrument, eliteDrumsTrack, Difficulty.ExpertPlus) },
            };

            return new(instrument, difficulties);
        }

        private DrumNote CreateFourLaneDrumNote(MoonNote moonNote, Dictionary<MoonPhrase.Type, MoonPhrase> currentPhrases)
        {
            var pad = GetFourLaneDrumPad(moonNote);
            var noteType = GetDrumNoteType(moonNote);
            var generalFlags = GetGeneralFlags(moonNote, currentPhrases);
            var drumFlags = GetDrumNoteFlags(moonNote, currentPhrases);

            double time = _moonSong.TickToTime(moonNote.tick);
            return new DrumNote(pad, noteType, drumFlags, generalFlags, time, moonNote.tick);
        }

        private DrumNote CreateFiveLaneDrumNote(MoonNote moonNote, Dictionary<MoonPhrase.Type, MoonPhrase> currentPhrases)
        {
            var pad = GetFiveLaneDrumPad(moonNote);
            var noteType = GetDrumNoteType(moonNote);
            var generalFlags = GetGeneralFlags(moonNote, currentPhrases);
            var drumFlags = GetDrumNoteFlags(moonNote, currentPhrases);

            double time = _moonSong.TickToTime(moonNote.tick);
            return new DrumNote(pad, noteType, drumFlags, generalFlags, time, moonNote.tick);
        }

        private InstrumentDifficulty<DrumNote> DownchartEliteDrumsDifficulty(Instrument instrument, InstrumentTrack<EliteDrumNote> eliteDrumsTrack, Difficulty difficulty)
        {
            var eliteDrumsDifficulty = eliteDrumsTrack.GetDifficulty(difficulty);

            var phrases = eliteDrumsDifficulty.Phrases;
            List<TextEvent> text = new();

            List <(DrumNote? kick, DrumNote? firstHandGem, DrumNote? secondHandGem)> unresolvedNotes = new();

            foreach (var eliteDrumNote in eliteDrumsDifficulty.Notes)
            {
                var note = DownchartEliteDrumsChord(eliteDrumNote);
                if (note is not null)
                {
                    unresolvedNotes.Add(note.Value);
                }
            }

            var notes = ResolveDownchartCollisions(unresolvedNotes);

            return new(instrument, difficulty, notes, phrases, text);
        }

        private static (DrumNote? kick, DrumNote? firstHandGem, DrumNote? secondHandGem)? DownchartEliteDrumsChord(EliteDrumNote eliteDrumChord)
        {
            DrumNote? kick = null;
            DrumNote? firstHandGem = null;
            DrumNote? secondHandGem = null;

            foreach (var eliteDrumNote in eliteDrumChord.AllNotes)
            {
                var downchartedNotes = DownchartIndividualEliteDrumsNote(eliteDrumNote);
                foreach (var downchartedNote in downchartedNotes)
                {
                    if (downchartedNote.Pad == (int)FourLaneDrumPad.Kick)
                    {
                        kick = downchartedNote;
                    } else if (firstHandGem is null)
                    {
                        firstHandGem = downchartedNote;
                    } else if (secondHandGem is null)
                    {
                        secondHandGem = downchartedNote;
                    }
                }
            }

            if (kick is null && firstHandGem is null)
            {
                // Downcharted to nothing; must have been just an unforced Hat Pedal note
                return null;
            }

            return (kick, firstHandGem, secondHandGem);
        }

        // In most cases, returns 1 note. Unforced hat pedals return 0 notes, while flams return 2.
        private static List<DrumNote> DownchartIndividualEliteDrumsNote(EliteDrumNote eliteDrumNote)
        {
            List<DrumNote> notes = new();

            var pad = ((EliteDrumPad) eliteDrumNote.Pad) switch
            {
                EliteDrumPad.HatPedal => GetCymbalForChannelFlag(eliteDrumNote, null),
                EliteDrumPad.Kick => FourLaneDrumPad.Kick,
                EliteDrumPad.Snare => GetDrumForChannelFlag(eliteDrumNote, FourLaneDrumPad.RedDrum),
                EliteDrumPad.HiHat => GetCymbalForChannelFlag(eliteDrumNote, FourLaneDrumPad.YellowCymbal),
                EliteDrumPad.LeftCrash => GetCymbalForChannelFlag(eliteDrumNote, FourLaneDrumPad.BlueCymbal),
                EliteDrumPad.Tom1 => GetDrumForChannelFlag(eliteDrumNote, FourLaneDrumPad.YellowDrum),
                EliteDrumPad.Tom2 => GetDrumForChannelFlag(eliteDrumNote, FourLaneDrumPad.BlueDrum),
                EliteDrumPad.Tom3 => GetDrumForChannelFlag(eliteDrumNote, FourLaneDrumPad.GreenDrum),
                EliteDrumPad.Ride => GetCymbalForChannelFlag(eliteDrumNote, FourLaneDrumPad.BlueCymbal),
                EliteDrumPad.RightCrash => GetCymbalForChannelFlag(eliteDrumNote, FourLaneDrumPad.GreenCymbal),
                _ => throw new Exception("Unreachable.")
            };

            if (pad is not null)
            {
                notes.Add(new(pad.Value, eliteDrumNote, eliteDrumNote.Dynamics, eliteDrumNote.DrumFlags, eliteDrumNote.Flags, eliteDrumNote.Time, eliteDrumNote.Tick));

                if (eliteDrumNote.IsFlam)
                {
                    FourLaneDrumPad? otherPad = pad.Value switch
                    {
                        FourLaneDrumPad.Kick => null,
                        FourLaneDrumPad.RedDrum => FourLaneDrumPad.YellowDrum,
                        FourLaneDrumPad.YellowDrum => FourLaneDrumPad.BlueDrum,
                        FourLaneDrumPad.BlueDrum => FourLaneDrumPad.GreenDrum,
                        FourLaneDrumPad.GreenDrum => FourLaneDrumPad.BlueDrum,
                        FourLaneDrumPad.YellowCymbal => FourLaneDrumPad.BlueCymbal,
                        FourLaneDrumPad.BlueCymbal => FourLaneDrumPad.GreenCymbal,
                        FourLaneDrumPad.GreenCymbal => FourLaneDrumPad.BlueCymbal,
                        _ => throw new Exception("Unreachable.")
                    };

                    if (otherPad is not null)
                    {
                        notes.Add(new(otherPad.Value, eliteDrumNote, eliteDrumNote.Dynamics, eliteDrumNote.DrumFlags, eliteDrumNote.Flags, eliteDrumNote.Time, eliteDrumNote.Tick));
                    }
                }
            }

            return notes;
        }

        private List<DrumNote> ResolveDownchartCollisions(List<(DrumNote? kick, DrumNote? firstHandGem, DrumNote? secondHandGem)> unresolvedNotes)
        {
            List<DrumNote> notes = new();

            foreach (var unresolvedNote in unresolvedNotes)
            {
                notes.Add(ResolveDownchartCollision(unresolvedNote.kick, unresolvedNote.firstHandGem, unresolvedNote.secondHandGem));
            }

            return notes;
        }

        private DrumNote ResolveDownchartCollision(DrumNote? kick, DrumNote? firstHandGem, DrumNote? secondHandGem)
        {
            DrumNote note;

            if (secondHandGem is null)
            {
                // No collisions are possible without a second hand gem, so return early
                if (kick is not null)
                {
                    note = kick;
                    if (firstHandGem is not null)
                    {
                        note.AddChildNote(firstHandGem);
                    }
                    return note;
                }

                if (firstHandGem is not null)
                {
                    return firstHandGem;
                }

                throw new Exception("Unreachable.");
            }

            var firstHandGemColor = DrumNote._fourLanePadToColor[(FourLaneDrumPad)firstHandGem.Pad];
            var secondHandGemColor = DrumNote._fourLanePadToColor[(FourLaneDrumPad) secondHandGem.Pad];

            if (firstHandGemColor != secondHandGemColor) {
                // Two hand gems, but no collision

                // Special case: Unforced LCrash + Unforced RCrash resolves to YG instead of BG
                if (firstHandGem.DownchartingSource!.ChannelFlag is EliteDrumsChannelFlag.None && secondHandGem.DownchartingSource!.ChannelFlag is EliteDrumsChannelFlag.None)
                {
                    if (firstHandGem.DownchartingSource.Pad is (int)EliteDrumPad.LeftCrash && secondHandGem.DownchartingSource.Pad is (int)EliteDrumPad.RightCrash)
                    {
                        firstHandGem = new(FourLaneDrumPad.YellowCymbal, firstHandGem.Type, firstHandGem.DrumFlags, firstHandGem.Flags, firstHandGem.Time, firstHandGem.Tick);
                    } else if (firstHandGem.DownchartingSource.Pad is (int) EliteDrumPad.RightCrash && secondHandGem.DownchartingSource.Pad is (int) EliteDrumPad.LeftCrash)
                    {
                        secondHandGem = new(FourLaneDrumPad.YellowCymbal, secondHandGem.Type, secondHandGem.DrumFlags, secondHandGem.Flags, secondHandGem.Time, secondHandGem.Tick);
                    }
                }

                note = firstHandGem;
                note.AddChildNote(secondHandGem);
                if (kick is not null)
                {
                    note.AddChildNote(kick);
                }

                return note;
            }

            // Two hand gems with equal colors - collision!

            // For tom/cymbal collisions, the tom goes on the left. For tom/tom and cym/cym collisions, preserve the
            // handedness of the dynamics from the original Elite Drums chord
            FourLaneDrumPad newLeftPad;
            FourLaneDrumPad newRightPad;

            var firstHandGemIsCymbal = (FourLaneDrumPad) firstHandGem.Pad is FourLaneDrumPad.YellowCymbal or FourLaneDrumPad.BlueCymbal or FourLaneDrumPad.GreenCymbal;
            var secondHandGemIsCymbal = (FourLaneDrumPad) secondHandGem.Pad is FourLaneDrumPad.YellowCymbal or FourLaneDrumPad.BlueCymbal or FourLaneDrumPad.GreenCymbal;

            if (firstHandGemIsCymbal == secondHandGemIsCymbal)
            {
                // This is a tom/tom or cym/cym collision, so we'll need to preserve the handedness of the dynamics
                // from the original Elite Drums chord
                (var leftHandGem, var rightHandGem) = firstHandGem.DownchartingSource!.Pad < secondHandGem.DownchartingSource!.Pad ? (firstHandGem, secondHandGem) : (secondHandGem, firstHandGem);

                if (firstHandGemIsCymbal)
                {
                    /* Cymbal-Cymbal Collision Resolutions
                     * Y -> YB
                     * B -> BG
                     * G -> BG
                     */
                    switch ((FourLaneDrumPad)firstHandGem.Pad)
                    {
                        case FourLaneDrumPad.YellowCymbal:
                            newLeftPad = FourLaneDrumPad.YellowCymbal;
                            newRightPad = FourLaneDrumPad.BlueCymbal;
                            break;
                        case FourLaneDrumPad.BlueCymbal or FourLaneDrumPad.GreenCymbal:
                            newLeftPad = FourLaneDrumPad.BlueCymbal;
                            newRightPad = FourLaneDrumPad.GreenCymbal;
                            break;
                        default:
                            throw new Exception("Unreachable.");
                    }
                } else
                {
                    /* Tom-Tom Collision Resolutions
                     * R -> RY
                     * Y -> YB
                     * B -> BG
                     * G -> BG
                     */
                    switch ((FourLaneDrumPad) firstHandGem.Pad)
                    {
                        case FourLaneDrumPad.RedDrum:
                            newLeftPad = FourLaneDrumPad.RedDrum;
                            newRightPad = FourLaneDrumPad.YellowDrum;
                            break;
                        case FourLaneDrumPad.YellowDrum:
                            newLeftPad = FourLaneDrumPad.YellowDrum;
                            newRightPad = FourLaneDrumPad.BlueDrum;
                            break;
                        case FourLaneDrumPad.BlueDrum or FourLaneDrumPad.GreenDrum:
                            newLeftPad = FourLaneDrumPad.BlueDrum;
                            newRightPad = FourLaneDrumPad.GreenDrum;
                            break;
                        default:
                            throw new Exception("Unreachable.");
                    }
                }
                firstHandGem = new(newLeftPad, leftHandGem.Type, leftHandGem.DrumFlags, leftHandGem.Flags, leftHandGem.Time, leftHandGem.Tick);
                secondHandGem = new(newRightPad, rightHandGem.Type, rightHandGem.DrumFlags, rightHandGem.Flags, rightHandGem.Time, rightHandGem.Tick);
            } else
            {
                // This is a tom/cym collision (or vice-versa)
                // Nudge the tom to the left
                (var cym, var tom) = firstHandGemIsCymbal ? (firstHandGem, secondHandGem) : (secondHandGem, firstHandGem);

                switch ((FourLaneDrumPad) cym.Pad)
                {
                    case FourLaneDrumPad.YellowCymbal:
                        newLeftPad = FourLaneDrumPad.RedDrum;
                        newRightPad = FourLaneDrumPad.YellowCymbal;
                        break;
                    case FourLaneDrumPad.BlueCymbal:
                        newLeftPad = FourLaneDrumPad.YellowDrum;
                        newRightPad = FourLaneDrumPad.BlueCymbal;
                        break;
                    case FourLaneDrumPad.GreenCymbal:
                        newLeftPad = FourLaneDrumPad.BlueDrum;
                        newRightPad = FourLaneDrumPad.GreenCymbal;
                        break;
                    default:
                        throw new Exception("Unreachable.");
                }

                firstHandGem = new(newLeftPad, tom.Type, tom.DrumFlags, tom.Flags, tom.Time, tom.Tick);
                secondHandGem = new(newRightPad, cym.Type, cym.DrumFlags, cym.Flags, cym.Time, cym.Tick);
            }

            firstHandGem.AddChildNote(secondHandGem);
            if (kick is not null)
            {
                firstHandGem.AddChildNote(new(FourLaneDrumPad.Kick, kick.Type, kick.DrumFlags, kick.Flags, kick.Time, kick.Tick));
            }

            return firstHandGem;
        }

        private static FourLaneDrumPad GetDrumForChannelFlag(EliteDrumNote drum, FourLaneDrumPad unforced)
        {
            return drum.ChannelFlag switch
            {
                EliteDrumsChannelFlag.Red => FourLaneDrumPad.RedDrum,
                EliteDrumsChannelFlag.Yellow => FourLaneDrumPad.YellowDrum,
                EliteDrumsChannelFlag.Blue => FourLaneDrumPad.BlueDrum,
                EliteDrumsChannelFlag.Green => FourLaneDrumPad.GreenDrum,
                _ => unforced
            };
        }

        private static FourLaneDrumPad? GetCymbalForChannelFlag(EliteDrumNote cymbal, FourLaneDrumPad? unforced)
        {
            return cymbal.ChannelFlag switch
            {
                EliteDrumsChannelFlag.Yellow => FourLaneDrumPad.YellowCymbal,
                EliteDrumsChannelFlag.Blue => FourLaneDrumPad.BlueCymbal,
                EliteDrumsChannelFlag.Green => FourLaneDrumPad.GreenCymbal,
                _ => unforced
            };
        }
        private void HandleTextEvent(MoonText text)
        {
            // Ignore on 5-lane or standard Drums
            if (_settings.DrumsType != DrumsType.FourLane && _currentInstrument is Instrument.FourLaneDrums)
                return;

            // Parse out event data
            if (!TextEvents.TryParseDrumsMixEvent(text.text, out var difficulty, out var config, out var setting))
                return;

            // Ignore if event is not for the given difficulty
            var currentDiff = _currentDifficulty;
            if (currentDiff == Difficulty.ExpertPlus)
                currentDiff = Difficulty.Expert;
            if (difficulty != currentDiff)
                return;

            _discoFlip = setting == DrumsMixSetting.DiscoFlip;
        }

        private FourLaneDrumPad GetFourLaneDrumPad(MoonNote moonNote)
        {
            var pad = _settings.DrumsType switch
            {
                DrumsType.FourLane => MoonNoteToFourLane(moonNote),
                DrumsType.FiveLane => GetFourLaneFromFiveLane(moonNote),
                _ => throw new InvalidOperationException($"Unexpected drums type {_settings.DrumsType}! (Drums type should have been calculated by now)")
            };

            return pad;
        }

        private FourLaneDrumPad GetFourLaneFromFiveLane(MoonNote moonNote)
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

            // Down-convert to standard 4-lane
            if (_currentInstrument is Instrument.FourLaneDrums)
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

        private FiveLaneDrumPad GetFiveLaneDrumPad(MoonNote moonNote)
        {
            return _settings.DrumsType switch
            {
                DrumsType.FiveLane => MoonNoteToFiveLane(moonNote),
                DrumsType.FourLane => GetFiveLaneFromFourLane(moonNote),
                _ => throw new InvalidOperationException($"Unexpected drums type {_settings.DrumsType}! (Drums type should have been calculated by now)")
            };
        }

        private FiveLaneDrumPad GetFiveLaneFromFourLane(MoonNote moonNote)
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

        private FourLaneDrumPad MoonNoteToFourLane(MoonNote moonNote)
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

            if (_currentInstrument is not Instrument.FourLaneDrums)
            {
                var flags = moonNote.flags;

                // Disco flip
                if (_discoFlip)
                {
                    if (pad == FourLaneDrumPad.RedDrum)
                    {
                        // Red drums in disco flip are turned into yellow cymbals
                        pad = FourLaneDrumPad.YellowDrum;
                        flags |= MoonNote.Flags.ProDrums_Cymbal;
                    }
                    else if (pad == FourLaneDrumPad.YellowDrum)
                    {
                        // Both yellow cymbals and yellow drums are turned into red drums in disco flip
                        pad = FourLaneDrumPad.RedDrum;
                        flags &= ~MoonNote.Flags.ProDrums_Cymbal;
                    }
                }

                // Cymbal marking
                if ((flags & MoonNote.Flags.ProDrums_Cymbal) != 0)
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

        private FiveLaneDrumPad MoonNoteToFiveLane(MoonNote moonNote)
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

        private DrumNoteFlags GetDrumNoteFlags(MoonNote moonNote, Dictionary<MoonPhrase.Type, MoonPhrase> currentPhrases)
        {
            var flags = DrumNoteFlags.None;

            // SP activator
            if (currentPhrases.TryGetValue(MoonPhrase.Type.ProDrums_Activation, out var activationPhrase) &&
                IsNoteClosestToEndOfPhrase(_moonSong, moonNote, activationPhrase))
            {
                flags |= DrumNoteFlags.StarPowerActivator;
            }

            return flags;
        }
    }
}