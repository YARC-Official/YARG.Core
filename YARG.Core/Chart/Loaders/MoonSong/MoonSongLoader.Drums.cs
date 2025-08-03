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

            List<DrumNote> notes = new();
            List<Phrase> phrases = new();
            List<TextEvent> text = new();

            foreach (var eliteDrumNote in eliteDrumsDifficulty.Notes)
            {
                var note = DownchartEliteDrumsChord(eliteDrumNote);
                if (note is not null)
                {
                    notes.Add(note);
                }
            }

            return new(instrument, difficulty, notes, phrases, text);
        }

        private static DrumNote? DownchartEliteDrumsChord(EliteDrumNote eliteDrumChord)
        {
            DrumNote? parent = null;

            // Once we've created two hand gems, ignore all others
            var handGemCount = 0;
            List<DrumNote?> noteQueue = new();

            var (parentCandidate, parentFlamPartner) = DownchartIndividualEliteDrumsNote(eliteDrumChord);
            noteQueue.Add(parentCandidate);
            noteQueue.Add(parentFlamPartner);

            foreach (var eliteDrumChild in eliteDrumChord.ChildNotes)
            {
                var (child, childFlamPartner) = DownchartIndividualEliteDrumsNote(eliteDrumChild);
                noteQueue.Add(child);
                noteQueue.Add(childFlamPartner);
            }

            foreach (var note in noteQueue)
            {
                if (note is not null)
                {
                    if (parent is null)
                    {
                        parent = note;
                        if (note.Pad is not (int)FourLaneDrumPad.Kick)
                        {
                            handGemCount++;
                        }
                    } else
                    {
                        if (note.Pad is (int) FourLaneDrumPad.Kick)
                        {
                            parent.AddChildNote(note);
                        } else if (handGemCount < 2)
                        {
                            AddChildHandGem(parent, note);
                            handGemCount++;
                        }
                    }
                }
            }

            return parent;
        }

        // Usually returns 1 note. Can return 0 notes for hat pedal gems that don't have channel flags, or 2 for flams
        private static (DrumNote? downchartedNote, DrumNote? flamPartner) DownchartIndividualEliteDrumsNote(EliteDrumNote eliteDrumNote)
        {
            DrumNote? downchartedNote = null;
            DrumNote? flamPartner = null;

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
                _ => throw new Exception("Unreachable")
            };

            if (pad is not null)
            {
                downchartedNote = new(pad.Value, (EliteDrumPad) eliteDrumNote.Pad, eliteDrumNote.Dynamics, eliteDrumNote.DrumFlags, eliteDrumNote.Flags, eliteDrumNote.Time, eliteDrumNote.Tick);

                if (eliteDrumNote.IsFlam)
                {
                    var otherPad = pad.Value switch
                    {
                        FourLaneDrumPad.RedDrum => FourLaneDrumPad.YellowDrum,
                        FourLaneDrumPad.YellowDrum => FourLaneDrumPad.BlueDrum,
                        FourLaneDrumPad.BlueDrum => FourLaneDrumPad.GreenDrum,
                        FourLaneDrumPad.GreenDrum => FourLaneDrumPad.BlueDrum,
                        FourLaneDrumPad.YellowCymbal => FourLaneDrumPad.BlueCymbal,
                        FourLaneDrumPad.BlueCymbal => FourLaneDrumPad.GreenCymbal,
                        FourLaneDrumPad.GreenCymbal => FourLaneDrumPad.BlueCymbal,
                        _ => throw new Exception("Unreachable.")
                    };

                    flamPartner = new(otherPad, (EliteDrumPad) eliteDrumNote.Pad, eliteDrumNote.Dynamics, eliteDrumNote.DrumFlags, eliteDrumNote.Flags, eliteDrumNote.Time, eliteDrumNote.Tick);
                }
            }

            return (downchartedNote, flamPartner);
        }

        private static void AddChildHandGem(DrumNote parent, DrumNote child)
        {
            var preexistingCollider = FindCollidingHandGem(parent, DrumNote._fourLanePadToColor[(FourLaneDrumPad) child.Pad]);
            if (preexistingCollider is null)
            {
                parent.AddChildNote(child);
            }
            else
            {
                var childIsCymbal = (FourLaneDrumPad) child.Pad is FourLaneDrumPad.YellowCymbal or FourLaneDrumPad.BlueCymbal or FourLaneDrumPad.GreenCymbal;
                var collidingGemIsCymbal = (FourLaneDrumPad) preexistingCollider.Pad is FourLaneDrumPad.YellowCymbal or FourLaneDrumPad.BlueCymbal or FourLaneDrumPad.GreenCymbal;

                if (childIsCymbal && collidingGemIsCymbal) // Cymbal colliding with cymbal
                {
                    (var leftmost, var rightmost) = preexistingCollider.DownchartingSourcePad < child.DownchartingSourcePad ? (preexistingCollider, child) : (child, preexistingCollider);
                    switch ((FourLaneDrumPad) child.Pad)
                    {
                        case FourLaneDrumPad.YellowCymbal:
                            rightmost.Pad = (int) FourLaneDrumPad.BlueCymbal;
                            break;
                        case FourLaneDrumPad.BlueCymbal:
                            rightmost.Pad = (int) FourLaneDrumPad.GreenCymbal;
                            break;
                        case FourLaneDrumPad.GreenCymbal:
                            leftmost.Pad = (int) FourLaneDrumPad.BlueCymbal;
                            break;
                    }
                }
                else if (childIsCymbal != collidingGemIsCymbal) // Tom colliding with cymbal or vice-versa
                {
                    (var tom, var cymbal) = childIsCymbal ? (preexistingCollider, child) : (child, preexistingCollider);
                    switch ((FourLaneDrumPad) cymbal.Pad)
                    {
                        case FourLaneDrumPad.YellowCymbal:
                            tom.Pad = (int) FourLaneDrumPad.RedDrum;
                            break;
                        case FourLaneDrumPad.BlueCymbal:
                            tom.Pad = (int) FourLaneDrumPad.YellowDrum;
                            break;
                        case FourLaneDrumPad.GreenCymbal:
                            tom.Pad = (int) FourLaneDrumPad.BlueDrum;
                            break;
                    }
                }
                else // Tom colliding with tom
                {
                    (var leftmost, var rightmost) = preexistingCollider.DownchartingSourcePad < child.DownchartingSourcePad ? (preexistingCollider, child) : (child, preexistingCollider);
                    switch ((FourLaneDrumPad) child.Pad)
                    {
                        case FourLaneDrumPad.RedDrum:
                            rightmost.Pad = (int) FourLaneDrumPad.YellowDrum;
                            break;
                        case FourLaneDrumPad.YellowDrum:
                            rightmost.Pad = (int) FourLaneDrumPad.BlueDrum;
                            break;
                        case FourLaneDrumPad.BlueDrum:
                            rightmost.Pad = (int) FourLaneDrumPad.GreenDrum;
                            break;
                        case FourLaneDrumPad.GreenDrum:
                            leftmost.Pad = (int) FourLaneDrumPad.BlueDrum;
                            break;
                    }
                }
                parent.AddChildNote(child);
            }
        }

        private static DrumNote? FindCollidingHandGem(DrumNote parent, FourLaneDrumHandGemColor color)
        {

            if ((FourLaneDrumPad) parent.Pad != FourLaneDrumPad.Kick && DrumNote._fourLanePadToColor[(FourLaneDrumPad)parent.Pad] == color)
            {
                return parent;
            }

            foreach (var child in parent.ChildNotes)
            {
                if (DrumNote._fourLanePadToColor[(FourLaneDrumPad) child.Pad] == color)
                {
                    return child;
                }
            }

            return null;
        }

        private static FourLaneDrumPad GetDrumForChannelFlag(EliteDrumNote drum, FourLaneDrumPad unforced)
        {
            return drum.ChannelFlag switch
            {
                EliteDrumNote.EliteDrumsChannelFlag.Red => FourLaneDrumPad.RedDrum,
                EliteDrumNote.EliteDrumsChannelFlag.Yellow => FourLaneDrumPad.YellowDrum,
                EliteDrumNote.EliteDrumsChannelFlag.Blue => FourLaneDrumPad.BlueDrum,
                EliteDrumNote.EliteDrumsChannelFlag.Green => FourLaneDrumPad.GreenDrum,
                _ => unforced
            };
        }

        private static FourLaneDrumPad? GetCymbalForChannelFlag(EliteDrumNote cymbal, FourLaneDrumPad? unforced)
        {
            return cymbal.ChannelFlag switch
            {
                EliteDrumNote.EliteDrumsChannelFlag.Yellow => FourLaneDrumPad.YellowCymbal,
                EliteDrumNote.EliteDrumsChannelFlag.Blue => FourLaneDrumPad.BlueCymbal,
                EliteDrumNote.EliteDrumsChannelFlag.Green => FourLaneDrumPad.GreenCymbal,
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