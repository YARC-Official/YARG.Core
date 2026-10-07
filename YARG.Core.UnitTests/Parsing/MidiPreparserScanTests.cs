using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using NUnit.Framework;
using YARG.Core.Chart;
using YARG.Core.IO;
using YARG.Core.Song;
using YARG.Core.UnitTests.Song;

namespace YARG.Core.UnitTests.Parsing
{
    using MidiTextEvent = Melanchall.DryWetMidi.Core.TextEvent;

    /// <summary>
    /// End-to-end tests for the MIDI preparsers: builds real MIDI bytes via DryWetMidi,
    /// feeds them through <see cref="SongEntry.ParseMidi"/> (via <see cref="TestSongEntry"/>),
    /// and asserts the resulting <see cref="AvailableParts"/> and <see cref="DrumsType"/>.
    /// This covers the preparsers, the YARGMidiFile binary parser, and track-name lookup in one shot.
    /// </summary>
    public class MidiPreparserScanTests
    {
        private const short Resolution = 192;
        private const int DefaultVelocity = 100;

        private readonly record struct TimedMidiEvent(long Tick, MidiEvent Event);

        private static SevenBitNumber S(int number) => (SevenBitNumber) (byte) number;
        private static FourBitNumber F(int number) => (FourBitNumber) (byte) number;

        // Five fret: valid lanes are 60-65 per difficulty (59 is open, disabled by default)
        private const int FIVEFRET_EASY = 60;
        private const int FIVEFRET_MEDIUM = 72;
        private const int FIVEFRET_HARD = 84;
        private const int FIVEFRET_EXPERT = 96;
        private const int FIVEFRET_OPEN = 59;

        // Six fret: valid lanes are 58-64 per difficulty
        private const int SIXFRET_EASY = 62;
        private const int SIXFRET_MEDIUM = 74;
        private const int SIXFRET_HARD = 86;
        private const int SIXFRET_EXPERT = 98;

        // Drums: kick lane is the first lane of each difficulty
        private const int DRUMS_EASY = 60;
        private const int DRUMS_MEDIUM = 72;
        private const int DRUMS_HARD = 84;
        private const int DRUMS_EXPERT = 96;
        private const int DRUMS_DOUBLE_KICK = 95;
        private const int DRUMS_YELLOW_TOM = 65;
        private const int DRUMS_PRO_FLAG = 110;

        // Elite drums: 24 notes per difficulty, 11 lanes
        private const int ELITE_EASY = 1;
        private const int ELITE_MEDIUM = 25;
        private const int ELITE_HARD = 49;
        private const int ELITE_EXPERT = 74;
        private const int ELITE_DOUBLE_KICK = 73;
        private const int ELITE_HAT_PEDAL_EASY = 0;

        // Pro guitar: 24 notes per difficulty, 6 strings
        private const int PROGUITAR_EASY = 24;
        private const int PROGUITAR_MEDIUM = 48;
        private const int PROGUITAR_HARD = 72;
        private const int PROGUITAR_EXPERT = 96;

        // Pro keys: any note in 48-72
        private const int PROKEYS_NOTE = 48;

        // Vocals: 36-84 are vocals, 105/106 are phrases, 96 is percussion
        private const int VOCAL_NOTE = 60;
        private const int VOCAL_PHRASE = 105;
        private const int VOCAL_PERCUSSION = 96;

        #region Five fret

        [Test]
        public void FiveFretGuitar_ParsesAllFourDifficulties()
        {
            var (result, parts, _) = ScanMidi(
                MakeTrack("PART GUITAR",
                    Note(0, 100, FIVEFRET_EASY),
                    Note(200, 300, FIVEFRET_MEDIUM),
                    Note(400, 500, FIVEFRET_HARD),
                    Note(600, 700, FIVEFRET_EXPERT)));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.HasValue, Is.True);
                Assert.That(parts.FiveFretGuitar.Difficulties,
                    Is.EqualTo(DifficultyMask.Beginner | DifficultyMask.Easy | DifficultyMask.Medium |
                        DifficultyMask.Hard | DifficultyMask.Expert));
                Assert.That(parts.FiveFretBass.Difficulties, Is.EqualTo(DifficultyMask.None));
            }
        }

        [Test]
        public void FiveFretGuitar_SingleDifficulty_DoesNotAddOthers()
        {
            var (_, parts, _) = ScanMidi(MakeTrack("PART GUITAR", Note(0, 100, FIVEFRET_MEDIUM)));

            Assert.That(parts.FiveFretGuitar.Difficulties, Is.EqualTo(DifficultyMask.Medium));
        }

        [Test]
        public void FiveFretGuitar_OpenNote_RequiresEnhancedOpensText()
        {
            var (_, withoutText, _) = ScanMidi(MakeTrack("PART GUITAR", Note(0, 100, FIVEFRET_OPEN)));
            var (_, withText, _) = ScanMidi(MakeTrack("PART GUITAR",
                Text(0, "[ENHANCED_OPENS]"),
                Note(0, 100, FIVEFRET_OPEN)));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(withoutText.FiveFretGuitar.Difficulties, Is.EqualTo(DifficultyMask.None));
                Assert.That(withText.FiveFretGuitar.Difficulties, Is.EqualTo(DifficultyMask.Beginner | DifficultyMask.Easy));
            }
        }

        [Test]
        public void FiveFret_AllTrackTypes_AreParsed()
        {
            var (_, parts, _) = ScanMidi(
                MakeTrack("PART GUITAR", Note(0, 100, FIVEFRET_EASY)),
                MakeTrack("PART BASS", Note(0, 100, FIVEFRET_EASY)),
                MakeTrack("PART RHYTHM", Note(0, 100, FIVEFRET_EASY)),
                MakeTrack("PART GUITAR COOP", Note(0, 100, FIVEFRET_EASY)),
                MakeTrack("PART KEYS", Note(0, 100, FIVEFRET_EASY)));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(parts.FiveFretGuitar.Difficulties, Is.EqualTo(DifficultyMask.Beginner | DifficultyMask.Easy));
                Assert.That(parts.FiveFretBass.Difficulties, Is.EqualTo(DifficultyMask.Beginner | DifficultyMask.Easy));
                Assert.That(parts.FiveFretRhythm.Difficulties, Is.EqualTo(DifficultyMask.Beginner | DifficultyMask.Easy));
                Assert.That(parts.FiveFretCoopGuitar.Difficulties, Is.EqualTo(DifficultyMask.Beginner | DifficultyMask.Easy));
                Assert.That(parts.Keys.Difficulties, Is.EqualTo(DifficultyMask.Beginner | DifficultyMask.Easy));
                Assert.That(parts.SixFretGuitar.Difficulties, Is.EqualTo(DifficultyMask.None));
            }
        }

        #endregion

        #region Six fret

        [Test]
        public void SixFretGuitar_ParsesAllFourDifficulties_WithoutBeginner()
        {
            var (_, parts, _) = ScanMidi(
                MakeTrack("PART GUITAR GHL",
                    Note(0, 100, SIXFRET_EASY),
                    Note(200, 300, SIXFRET_MEDIUM),
                    Note(400, 500, SIXFRET_HARD),
                    Note(600, 700, SIXFRET_EXPERT)));

            Assert.That(parts.SixFretGuitar.Difficulties,
                Is.EqualTo(DifficultyMask.Easy | DifficultyMask.Medium | DifficultyMask.Hard | DifficultyMask.Expert));
        }

        [Test]
        public void SixFret_AllTrackTypes_AreParsed()
        {
            var (_, parts, _) = ScanMidi(
                MakeTrack("PART GUITAR GHL", Note(0, 100, SIXFRET_EASY)),
                MakeTrack("PART BASS GHL", Note(0, 100, SIXFRET_EASY)),
                MakeTrack("PART RHYTHM GHL", Note(0, 100, SIXFRET_EASY)),
                MakeTrack("PART GUITAR COOP GHL", Note(0, 100, SIXFRET_EASY)));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(parts.SixFretGuitar.Difficulties, Is.EqualTo(DifficultyMask.Easy));
                Assert.That(parts.SixFretBass.Difficulties, Is.EqualTo(DifficultyMask.Easy));
                Assert.That(parts.SixFretRhythm.Difficulties, Is.EqualTo(DifficultyMask.Easy));
                Assert.That(parts.SixFretCoopGuitar.Difficulties, Is.EqualTo(DifficultyMask.Easy));
            }
        }

        #endregion

        #region Drums

        [Test]
        public void Drums_FourLane_KeepsFourLaneType()
        {
            var (_, parts, drumsType) = ScanMidi(DrumsType.FourLane,
                MakeTrack("PART DRUMS",
                    Note(0, 100, DRUMS_EASY),
                    Note(200, 300, DRUMS_MEDIUM),
                    Note(400, 500, DRUMS_HARD),
                    Note(600, 700, DRUMS_EXPERT)));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(drumsType, Is.EqualTo(DrumsType.FourLane));
                Assert.That(parts.FourLaneDrums.Difficulties,
                    Is.EqualTo(DifficultyMask.Beginner | DifficultyMask.Easy | DifficultyMask.Medium |
                        DifficultyMask.Hard | DifficultyMask.Expert));
            }
        }

        [Test]
        public void Drums_ProDrums_DetectedFromProNote()
        {
            var (_, parts, drumsType) = ScanMidi(DrumsType.FourLane,
                MakeTrack("PART DRUMS",
                    Note(0, 100, DRUMS_EASY),
                    Note(200, 300, DRUMS_PRO_FLAG)));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(drumsType, Is.EqualTo(DrumsType.ProDrums));
                Assert.That(parts.FourLaneDrums.Difficulties, Is.EqualTo(DifficultyMask.Beginner | DifficultyMask.Easy));
            }
        }

        [Test]
        public void Drums_FiveLane_DetectedFromYellowTom_AndFinalized()
        {
            var (_, parts, drumsType) = ScanMidi(DrumsType.FiveLane,
                MakeTrack("PART DRUMS", Note(0, 100, DRUMS_YELLOW_TOM)));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(drumsType, Is.EqualTo(DrumsType.FiveLane));
                Assert.That(parts.FourLaneDrums.Difficulties, Is.EqualTo(DifficultyMask.Beginner | DifficultyMask.Easy));

                var finalized = TestSongEntry.FinalizeDrumsForTest(parts, drumsType);
                Assert.That(finalized.FiveLaneDrums.Difficulties, Is.EqualTo(DifficultyMask.Beginner | DifficultyMask.Easy));
                Assert.That(finalized.FourLaneDrums.Difficulties, Is.EqualTo(DifficultyMask.None));
            }
        }

        [Test]
        public void Drums_YellowTom_IgnoredWhenFourLane()
        {
            var (_, parts, drumsType) = ScanMidi(DrumsType.FourLane,
                MakeTrack("PART DRUMS", Note(0, 100, DRUMS_YELLOW_TOM)));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(drumsType, Is.EqualTo(DrumsType.FourLane));
                Assert.That(parts.FourLaneDrums.Difficulties, Is.EqualTo(DifficultyMask.None));
            }
        }

        [Test]
        public void Drums_DoubleKick_ActivatesExpertPlus()
        {
            var (_, parts, _) = ScanMidi(DrumsType.FourLane,
                MakeTrack("PART DRUMS", Note(0, 100, DRUMS_DOUBLE_KICK)));

            Assert.That(parts.FourLaneDrums.Difficulties, Is.EqualTo(DifficultyMask.Expert | DifficultyMask.ExpertPlus));
        }

        [Test]
        public void Drums_ProNote_IgnoredWhenFiveLane()
        {
            var (_, _, drumsType) = ScanMidi(DrumsType.FiveLane,
                MakeTrack("PART DRUMS", Note(0, 100, DRUMS_PRO_FLAG)));

            Assert.That(drumsType, Is.EqualTo(DrumsType.FiveLane));
        }

        #endregion

        #region Elite drums

        [Test]
        public void EliteDrums_ParsesDifficulties_AndSetsDownchart()
        {
            var (_, parts, _) = ScanMidi(
                MakeTrack("PART ELITE_DRUMS",
                    Note(0, 100, ELITE_EASY),
                    Note(200, 300, ELITE_MEDIUM),
                    Note(400, 500, ELITE_HARD),
                    Note(600, 700, ELITE_EXPERT)));

            var expected = DifficultyMask.Beginner | DifficultyMask.Easy | DifficultyMask.Medium |
                DifficultyMask.Hard | DifficultyMask.Expert;

            using (Assert.EnterMultipleScope())
            {
                Assert.That(parts.EliteDrums.Difficulties, Is.EqualTo(expected));
                Assert.That(parts.FourLaneDrums.Difficulties, Is.EqualTo(expected));
            }
        }

        [Test]
        public void EliteDrums_HatPedal_ExcludedFromDownchart()
        {
            var (_, parts, _) = ScanMidi(MakeTrack("PART ELITE_DRUMS", Note(0, 100, ELITE_HAT_PEDAL_EASY)));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(parts.EliteDrums.Difficulties, Is.EqualTo(DifficultyMask.Beginner | DifficultyMask.Easy));
                Assert.That(parts.FourLaneDrums.Difficulties, Is.EqualTo(DifficultyMask.None));
            }
        }

        [Test]
        public void EliteDrums_HatPedal_WithCymbalChannel_IncludedInDownchart()
        {
            var (_, parts, _) = ScanMidi(MakeTrack("PART ELITE_DRUMS", Note(0, 100, ELITE_HAT_PEDAL_EASY, channel: 11)));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(parts.EliteDrums.Difficulties, Is.EqualTo(DifficultyMask.Beginner | DifficultyMask.Easy));
                Assert.That(parts.FourLaneDrums.Difficulties, Is.EqualTo(DifficultyMask.Beginner | DifficultyMask.Easy));
            }
        }

        [Test]
        public void EliteDrums_DoubleKick_ActivatesExpertPlus()
        {
            var (_, parts, _) = ScanMidi(MakeTrack("PART ELITE_DRUMS", Note(0, 100, ELITE_DOUBLE_KICK)));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(parts.EliteDrums.Difficulties, Is.EqualTo(DifficultyMask.Expert | DifficultyMask.ExpertPlus));
                Assert.That(parts.FourLaneDrums.Difficulties, Is.EqualTo(DifficultyMask.Expert | DifficultyMask.ExpertPlus));
            }
        }

        #endregion

        #region Pro keys

        [Test]
        public void ProKeys_ParsesEachDifficultyTrack()
        {
            var (_, parts, _) = ScanMidi(
                MakeTrack("PART REAL_KEYS_E", Note(0, 100, PROKEYS_NOTE)),
                MakeTrack("PART REAL_KEYS_M", Note(0, 100, PROKEYS_NOTE)),
                MakeTrack("PART REAL_KEYS_H", Note(0, 100, PROKEYS_NOTE)),
                MakeTrack("PART REAL_KEYS_X", Note(0, 100, PROKEYS_NOTE)));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(parts.ProKeys[Difficulty.Easy], Is.True);
                Assert.That(parts.ProKeys[Difficulty.Medium], Is.True);
                Assert.That(parts.ProKeys[Difficulty.Hard], Is.True);
                Assert.That(parts.ProKeys[Difficulty.Expert], Is.True);
                Assert.That(parts.ProKeys[Difficulty.ExpertPlus], Is.False);
            }
        }

        #endregion

        #region Pro guitar

        [Test]
        public void ProGuitar_17Fret_ParsesAllFourDifficulties()
        {
            var (_, parts, _) = ScanMidi(
                MakeTrack("PART REAL_GUITAR",
                    Note(0, 100, PROGUITAR_EASY),
                    Note(200, 300, PROGUITAR_MEDIUM),
                    Note(400, 500, PROGUITAR_HARD),
                    Note(600, 700, PROGUITAR_EXPERT)));

            Assert.That(parts.ProGuitar_17Fret.Difficulties,
                Is.EqualTo(DifficultyMask.Easy | DifficultyMask.Medium | DifficultyMask.Hard | DifficultyMask.Expert));
        }

        [Test]
        public void ProGuitar_22Fret_AcceptsHigherVelocity()
        {
            const int HIGH_VELOCITY = 120;

            var (_, parts17, _) = ScanMidi(MakeTrack("PART REAL_GUITAR", Note(0, 100, PROGUITAR_EASY, velocity: HIGH_VELOCITY)));
            var (_, parts22, _) = ScanMidi(MakeTrack("PART REAL_GUITAR_22", Note(0, 100, PROGUITAR_EASY, velocity: HIGH_VELOCITY)));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(parts17.ProGuitar_17Fret.Difficulties, Is.EqualTo(DifficultyMask.None));
                Assert.That(parts22.ProGuitar_22Fret.Difficulties, Is.EqualTo(DifficultyMask.Easy));
            }
        }

        [Test]
        public void ProGuitar_VelocityBelowMinimum_Ignored()
        {
            const int LOW_VELOCITY = 99;

            var (_, parts17, _) = ScanMidi(MakeTrack("PART REAL_GUITAR", Note(0, 100, PROGUITAR_EASY, velocity: LOW_VELOCITY)));
            var (_, parts22, _) = ScanMidi(MakeTrack("PART REAL_GUITAR_22", Note(0, 100, PROGUITAR_EASY, velocity: LOW_VELOCITY)));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(parts17.ProGuitar_17Fret.Difficulties, Is.EqualTo(DifficultyMask.None));
                Assert.That(parts22.ProGuitar_22Fret.Difficulties, Is.EqualTo(DifficultyMask.None));
            }
        }

        [Test]
        public void ProGuitar_ArpeggioChannel_Ignored()
        {
            var (_, parts17, _) = ScanMidi(MakeTrack("PART REAL_GUITAR", Note(0, 100, PROGUITAR_EASY, channel: 1)));
            var (_, parts22, _) = ScanMidi(MakeTrack("PART REAL_GUITAR_22", Note(0, 100, PROGUITAR_EASY, channel: 1)));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(parts17.ProGuitar_17Fret.Difficulties, Is.EqualTo(DifficultyMask.None));
                Assert.That(parts22.ProGuitar_22Fret.Difficulties, Is.EqualTo(DifficultyMask.None));
            }
        }

        #endregion

        #region Vocals

        [Test]
        public void Vocals_Lead_RequiresPhrase()
        {
            var (_, withoutPhrase, _) = ScanMidi(MakeTrack("PART VOCALS", Note(0, 100, VOCAL_NOTE)));
            var (_, withPhrase, _) = ScanMidi(MakeTrack("PART VOCALS",
                Note(0, 100, VOCAL_NOTE),
                Note(10, 110, VOCAL_PHRASE)));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(withoutPhrase.LeadVocals[0], Is.False);
                Assert.That(withPhrase.LeadVocals[0], Is.True);
            }
        }

        [Test]
        public void Vocals_Lead_Percussion_DoesNotActivateWithoutPhrase()
        {
            var (_, parts, _) = ScanMidi(MakeTrack("PART VOCALS",
                Note(0, 100, VOCAL_NOTE),
                Note(200, 300, VOCAL_PERCUSSION)));

            Assert.That(parts.LeadVocals[0], Is.False);
        }

        [Test]
        public void Vocals_HarmonyTracks_RequireHarmony1()
        {
            var (_, harm2Only, _) = ScanMidi(MakeTrack("PART HARM2", Note(0, 100, VOCAL_NOTE)));

            var (_, allHarmonies, _) = ScanMidi(
                MakeTrack("PART HARM1",
                    Note(0, 100, VOCAL_NOTE),
                    Note(10, 110, VOCAL_PHRASE)),
                MakeTrack("PART HARM2", Note(0, 100, VOCAL_NOTE)),
                MakeTrack("PART HARM3", Note(0, 100, VOCAL_NOTE)));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(harm2Only.HarmonyVocals[0], Is.False);
                Assert.That(allHarmonies.HarmonyVocals[0], Is.True);
                Assert.That(allHarmonies.HarmonyVocals[1], Is.True);
                Assert.That(allHarmonies.HarmonyVocals[2], Is.True);
                Assert.That(allHarmonies.LeadVocals[0], Is.False);
            }
        }

        #endregion

        #region General scan behavior

        [Test]
        public void UnknownAndNamelessTracks_AreSkipped()
        {
            var (result, parts, _) = ScanMidi(
                MakeTrack("PART BANJO", Note(0, 100, FIVEFRET_EASY)),
                MakeNamelessTrack(Note(0, 100, FIVEFRET_EASY)));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.HasValue, Is.True);
                Assert.That(result.Value, Is.EqualTo(Resolution));
                Assert.That(parts.FiveFretGuitar.Difficulties, Is.EqualTo(DifficultyMask.None));
            }
        }

        [Test]
        public void MultipleTrackNamesInSameTrack_ReturnsError()
        {
            var (result, _, _) = ScanMidi(MakeTrack("PART GUITAR",
                TrackName(0, "PART BASS"),
                Note(0, 100, FIVEFRET_EASY)));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.HasValue, Is.False);
                Assert.That(result.Error, Is.EqualTo(ScanResult.MultipleMidiTrackNames));
            }
        }

        [Test]
        public void DuplicateTrackNameEvents_AreAllowed()
        {
            var (result, parts, _) = ScanMidi(MakeTrack("PART GUITAR",
                TrackName(0, "PART GUITAR"),
                Note(0, 100, FIVEFRET_EASY)));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.HasValue, Is.True);
                Assert.That(parts.FiveFretGuitar.Difficulties, Is.EqualTo(DifficultyMask.Beginner | DifficultyMask.Easy));
            }
        }

        [Test]
        public void DuplicateInstrumentTrack_IsIgnored()
        {
            var (_, parts, _) = ScanMidi(
                MakeTrack("PART GUITAR", Note(0, 100, FIVEFRET_EASY)),
                MakeTrack("PART GUITAR", Note(0, 100, FIVEFRET_MEDIUM)));

            Assert.That(parts.FiveFretGuitar.Difficulties, Is.EqualTo(DifficultyMask.Beginner | DifficultyMask.Easy));
        }

        [Test]
        public void InvalidResolution_ReturnsError()
        {
            var midi = MakeMidi(MakeTrack("PART GUITAR", Note(0, 100, FIVEFRET_EASY)));
            using var file = ToFixedArrayWithZeroResolution(midi);

            var (result, _, _) = TestSongEntry.ParseMidiForTest(file);

            using (Assert.EnterMultipleScope())
            {
                Assert.That(result.HasValue, Is.False);
                Assert.That(result.Error, Is.EqualTo(ScanResult.InvalidResolution));
            }
        }

        [Test]
        public void CorruptHeader_Throws()
        {
            var garbage = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x00, 0x01, 0x02, 0x03 };
            using var stream = new MemoryStream(garbage);
            using var file = FixedArray.Read(stream, garbage.Length);

            Assert.That(() => TestSongEntry.ParseMidiForTest(file),
                Throws.Exception.With.Message.Contains("MThd"));
        }

        #endregion

        #region Helpers

        private static (ScanExpected<long> Result, AvailableParts Parts, DrumsType DrumsType) ScanMidi(
            params TrackChunk[] tracks)
        {
            using var file = ToFixedArray(MakeMidi(tracks));
            return TestSongEntry.ParseMidiForTest(file);
        }

        private static (ScanExpected<long> Result, AvailableParts Parts, DrumsType DrumsType) ScanMidi(
            DrumsType initialDrumsType, params TrackChunk[] tracks)
        {
            using var file = ToFixedArray(MakeMidi(tracks));
            return TestSongEntry.ParseMidiForTest(file, initialDrumsType);
        }

        private static MidiFile MakeMidi(params TrackChunk[] tracks)
        {
            var midi = new MidiFile
            {
                TimeDivision = new TicksPerQuarterNoteTimeDivision(Resolution),
            };

            // Required: DryWetMidi 7.0's writer splits a *single*-chunk file into separate
            // meta/channel chunks when writing MultiTrack format, which would sever the
            // track-name event from its notes. Adding a sync track (ignored by ParseMidi:
            // "EVENTS" maps to MidiTrackType.Events, which has no switch case) keeps every
            // chunk intact, and mirrors real charts which always carry a tempo track.
            midi.Chunks.Add(new TrackChunk(new SequenceTrackNameEvent("EVENTS"), new SetTempoEvent(400000)));

            foreach (var track in tracks)
            {
                midi.Chunks.Add(track);
            }

            return midi;
        }

        private static FixedArray<byte> ToFixedArray(MidiFile midi)
        {
            using var stream = new MemoryStream();
            midi.Write(stream, MidiFileFormat.MultiTrack, null);
            stream.Position = 0;
            return FixedArray.Read(stream, stream.Length);
        }

        private static FixedArray<byte> ToFixedArrayWithZeroResolution(MidiFile midi)
        {
            using var stream = new MemoryStream();
            midi.Write(stream, MidiFileFormat.MultiTrack, null);
            var bytes = stream.ToArray();
            // Division field is the last two bytes of the 14-byte header, big-endian
            bytes[12] = 0;
            bytes[13] = 0;

            using var patched = new MemoryStream(bytes);
            return FixedArray.Read(patched, patched.Length);
        }

        private static TrackChunk MakeTrack(string trackName, params object[] eventItems)
        {
            return MakeTrack(trackName, FlattenEvents(eventItems));
        }

        private static TrackChunk MakeTrack(string trackName, IEnumerable<TimedMidiEvent> events)
        {
            var ordered = SortEvents(events);
            var chunk = new TrackChunk(new SequenceTrackNameEvent(trackName));
            chunk.Events.AddRange(ordered.Select(item => item.Event));
            return chunk;
        }

        private static TrackChunk MakeNamelessTrack(params object[] eventItems)
        {
            var ordered = SortEvents(FlattenEvents(eventItems));
            var chunk = new TrackChunk();
            chunk.Events.AddRange(ordered.Select(item => item.Event));
            return chunk;
        }

        private static TimedMidiEvent[] SortEvents(IEnumerable<TimedMidiEvent> events)
        {
            var ordered = events
                .OrderBy(item => item.Tick)
                .ThenBy(item => EventPriority(item.Event))
                .ThenBy(item => item.Event is NoteEvent note ? (int) note.NoteNumber : 0)
                .ToArray();

            long previousTick = 0;
            foreach (var (tick, midiEvent) in ordered)
            {
                midiEvent.DeltaTime = tick - previousTick;
                previousTick = tick;
            }

            return ordered;
        }

        private static int EventPriority(MidiEvent midiEvent)
        {
            return midiEvent switch
            {
                NoteOffEvent => 0,
                BaseTextEvent => 1,
                NoteOnEvent => 2,
                _ => 1,
            };
        }

        private static IEnumerable<TimedMidiEvent> FlattenEvents(IEnumerable<object> eventItems)
        {
            foreach (var item in eventItems)
            {
                switch (item)
                {
                    case TimedMidiEvent timedEvent:
                        yield return timedEvent;
                        break;
                    case IEnumerable<TimedMidiEvent> eventGroup:
                        foreach (var timedEvent in eventGroup)
                        {
                            yield return timedEvent;
                        }
                        break;
                    default:
                        throw new ArgumentException($"Unsupported MIDI test event item: {item.GetType()}");
                }
            }
        }

        private static TimedMidiEvent Text(long tick, string text)
        {
            return new TimedMidiEvent(tick, new MidiTextEvent(text));
        }

        private static TimedMidiEvent TrackName(long tick, string name)
        {
            return new TimedMidiEvent(tick, new SequenceTrackNameEvent(name));
        }

        private static TimedMidiEvent[] Note(long startTick, long endTick, int noteNumber,
            int velocity = DefaultVelocity, int channel = 0)
        {
            return new[]
            {
                new TimedMidiEvent(startTick, new NoteOnEvent
                {
                    NoteNumber = S(noteNumber),
                    Velocity = S(velocity),
                    Channel = F(channel),
                }),
                new TimedMidiEvent(endTick, new NoteOffEvent
                {
                    NoteNumber = S(noteNumber),
                    Channel = F(channel),
                }),
            };
        }

        #endregion
    }
}
