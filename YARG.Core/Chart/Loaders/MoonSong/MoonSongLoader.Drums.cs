using System;
using System.Collections.Generic;
using MoonscraperChartEditor.Song;
using MoonscraperChartEditor.Song.IO;
using YARG.Core.Extensions;
using YARG.Core.Logging;
using YARG.Core.Parsing;
using static MoonscraperChartEditor.Song.MoonNote;

namespace YARG.Core.Chart
{
    internal partial class MoonSongLoader : ISongLoader
    {
        private bool _discoFlip = false;
        private DrumsMixConfiguration _mixConfig = DrumsMixConfiguration.StereoKickSnareKit;

        public InstrumentTrack<DrumNote> LoadDrumsTrack(Instrument instrument, InstrumentTrack<EliteDrumNote>? eliteDrumsFallback)
        {
            _discoFlip = false;
            // by default, assume that all stems are supported, since we gracefully degrade if the stems are missing
            _mixConfig = DrumsMixConfiguration.StereoKickSnareKit;
            return instrument.ToNativeGameMode() switch
            {
                GameMode.FourLaneDrums => LoadDrumsTrack(instrument, CreateFourLaneDrumNote, eliteDrumsFallback),
                GameMode.FiveLaneDrums => LoadDrumsTrack(instrument, CreateFiveLaneDrumNote, eliteDrumsFallback),
                _ => throw new ArgumentException($"Instrument {instrument} is not a drums instrument!", nameof(instrument))
            };
        }

        private InstrumentTrack<DrumNote> LoadDrumsTrack(Instrument instrument, CreateNoteDelegate<DrumNote> createNote, InstrumentTrack<EliteDrumNote>? eliteDrumsFallback)
        {
            CreateNoteDelegate<DrumNote> beginnerNoteDelegate = instrument is Instrument.FourLaneDrums
                ? CreateFourLaneDrumBeginnerNote
                : CreateFiveLaneDrumBeginnerNote;

            var difficulties = new Dictionary<Difficulty, InstrumentDifficulty<DrumNote>>()
            {
                { Difficulty.Beginner, LoadDifficulty(instrument, Difficulty.Beginner, beginnerNoteDelegate, HandleTextEvent, finalPassDelegate: DrumsFinalPass) },
                { Difficulty.Easy, LoadDifficulty(instrument, Difficulty.Easy, createNote, HandleTextEvent, finalPassDelegate: DrumsFinalPass) },
                { Difficulty.Medium, LoadDifficulty(instrument, Difficulty.Medium, createNote, HandleTextEvent, finalPassDelegate: DrumsFinalPass) },
                { Difficulty.Hard, LoadDifficulty(instrument, Difficulty.Hard, createNote, HandleTextEvent, finalPassDelegate: DrumsFinalPass) },
                { Difficulty.Expert, LoadDifficulty(instrument, Difficulty.Expert, createNote, HandleTextEvent, finalPassDelegate: DrumsFinalPass) },
                { Difficulty.ExpertPlus, LoadDifficulty(instrument, Difficulty.ExpertPlus, createNote, HandleTextEvent, finalPassDelegate: DrumsFinalPass) },
            };

            foreach (var difficulty in difficulties)
            {
                if (difficulty.Value.Notes.Count > 0)
                {
                    // If at least one difficulty has at least one note, return the native chart
                    return new(instrument, difficulties, GetAnimationTrack(instrument));
                }
            }

            // No native chart. Do we have an Elite Drums chart to fall back on?
            if (eliteDrumsFallback is not null)
            {
                // Generate downcharts if we haven't already
                _downCharts ??= DownchartEliteDrumsTrack(eliteDrumsFallback);

                if (_downCharts is not null)
                {
                    _settings.DrumsType = DrumsType.FourLane;

                    difficulties = new Dictionary<Difficulty, InstrumentDifficulty<DrumNote>>()
                    {
                        { Difficulty.Beginner, LoadFromEliteDrumsDownchartDifficulty(instrument, Difficulty.Easy, beginnerNoteDelegate, HandleTextEvent) },
                        { Difficulty.Easy, LoadFromEliteDrumsDownchartDifficulty(instrument, Difficulty.Easy, createNote, HandleTextEvent)},
                        { Difficulty.Medium, LoadFromEliteDrumsDownchartDifficulty(instrument, Difficulty.Medium, createNote, HandleTextEvent)},
                        { Difficulty.Hard, LoadFromEliteDrumsDownchartDifficulty(instrument, Difficulty.Hard, createNote, HandleTextEvent)},
                        { Difficulty.Expert, LoadFromEliteDrumsDownchartDifficulty(instrument, Difficulty.Expert, createNote, HandleTextEvent)},
                        { Difficulty.ExpertPlus, LoadFromEliteDrumsDownchartDifficulty(instrument, Difficulty.ExpertPlus, createNote, HandleTextEvent)},
                    };
                }
            }

            return new(instrument, difficulties, GetAnimationTrack(instrument));
        }

        // "Hand chords" are ineligible for drum trills, but chords consisting of one hand gem and a kick are eligible
        private static bool IsEligibleForDrumTrill(MoonNote moonNote)
        {
            var numberOfHandGems = 0;
            var currentNote = moonNote;

            while (currentNote is not null && currentNote.tick == moonNote.tick)
            {
                if (currentNote.drumPad is not DrumPad.Kick)
                {
                    numberOfHandGems++;
                    if (numberOfHandGems > 1)
                    {
                        return false;
                    }
                }
                currentNote = currentNote.previous;
            }

            currentNote = moonNote.next;

            while (currentNote is not null && currentNote.tick == moonNote.tick)
            {
                if (currentNote.drumPad is not DrumPad.Kick)
                {
                    numberOfHandGems++;
                    if (numberOfHandGems > 1)
                    {
                        return false;
                    }
                }
                currentNote = currentNote.next;
            }

            return true;
        }

        private DrumNote CreateFourLaneDrumNote(MoonNote moonNote, Dictionary<MoonPhrase.Type, MoonPhrase> currentPhrases, List<DrumNote> notes)
        {
            var pad = GetFourLaneDrumPad(moonNote);
            var noteType = GetDrumNoteType(moonNote);

            var generalFlags = GetGeneralFlags(moonNote, currentPhrases, IsEligibleForDrumTrill);

            var drumFlags = GetDrumNoteFlags(moonNote, currentPhrases);

            double time = _moonSong.TickToTime(moonNote.tick);

            bool isDoubleKick = pad is FourLaneDrumPad.Kick && ((moonNote.flags & Flags.InstrumentPlus) != 0);

            return new DrumNote(pad, noteType, drumFlags, generalFlags, time, moonNote.tick, isDoubleKick, GetStem(pad));
        }

        private DrumNote CreateFiveLaneDrumNote(MoonNote moonNote, Dictionary<MoonPhrase.Type, MoonPhrase> currentPhrases, List<DrumNote> notes)
        {
            var pad = GetFiveLaneDrumPad(moonNote);
            var noteType = GetDrumNoteType(moonNote);

            var generalFlags = GetGeneralFlags(moonNote, currentPhrases, IsEligibleForDrumTrill);

            var drumFlags = GetDrumNoteFlags(moonNote, currentPhrases);

            double time = _moonSong.TickToTime(moonNote.tick);

            bool isDoubleKick = pad is FiveLaneDrumPad.Kick && ((moonNote.flags & Flags.InstrumentPlus) != 0);

            return new DrumNote(pad, noteType, drumFlags, generalFlags, time, moonNote.tick, isDoubleKick, GetStem(pad));
        }

        private DrumNote CreateFourLaneDrumBeginnerNote(MoonNote moonNote, Dictionary<MoonPhrase.Type, MoonPhrase> currentPhrases, List<DrumNote> notes)
        {
            const FourLaneDrumPad pad = FourLaneDrumPad.Wildcard;
            const DrumNoteType noteType = DrumNoteType.Neutral;

            var generalFlags = GetGeneralFlags(moonNote, currentPhrases);

            // Beginner is all the same note type, so trills become tremolos
            if ((generalFlags & NoteFlags.Trill) != 0)
            {
                generalFlags &= ~NoteFlags.Trill;
                generalFlags |= NoteFlags.Tremolo;
            }

            var drumFlags = GetDrumNoteFlags(moonNote, currentPhrases);

            double time = _moonSong.TickToTime(moonNote.tick);
            return new DrumNote(pad, noteType, drumFlags, generalFlags, time, moonNote.tick, false, GetStem(pad));
        }

        private DrumNote CreateFiveLaneDrumBeginnerNote(MoonNote moonNote, Dictionary<MoonPhrase.Type, MoonPhrase> currentPhrases, List<DrumNote> notes)
        {
            const FiveLaneDrumPad pad = FiveLaneDrumPad.Wildcard;
            const DrumNoteType noteType = DrumNoteType.Neutral;
            var generalFlags = GetGeneralFlags(moonNote, currentPhrases);

            // Beginner is all the same note type, so trills become tremolos
            if ((generalFlags & NoteFlags.Trill) != 0)
            {
                generalFlags &= ~NoteFlags.Trill;
                generalFlags |= NoteFlags.Tremolo;
            }

            var drumFlags = GetDrumNoteFlags(moonNote, currentPhrases);

            double time = _moonSong.TickToTime(moonNote.tick);
            return new DrumNote(pad, noteType, drumFlags, generalFlags, time, moonNote.tick, false, GetStem(pad));
        }

        private DrumStem GetStem(FourLaneDrumPad pad)
        {
            var stem = pad switch
            {
                FourLaneDrumPad.Kick       => DrumStem.Kick,
                FourLaneDrumPad.RedDrum    => _discoFlip ? DrumStem.Else : DrumStem.Snare,
                FourLaneDrumPad.YellowDrum => _discoFlip ? DrumStem.Snare : DrumStem.Else,
                _                          => DrumStem.Else,
            };
            return ApplyMixConfig(stem);
        }

        private DrumStem GetStem(FiveLaneDrumPad pad)
        {
            var stem = pad switch
            {
                FiveLaneDrumPad.Kick   => DrumStem.Kick,
                FiveLaneDrumPad.Red    => _discoFlip ? DrumStem.Else : DrumStem.Snare,
                FiveLaneDrumPad.Yellow => _discoFlip ? DrumStem.Snare : DrumStem.Else,
                _                      => DrumStem.Else,
            };
            return ApplyMixConfig(stem);
        }

        private DrumStem ApplyMixConfig(DrumStem stem)
        {
            return _mixConfig switch
            {
                DrumsMixConfiguration.StereoKit          => DrumStem.Else,
                DrumsMixConfiguration.MonoKick_StereoKit => stem == DrumStem.Snare ? DrumStem.Else : stem,
                _                                        => stem,
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

        // Left as an example of how to use phrase validation/replacement despite being no longer required
        private Phrase? ValidateDrumsPhrase(Phrase phrase, List<Phrase> phrases)
        {
            if (phrase.Type != PhraseType.DrumFill)
            {
                // We only care about drum fills
                return phrase;
            }

            if (phrase.Time < _codaTime)
            {
                return phrase;
            }

            // If we're here, we were presented a drum fill after a coda and that needs to be a BRE
            return new Phrase(PhraseType.BigRockEnding, phrase.Time, phrase.TimeLength, phrase.Tick, phrase.TickLength);
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
            // Can be populated later if additional note flags are added
            // Activation note marking is done within DrumsPlayer
            return DrumNoteFlags.None;
        }

        private InstrumentDifficulty<DrumNote> LoadFromEliteDrumsDownchartDifficulty(Instrument instrument,
            Difficulty difficulty, CreateNoteDelegate<DrumNote> createNote, ProcessTextDelegate? processText = null)
        {
            var downchart = _downCharts![difficulty];

            var notes = GetNotes(downchart, difficulty, createNote, processText);
            var phrases = GetPhrases(downchart);
            var textEvents = GetTextEvents(downchart);
            return new(instrument, difficulty, notes, phrases, textEvents);
        }

        private static void DrumsFinalPass(InstrumentDifficulty<DrumNote> chart)
        {
            var noteIndex = 0;

            // All we're here to do is assemble lane phrases, so if there aren't any notes or phrases, then we have nothing to do
            if (chart.Phrases.Count == 0 || chart.Notes.Count == 0)
            {
                return;
            }


            for (var phraseIndex = 0; phraseIndex < chart.Phrases.Count; phraseIndex++)
            {
                var phrase = chart.Phrases[phraseIndex];

                if (phrase.Type is not (PhraseType.TremoloLane or PhraseType.TrillLane))
                {
                    continue;
                }

                var notesInPhrase = GetNotesInLanePhrase(chart.Phrases, phraseIndex, chart.Notes, noteIndex, out noteIndex);

                var fourLane = chart.Instrument is Instrument.FourLaneDrums or Instrument.ProDrums;

                List<DrumNote> laneNotes;

                switch (phrase.Type)
                {
                    case PhraseType.TremoloLane:
                        laneNotes = GetDrumTremoloNotes(notesInPhrase, fourLane);
                        break;
                    case PhraseType.TrillLane:
                        if (chart.Difficulty is Difficulty.Beginner)
                        {
                            laneNotes = GetDrumTremoloNotes(notesInPhrase, fourLane);
                        }
                        else
                        {
                            laneNotes = GetDrumTrillNotes(notesInPhrase, fourLane);
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException("Unreachable.");
                }

                if (laneNotes.Count > 0)
                {
                    laneNotes[0].ActivateFlag(NoteFlags.LaneStart);
                    laneNotes[^1].ActivateFlag(NoteFlags.LaneEnd);
                }
            }
        }

        // Takes all notes that are supposedly inside a drum tremolo phrase and validates them.
        // Activates the Tremolo flag for all notes in the phrase that constitute a valid tremolo
        //   -For a well-formed chart, this will be all of them
        //   -If the chart is malformed, the tremolo might terminate earlier than the supposed end of the phrase or be invalidated altogether
        // Returns the list of all marked notes. DrumsFinalPass will assign the LaneStart and LaneEnd flags (shared behavior with trills)
        private static List<DrumNote> GetDrumTremoloNotes(List<DrumNote> notesInPhrase, bool fourLane)
        {
            List<DrumNote> tremoloNotes = new();

            // First we need to know which pad is getting the tremolo. If the tremolo turns out to be invalid, this will be null
            var tremoloPad = GetDrumTremoloPad(notesInPhrase, fourLane);

            if (tremoloPad is not null)
            {
                // If this is a 4L format, we'll need to watch out for a same-color counterpart (tom vs. cymbal) and either prematurely terminate
                // the tremolo or invalidate it altogether
                int? counterpart = fourLane ? (int?) ((FourLaneDrumPad) tremoloPad).OtherPadOfSameColor() : null;

                for (var i = 0; i < notesInPhrase.Count; i++)
                {
                    var note = notesInPhrase[i];

                    foreach (var child in note.AllNotes)
                    {
                        if (counterpart is not null && child.Pad == counterpart)
                        {
                            // Uh-oh, we hit the tremolo pad's same-color counterpart. Terminate early
                            return tremoloNotes;
                        }

                        if (child.Pad == tremoloPad)
                        {
                            child.ActivateFlag(NoteFlags.Tremolo);
                            tremoloNotes.Add(child);
                        }
                    }
                }
            }

            return tremoloNotes;
        }


        // If a tremolo phrase starts on a single non-kick pad (optionally paired with a kick), then it's a tremolo for that pad as long as that pad reoccurs at least once in the phrase.
        // It's okay if other notes of different colors happen later in the phrase, whether they're chorded with the laned pad or not
        // If the other pad of the same color (tom vs. cymbal) occurs, the tremolo is terminated at that point. It's still a valid tremolo up to that point as long as it contains at least 2 valid notes
        // If the starting note never reoccurs for the duration of the tremolo, the tremolo isn't valid
        //
        // If a tremolo phrase starts on a chord (ignoring kicks), it can only be a lane for one pad in the chord
        // Of the pads in the starting chord, the one that gets laned is whichever one reoccurs first
        // If multiple pads from the starting chord reoccur simultaneously, that doesn't count; keep looking for the first unambiguous reoccurence
        // If no starting chord pad ever reoccurs unambiguously in the tremolo, the tremolo isn't valid
        //
        // If a tremolo phrase starts on a lone kick, it isn't valid
        private static int? GetDrumTremoloPad(
            List<DrumNote> notesInPhrase,
            bool fourLane // If this is a 4L format, then we need to run some extra checks regarding same-color counterparts
        )
        {
            var kick = fourLane ? (int) FourLaneDrumPad.Kick : (int) FiveLaneDrumPad.Kick;

            // A tremolo must encompass at least two hits to be valid
            if (notesInPhrase.Count < 2)
            {
                return null;
            }

            // Get a list of pads that this tremolo might be
            List<int> candidatePads = new();
            foreach (var child in notesInPhrase[0].AllNotes)
            {
                if (child.Pad != kick) // Kicks can't form tremolo lanes
                {
                    candidatePads.Add(child.Pad);
                }
            }

            // If there is only one candidate pad, then all we need to do is verify these two things:
            //   -The pad reoccurs at least once in the phrase
            //   -If the pad has a same-color counterpart, that counterpart does not occur earlier than the first reocurrence of the candidate pad
            if (candidatePads.Count == 1)
            {
                var otherPadOfSameColor = fourLane ? ((FourLaneDrumPad) candidatePads[0]).OtherPadOfSameColor() : null;

                // Check all subsequent notes for a reoccurence of the candidate pad
                for (var i = 1; i < notesInPhrase.Count; i++)
                {
                    foreach (var child in notesInPhrase[i].AllNotes)
                    {
                        if (otherPadOfSameColor is not null && (int)otherPadOfSameColor == child.Pad)
                        {
                            // The other pad of the same color reoccurred before the candidate did, so this is not a valid tremolo
                            return null;
                        }

                        if (candidatePads[0] == child.Pad)
                        {
                            // The pad reoccurred, so this is a valid tremolo of that pad
                            return candidatePads[0];
                        }
                    }
                }

                return null; // The pad never reoccurred, so this is not a valid tremolo
            }

            // If there are multiple candidate pads, then we want to give the tremolo to the first one of them to reoccur
            // If a pad's same-pad-of-other-color happens before this is determined, that pad is out of the running
            else if (candidatePads.Count > 1)
            {
                // Check all subsequent notes for a reoccurence of exactly one candidate pad
                for (var i = 1; i < notesInPhrase.Count; i++)
                {
                    List<int> repeatedCandidatePads = new();
                    foreach (var child in notesInPhrase[i].AllNotes)
                    {
                        if (candidatePads.Contains(child.Pad))
                        {
                            repeatedCandidatePads.Add(child.Pad);
                        }
                        else if (fourLane)
                        {
                            var otherPadOfSameColor = ((FourLaneDrumPad) child.Pad).OtherPadOfSameColor();
                            if (otherPadOfSameColor is not null)
                            {
                                // Remove same-color counterparts from the running
                                // For example, if Ycym is one of our candidates, but we just found a Ytom, then we know that Ycym isn't
                                // going to be the lane
                                candidatePads.Remove((int)otherPadOfSameColor);
                            }
                        }
                    }

                    if (repeatedCandidatePads.Count == 1)
                    {
                        // A candidate pad has reoccurred without any other candidate pads on the same tick; that's the tremolo
                        return repeatedCandidatePads[0];
                    }
                }

                return null; // No single candidate pad ever reoccurred, so this is not a valid tremolo
            }

            return null; // There were no candidate pads, presumably because this tremolo started on a lone kick. Not a valid tremolo
        }


        // A drum trill must alternate strictly between two single pads of different colors, ignoring kicks. See the comment on GetDrumTrillPads
        // for more information on what constitutes a valid trill.
        //
        // Assuming a trill is valid, then it lasts until the end of the phrase marker, until a non-trill pad (besides kicks) occurs, or
        // until the trill repeats the same pad twice instead of strictly alternating, whichever comes first.
        //
        // Drum trills do not support "gravity blast"-style unlaned notes (besides kicks) in the way that drum tremolos do. Any unlaned non-kick note
        // terminates the trill immediately, regardless of whether it's chorded with a lane note.
        private static List<DrumNote> GetDrumTrillNotes(List<DrumNote> notesInPhrase, bool fourLane)
        {
            var kick = fourLane ? (int) FourLaneDrumPad.Kick : (int) FiveLaneDrumPad.Kick;

            List<DrumNote> trillNotes = new();
            var trillPads = GetDrumTrillPads(notesInPhrase, fourLane);

            if (trillPads is not null)
            {
                var trillPad1 = trillPads.Value.pad1;
                var trillPad2 = trillPads.Value.pad2;

                foreach (var note in notesInPhrase)
                {
                    foreach (var child in note.AllNotes)
                    {
                        if (child.Pad == kick)
                        {
                            // Ignore kicks
                            continue;
                        }

                        if (child.Pad != trillPad1 && child.Pad != trillPad2)
                        {
                            // This is not a valid part of the trill, so we're terminating early
                            return trillNotes;
                        }

                        if (trillNotes.Count > 0 && child.Pad == trillNotes[^1].Pad)
                        {
                            // Repeating the same trill note twice is invalid, so we're terminating early
                            return trillNotes;
                        }
                    }

                    // We didn't terminate early, so this hit is part of the trill. We're going to add it to the trill notes,
                    // although there could still be a kick here, so we need to iterate again (over a maximum of 2 notes) to
                    // avoid flagging it if so
                    foreach (var child in note.AllNotes)
                    {
                        if (child.Pad != kick)
                        {
                            child.ActivateFlag(NoteFlags.Trill);
                            trillNotes.Add(child);
                            break;
                        }
                    }
                }
            }

            return trillNotes;
        }


        // A drum trill phrase is defined by exactly two alternating pads. Those pads must be different colors (e.g. no Gtom+Gcym trills)
        //
        // The first hit of the trill phrase must be a single pad (optionally paired with a kick, but it cannot be a lone kick)
        //
        // The second non-kick hit of the trill phrase must be a different single pad of a different color (it can be paired with
        // a kick, and there can also be kicks between the first and second trill notes)
        //
        // The third non-kick hit of the trill must be the same single pad as the first (again, optionally paired with a kick, and
        // there can be interposed kicks as well)
        //
        // If all of those conditions are not met, then this trill is valid for those pads. Otherwise, the trill is not valid.
        private static (int pad1, int pad2)? GetDrumTrillPads(List<DrumNote> notesInPhrase, bool fourLane)
        {
            var kick = fourLane ? (int) FourLaneDrumPad.Kick : (int) FiveLaneDrumPad.Kick;

            // A tremolo must encompass at least three hits to be legitimate
            if (notesInPhrase.Count < 3)
            {
                return null;
            }

            int? pad1 = null;

            // The first note of the phrase must have exactly one non-kick gem (optionally paired with a kick)
            foreach (var child in notesInPhrase[0].AllNotes)
            {
                if (child.Pad == kick)
                {
                    continue;
                }

                if (pad1 is not null)
                {
                    // We already found a non-kick pad, so this is a hand chord; not a valid trill
                    return null;
                }

                pad1 = child.Pad;
            }

            if (pad1 is null)
            {
                // We didn't find a valid first pad (presumably because the phrase started on a lone kick), so this is not a valid trill
                return null;
            }

            // So far so good; time to find the second pad
            int? pad2 = null;

            var noteRef = notesInPhrase[1]; // It's probably here, but this might be a lone kick, in which case we need to keep going forward

            // Iterate until we find something other than a lone kick
            while (noteRef is not null && pad2 is null)
            {
                if (noteRef.NextNote is null)
                {
                    // We still need a third note for the trill to be legitimate, so if there's no next note, we're already done
                    return null;
                }

                if (!noteRef.IsChord && noteRef.Pad == kick)
                {
                    // If this is a lone kick, move on
                    noteRef = noteRef.NextNote;
                    continue;
                }

                // We found our first hit that isn't a lone kick; this is where we're going to need to find our second trill pad
                // If it's not here, this isn't a valid trill
                foreach (var child in noteRef.AllNotes)
                {
                    if (child.Pad == kick)
                    {
                        continue;
                    }

                    if (pad2 is not null)
                    {
                        // We already found a non-kick pad, so this is a hand chord; not a valid trill
                        return null;
                    }

                    if (child.Pad == pad1)
                    {
                        // The phrase starts with two of the same pad in a row; not a valid trill
                        return null;
                    }

                    if (fourLane && child.Pad == (int?) ((FourLaneDrumPad) pad1).OtherPadOfSameColor())
                    {
                        // The phrase starts with a tom and cymbal of the same color back-to-back (in either order); not a valid trill
                        return null;
                    }

                    pad2 = child.Pad;
                }
            }

            if (pad2 is null)
            {
                // We didn't find a valid second pad (presumably because the phrasae was nothing but kicks after the first hit), so this is not a valid trill
                return null;
            }

            // We have two valid trill notes. We just need to make sure that the next non-lone-kick hit matches the first, and we have our pads
            for (var i = 2; i < notesInPhrase.Count; i++)
            {
                noteRef = notesInPhrase[i];

                if (!noteRef.IsChord && noteRef.Pad == kick)
                {
                    // This is a lone kick; move on
                    continue;
                }

                // We found our first hit that isn't a lone kick; this is where we're going to need to find our second trill pad
                foreach (var child in noteRef.AllNotes)
                {
                    if (child.Pad == kick)
                    {
                        continue;
                    }

                    if (child.Pad != pad1)
                    {
                        // We found a non-kick pad that doesn't match pad1; not a valid trill
                        return null;
                    }
                }

                // If we made it here, we know we have a valid third hit
                //   -If there were a non-kick pad that didn't match pad1, we would have returned null in the previous if statement
                //   -If there were only a kick pad, we would have continued the while loop in the first if statement
                //   -Thus, the only possibilities are a lone pad that matches pad1, or that plus a kick
                return (pad1.Value, pad2.Value);
            }

            // If we're here, we never found a valid third hit (presumably because the phrase was nothing but kicks after the second
            // pad); // not a valid trill
            return null;
        }
    }
}