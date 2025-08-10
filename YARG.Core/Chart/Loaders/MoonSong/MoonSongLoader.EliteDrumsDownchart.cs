using MoonscraperChartEditor.Song;
using System;
using System.Collections.Generic;
using static YARG.Core.Chart.EliteDrumNote;

namespace YARG.Core.Chart
{
    internal partial class MoonSongLoader : ISongLoader
    {
        private Dictionary<Difficulty, MoonChart>? _downCharts = null;

        private List<MoonText> _downchartTextEvents = new();

        private Dictionary<Difficulty, MoonChart>? DownchartEliteDrumsTrack(InstrumentTrack<EliteDrumNote> eliteDrumsTrack)
        {
            var downcharts = new Dictionary<Difficulty, MoonChart>()
            {
                {  Difficulty.Easy, DownchartEliteDrumsDifficulty(eliteDrumsTrack, Difficulty.Easy) },
                {  Difficulty.Medium, DownchartEliteDrumsDifficulty(eliteDrumsTrack, Difficulty.Medium) },
                {  Difficulty.Hard, DownchartEliteDrumsDifficulty(eliteDrumsTrack, Difficulty.Hard) },
                {  Difficulty.Expert, DownchartEliteDrumsDifficulty(eliteDrumsTrack, Difficulty.Expert) },
                {  Difficulty.ExpertPlus, DownchartEliteDrumsDifficulty(eliteDrumsTrack, Difficulty.ExpertPlus) },
            };

            var atLeastOneDownchartHasAtLeastOneNote = false;
            foreach (var downchart in downcharts)
            {
                if (downchart.Value.notes.Count == 0)
                {
                    continue;
                }

                atLeastOneDownchartHasAtLeastOneNote = true;

                foreach (var textEvent in _downchartTextEvents)
                {
                    downchart.Value.Insert(textEvent);
                }
            }

            return atLeastOneDownchartHasAtLeastOneNote ? downcharts : null;
        }

        private MoonChart DownchartEliteDrumsDifficulty(InstrumentTrack<EliteDrumNote> eliteDrumsTrack, Difficulty difficulty)
        {
            MoonChart moonChart = new(MoonChart.GameMode.Drums);

            var eliteDrumsDifficulty = eliteDrumsTrack.GetDifficulty(difficulty);

            // Downchart notes
            List<DownchartChord> unresolvedChords = new();
            foreach (var eliteDrumNote in eliteDrumsDifficulty.Notes)
            {
                var chord = DownchartEliteDrumsChord(eliteDrumNote, eliteDrumsDifficulty.Phrases);
                if (chord is not null)
                {
                    unresolvedChords.Add(chord.Value);
                }
            }

            var notes = ResolveDownchartCollisions(unresolvedChords);

            for (var i = 0; i < notes.Count; i++)
            {
                var note = notes[i];
                if (i > 0)
                {
                    note.previous = notes[i - 1];
                    notes[i - 1].next = note;
                }

                moonChart.Add(note);
            }

            
            var (discoOnText, discoOffText) = GetDiscoFlipEventText(difficulty);
            List<MoonPhrase> phrases = new();
            List<MoonText> textEvents = new()
            {
                new(discoOffText, 0)
            };

            foreach (var phrase in eliteDrumsDifficulty.Phrases)
            {
                switch (phrase.Type)
                {
                    case PhraseType.StarPower:
                        phrases.Add(new(phrase.Tick, phrase.TickLength, MoonPhrase.Type.Starpower));
                        break;
                    case PhraseType.DrumFill:
                        phrases.Add(new(phrase.Tick, phrase.TickLength, MoonPhrase.Type.ProDrums_Activation));
                        break;
                    case PhraseType.VersusPlayer1:
                        phrases.Add(new(phrase.Tick, phrase.TickLength, MoonPhrase.Type.Versus_Player1));
                        break;
                    case PhraseType.VersusPlayer2:
                        phrases.Add(new(phrase.Tick, phrase.TickLength, MoonPhrase.Type.Versus_Player2));
                        break;
                    case PhraseType.BigRockEnding:
                        // TODO
                        break;
                    case PhraseType.Solo:
                        phrases.Add(new(phrase.Tick, phrase.TickLength, MoonPhrase.Type.Solo));
                        break;
                    case PhraseType.EliteDrums_DiscoFlip:
                        if (phrase.Tick == 0)
                        {
                            textEvents[0].text = discoOnText;
                        }
                        else
                        {
                            textEvents.Add(new(discoOnText, phrase.Tick));
                        }
                        textEvents.Add(new(discoOffText, phrase.TickEnd));
                        break;
                }
            }

            foreach (var phrase in phrases)
            {
                moonChart.Add(phrase);
            }

            foreach (var textEvent in textEvents)
            {
                _downchartTextEvents.Add(textEvent);
            }

            return moonChart;
        }

        private (string onText, string offText) GetDiscoFlipEventText(Difficulty difficulty)
        {
            var diffNum = difficulty switch
            {
                Difficulty.Beginner or Difficulty.Easy => 0,
                Difficulty.Medium => 1,
                Difficulty.Hard => 2,
                Difficulty.Expert or Difficulty.ExpertPlus => 3,
                _ => throw new Exception("Unreachable")
            };

            return ($"mix {diffNum} drums0d", $"mix {diffNum} drums0");
        }

        private static DownchartChord? DownchartEliteDrumsChord(EliteDrumNote eliteDrumChord, List<Phrase> phrases)
        {
            MoonNote? kick = null;
            DownchartNote? firstHandGem = null;
            DownchartNote? secondHandGem = null;

            var chordIsInDiscoFlip = false;
            foreach (var phrase in phrases)
            {
                if (phrase.Tick > eliteDrumChord.Tick)
                {
                    break;
                }
                if (phrase.Type is PhraseType.EliteDrums_DiscoFlip)
                {
                    if (phrase.Tick <= eliteDrumChord.Tick && phrase.TickEnd > eliteDrumChord.Tick)
                    {
                        chordIsInDiscoFlip = true;
                        break;
                    }
                }
            }

            foreach (var eliteDrumNote in eliteDrumChord.AllNotes)
            {
                var downchartedNotes = DownchartIndividualEliteDrumsNote(eliteDrumNote, chordIsInDiscoFlip);
                foreach (var downchartedNote in downchartedNotes)
                {
                    if (downchartedNote.MoonNote.drumPad == MoonNote.DrumPad.Kick)
                    {
                        kick = downchartedNote.MoonNote;
                    }
                    else if (firstHandGem is null)
                    {
                        firstHandGem = downchartedNote;
                    }
                    else if (secondHandGem is null)
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

            return new(kick, firstHandGem, secondHandGem);
        }


        // In most cases, returns 1 note. Unforced or invisible hat pedals return 0 notes, while flams return 2.
        private static List<DownchartNote> DownchartIndividualEliteDrumsNote(EliteDrumNote eliteDrumNote, bool noteIsInDiscoFlip)
        {
            List<DownchartNote> notes = new();

            // Never downchart invisible terminator hat pedals, even if channel flagged
            if (eliteDrumNote.Pad is (int)EliteDrumPad.HatPedal && eliteDrumNote.IsInvisibleTerminator) return notes;

            (MoonNote.DrumPad? pad, MoonNote.Flags flags) = ((EliteDrumPad) eliteDrumNote.Pad) switch
            {
                EliteDrumPad.HatPedal =>    (GetDrumPadForChannelFlag(eliteDrumNote,   null),                      MoonNote.Flags.ProDrums_Cymbal),
                EliteDrumPad.Kick =>        (                                          MoonNote.DrumPad.Kick,      MoonNote.Flags.None),
                EliteDrumPad.Snare =>       (GetDrumPadForChannelFlag(eliteDrumNote,   MoonNote.DrumPad.Red),      MoonNote.Flags.None),
                EliteDrumPad.HiHat =>       (GetDrumPadForChannelFlag(eliteDrumNote,   MoonNote.DrumPad.Yellow),   MoonNote.Flags.ProDrums_Cymbal),
                EliteDrumPad.LeftCrash =>   (GetDrumPadForChannelFlag(eliteDrumNote,   MoonNote.DrumPad.Blue),     MoonNote.Flags.ProDrums_Cymbal),
                EliteDrumPad.Tom1 =>        (GetDrumPadForChannelFlag(eliteDrumNote,   MoonNote.DrumPad.Yellow),   MoonNote.Flags.None),
                EliteDrumPad.Tom2 =>        (GetDrumPadForChannelFlag(eliteDrumNote,   MoonNote.DrumPad.Blue),   MoonNote.Flags.None),
                EliteDrumPad.Tom3 =>        (GetDrumPadForChannelFlag(eliteDrumNote,   MoonNote.DrumPad.Green),   MoonNote.Flags.None),
                EliteDrumPad.Ride =>        (GetDrumPadForChannelFlag(eliteDrumNote,   MoonNote.DrumPad.Blue),     MoonNote.Flags.ProDrums_Cymbal),
                EliteDrumPad.RightCrash =>  (GetDrumPadForChannelFlag(eliteDrumNote,   MoonNote.DrumPad.Green),    MoonNote.Flags.ProDrums_Cymbal),
                _ => throw new Exception("Unreachable.")
            };

            if (pad is not null)
            {
                if (noteIsInDiscoFlip)
                {
                    // Disco-flipped snares are charted as Ycyms
                    if (pad is MoonNote.DrumPad.Red)
                    {
                        pad = MoonNote.DrumPad.Yellow;
                        flags &= MoonNote.Flags.ProDrums_Cymbal;
                    }
                    else if (pad is MoonNote.DrumPad.Yellow)
                    {
                        // Disco flipped Ycyms are charted as snares
                        if ((flags & MoonNote.Flags.ProDrums_Cymbal) != 0)
                        {
                            pad = MoonNote.DrumPad.Red;
                            flags &= ~MoonNote.Flags.ProDrums_Cymbal;
                        }

                        // Disco flipped Ytoms are treated like collisions, shunted to Btoms
                        else
                        {
                            pad = MoonNote.DrumPad.Blue;
                        }
                    }
                }

                flags |= eliteDrumNote.Dynamics switch
                {
                    DrumNoteType.Accent => MoonNote.Flags.ProDrums_Accent,
                    DrumNoteType.Ghost => MoonNote.Flags.ProDrums_Ghost,
                    _ => MoonNote.Flags.None
                };

                var mainNote = new MoonNote(eliteDrumNote.Tick, -1, 0, flags)
                {
                    drumPad = pad.Value
                };

                notes.Add(new(mainNote, eliteDrumNote));

                if (eliteDrumNote.IsFlam)
                {
                    MoonNote.DrumPad? otherPad = pad.Value switch
                    {
                        MoonNote.DrumPad.Kick => null,
                        MoonNote.DrumPad.Red => MoonNote.DrumPad.Yellow,
                        MoonNote.DrumPad.Yellow => MoonNote.DrumPad.Blue,
                        MoonNote.DrumPad.Blue => MoonNote.DrumPad.Green,
                        MoonNote.DrumPad.Green => MoonNote.DrumPad.Blue,
                        _ => throw new Exception("Unreachable.")
                    };

                    if (otherPad is not null)
                    {
                        var flamPartner = new MoonNote(eliteDrumNote.Tick, -1, 0, flags)
                        {
                            drumPad = otherPad.Value
                        };

                        notes.Add(new(flamPartner, eliteDrumNote));
                    }
                }
            }

            return notes;
        }

        private List<MoonNote> ResolveDownchartCollisions(List<DownchartChord> unresolvedChords)
        {
            List<MoonNote> notes = new();

            foreach (var unresolvedChord in unresolvedChords)
            {
                foreach (var note in ResolveDownchartCollision(unresolvedChord))
                {
                    notes.Add(note);
                }
            }

            return notes;
        }

        private List<MoonNote> ResolveDownchartCollision(DownchartChord downchartChord)
        {
            List<MoonNote> notes = new();

            if (downchartChord.Kick is not null)
            {
                notes.Add(downchartChord.Kick!);
            }

            if (downchartChord.SecondHandGem is null)
            {
                // Can't have collisions without a second hand gem, so return early
                if (downchartChord.FirstHandGem is not null)
                {
                    notes.Add(downchartChord.FirstHandGem.Value.MoonNote);
                }

                return notes;
            }

            var firstHandGem = downchartChord.FirstHandGem!.Value;
            var secondHandGem = downchartChord.SecondHandGem!.Value;


            if (firstHandGem.MoonNote.drumPad != secondHandGem.MoonNote.drumPad)
            {
                // Two hand gems, but no collision

                // Special case: Unforced LCrash + Unforced RCrash resolves to YG instead of BG
                if (firstHandGem.Origin.ChannelFlag is EliteDrumsChannelFlag.None && secondHandGem.Origin.ChannelFlag is EliteDrumsChannelFlag.None)
                {
                    if (firstHandGem.Origin.Pad is (int) EliteDrumPad.LeftCrash && secondHandGem.Origin.Pad is (int) EliteDrumPad.RightCrash)
                    {
                        firstHandGem.MoonNote.drumPad = MoonNote.DrumPad.Yellow;
                    }
                    else if (firstHandGem.Origin.Pad is (int) EliteDrumPad.RightCrash && secondHandGem.Origin.Pad is (int) EliteDrumPad.LeftCrash)
                    {
                        secondHandGem.MoonNote.drumPad = MoonNote.DrumPad.Yellow;
                    }
                }
            }
            else
            {

                // Two hand gems with equal colors - collision!

                // For tom/cymbal collisions, the tom goes on the left. For tom/tom and cym/cym collisions, preserve the
                // handedness of the dynamics from the original Elite Drums chord
                MoonNote.DrumPad newLeftPad;
                MoonNote.DrumPad newRightPad;

                var firstHandGemIsCymbal = (firstHandGem.MoonNote.flags & MoonNote.Flags.ProDrums_Cymbal) != 0;
                var secondHandGemIsCymbal = (secondHandGem.MoonNote.flags & MoonNote.Flags.ProDrums_Cymbal) != 0;

                if (firstHandGemIsCymbal == secondHandGemIsCymbal)
                {
                    // This is a tom/tom or cym/cym collision, so we'll need to preserve the handedness of the dynamics
                    // from the original Elite Drums chord
                    (var leftHandGem, var rightHandGem) = firstHandGem.Origin.Pad < secondHandGem.Origin.Pad ? (firstHandGem, secondHandGem) : (secondHandGem, firstHandGem);

                    (newLeftPad, newRightPad) = firstHandGem.MoonNote.drumPad switch
                    {
                        MoonNote.DrumPad.Red => (MoonNote.DrumPad.Red, MoonNote.DrumPad.Yellow),
                        MoonNote.DrumPad.Yellow => (MoonNote.DrumPad.Yellow, MoonNote.DrumPad.Blue),
                        MoonNote.DrumPad.Blue => (MoonNote.DrumPad.Blue, MoonNote.DrumPad.Green),
                        MoonNote.DrumPad.Green => (MoonNote.DrumPad.Blue, MoonNote.DrumPad.Green),
                        _ => throw new Exception("Unreachable.")
                    };

                    leftHandGem.MoonNote.drumPad = newLeftPad;
                    rightHandGem.MoonNote.drumPad = newRightPad;
                }
                else
                {
                    // This is a tom/cym collision (or vice-versa)
                    // Nudge the tom to the left
                    (var cym, var tom) = firstHandGemIsCymbal ? (firstHandGem, secondHandGem) : (secondHandGem, firstHandGem);

                    switch (cym.MoonNote.drumPad)
                    {
                        case MoonNote.DrumPad.Yellow:
                            newLeftPad = MoonNote.DrumPad.Red;
                            newRightPad = MoonNote.DrumPad.Yellow;
                            break;
                        case MoonNote.DrumPad.Blue:
                            newLeftPad = MoonNote.DrumPad.Yellow;
                            newRightPad = MoonNote.DrumPad.Blue;
                            break;
                        case MoonNote.DrumPad.Green:
                            newLeftPad = MoonNote.DrumPad.Blue;
                            newRightPad = MoonNote.DrumPad.Green;
                            break;
                        default:
                            throw new Exception("Unreachable.");
                    }

                    tom.MoonNote.drumPad = newLeftPad;
                    cym.MoonNote.drumPad = newRightPad;
                }
            }

            notes.Add(firstHandGem.MoonNote);
            notes.Add(secondHandGem.MoonNote);
            return notes;
        }

        private static MoonNote.DrumPad? GetDrumPadForChannelFlag(EliteDrumNote drum, MoonNote.DrumPad? unforced)
        {
            return drum.ChannelFlag switch
            {
                EliteDrumsChannelFlag.Red => MoonNote.DrumPad.Red,
                EliteDrumsChannelFlag.Yellow => MoonNote.DrumPad.Yellow,
                EliteDrumsChannelFlag.Blue => MoonNote.DrumPad.Blue,
                EliteDrumsChannelFlag.Green => MoonNote.DrumPad.Green,
                _ => unforced
            };
        }
    }

    internal readonly struct DownchartChord
    {
        public DownchartChord(MoonNote? kick, DownchartNote? firstHandGem, DownchartNote? secondHandGem)
        {
            Kick = kick;
            FirstHandGem = firstHandGem;
            SecondHandGem = secondHandGem;
        }

        public MoonNote? Kick { get; }
        public DownchartNote? FirstHandGem { get; }
        public DownchartNote? SecondHandGem { get; }

    }

    internal readonly struct DownchartNote {
        public DownchartNote(MoonNote moonNote, EliteDrumNote origin)
        {
            MoonNote = moonNote;
            Origin = origin;
        }

        public MoonNote MoonNote { get; }
        public EliteDrumNote Origin { get; }

    }
}
