using System;
using System.Collections.Generic;

namespace YARG.Core.Audio
{
    public static class AudioHelpers
    {
        public static readonly Dictionary<string, SongStem> SupportedStems = new()
        {
            { "song",     SongStem.Song    },
            { "guitar",   SongStem.Guitar  },
            { "bass",     SongStem.Bass    },
            { "rhythm",   SongStem.Rhythm  },
            { "keys",     SongStem.Keys    },
            { "vocals",   SongStem.Vocals  },
            { "vocals_1", SongStem.Vocals1 },
            { "vocals_2", SongStem.Vocals2 },
            { "drums",    SongStem.Drums   },
            { "drums_1",  SongStem.Drums1  },
            { "drums_2",  SongStem.Drums2  },
            { "drums_3",  SongStem.Drums3  },
            { "drums_4",  SongStem.Drums4  },
            { "crowd",    SongStem.Crowd   },
            // "preview"
        };

        public static readonly List<SongStem> PitchBendAllowedStems = new()
        {
            SongStem.Guitar,
            SongStem.Bass,
            SongStem.Rhythm,
        };

        public static SongStem GetStemFromName(string stem)
        {
            return stem.ToLowerInvariant() switch
            {
                "song"       => SongStem.Song,
                "guitar"     => SongStem.Guitar,
                "bass"       => SongStem.Bass,
                "rhythm"     => SongStem.Rhythm,
                "keys"       => SongStem.Keys,
                "vocals"     => SongStem.Vocals,
                "vocals_1"   => SongStem.Vocals1,
                "vocals_2"   => SongStem.Vocals2,
                "drums"      => SongStem.Drums,
                "drums_1"    => SongStem.Drums1,
                "drums_2"    => SongStem.Drums2,
                "drums_3"    => SongStem.Drums3,
                "drums_4"    => SongStem.Drums4,
                "crowd"      => SongStem.Crowd,
                // "preview" => SongStem.Preview,
                _ => SongStem.Song,
            };
        }

        public static SongStem ToSongStem(this Instrument instrument)
        {
            return instrument switch
            {
                Instrument.FiveFretGuitar or
                Instrument.SixFretGuitar or
                Instrument.ProGuitar_17Fret or
                Instrument.ProGuitar_22Fret => SongStem.Guitar,

                Instrument.FiveFretBass or
                Instrument.SixFretBass or
                Instrument.ProBass_17Fret or
                Instrument.ProBass_22Fret => SongStem.Bass,

                Instrument.FiveFretRhythm or
                Instrument.SixFretRhythm or
                Instrument.FiveFretCoopGuitar or
                Instrument.SixFretCoopGuitar => SongStem.Rhythm,

                Instrument.Keys or
                Instrument.ProKeys => SongStem.Keys,

                Instrument.ProDrums or
                Instrument.FourLaneDrums or
                Instrument.FiveLaneDrums => SongStem.Drums,

                Instrument.Vocals or
                Instrument.Harmony => SongStem.Vocals,

                _ => throw new Exception("Unreachable.")
            };
        }

        // Note that ordering is important here, entries must be in the same order as the corresponding enum
        // This annoys me, but it's still better than what we had before where we had the same issue, but in several
        // different places.
        public static readonly IList<Sample<SfxSample>> SfxSamples = new[]
        {
            new Sample<SfxSample>(SfxSample.NoteMiss, "note_miss", 0.55f),
            new Sample<SfxSample>(SfxSample.StarPowerAward, "starpower_award", 0.5f),
            new Sample<SfxSample>(SfxSample.StarPowerGain, "starpower_gain", 0.5f),
            new Sample<SfxSample>(SfxSample.StarPowerDeploy, "starpower_deploy", 0.4f),
            new Sample<SfxSample>(SfxSample.StarPowerDeployCrowd, "overdrive_deploy_crowd", 0.4f),
            new Sample<SfxSample>(SfxSample.StarPowerRelease, "starpower_release", 0.5f),
            new Sample<SfxSample>(SfxSample.Clap, "clap", 0.16f),
            new Sample<SfxSample>(SfxSample.StarGain, "star"),
            new Sample<SfxSample>(SfxSample.StarGold, "star_gold"),
            new Sample<SfxSample>(SfxSample.Overstrum1, "overstrum_1", 0.4f),
            new Sample<SfxSample>(SfxSample.Overstrum2, "overstrum_2", 0.4f),
            new Sample<SfxSample>(SfxSample.Overstrum3, "overstrum_3", 0.4f),
            new Sample<SfxSample>(SfxSample.Overstrum4, "overstrum_4", 0.4f),
            new Sample<SfxSample>(SfxSample.CrowdOpen1, "crowd_open_1", 1.0f, true),
            new Sample<SfxSample>(SfxSample.CrowdOpen2, "crowd_open_2", 1.0f, true),
            new Sample<SfxSample>(SfxSample.CrowdStart, "crowd_start_1"),
            new Sample<SfxSample>(SfxSample.CrowdStart2, "crowd_start_2", 0.6f),
            new Sample<SfxSample>(SfxSample.CrowdStart3, "crowd_start_3", 0.7f),
            new Sample<SfxSample>(SfxSample.CrowdEnd1, "crowd_end_1", 1.0f, true),
            new Sample<SfxSample>(SfxSample.CrowdEnd2, "crowd_end_2", 1.0f, true),
            new Sample<SfxSample>(SfxSample.Chatter, "chatter", 0.6f),
        };

        public static readonly IList<Sample<DrumSfxSample>> DrumSamples = new[]
        {
            new Sample<DrumSfxSample>(DrumSfxSample.Vel0Pad0Smp0, "vel0pad0smp0"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel0Pad0Smp1, "vel0pad0smp1"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel0Pad0Smp2, "vel0pad0smp2"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel0Pad1Smp0, "vel0pad1smp0"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel0Pad1Smp1, "vel0pad1smp1"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel0Pad1Smp2, "vel0pad1smp2"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel0Pad2Smp0, "vel0pad2smp0"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel0Pad2Smp1, "vel0pad2smp1"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel0Pad2Smp2, "vel0pad2smp2"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel0Pad3Smp0, "vel0pad3smp0"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel0Pad3Smp1, "vel0pad3smp1"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel0Pad3Smp2, "vel0pad3smp2"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel0Pad4Smp0, "vel0pad4smp0"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel0Pad4Smp1, "vel0pad4smp1"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel0Pad4Smp2, "vel0pad4smp2"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel0Pad5Smp0, "vel0pad5smp0"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel0Pad5Smp1, "vel0pad5smp1"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel0Pad5Smp2, "vel0pad5smp2"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel0Pad6Smp0, "vel0pad6smp0"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel0Pad6Smp1, "vel0pad6smp1"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel0Pad6Smp2, "vel0pad6smp2"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel0Pad7Smp0, "vel0pad7smp0"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel0Pad7Smp1, "vel0pad7smp1"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel0Pad7Smp2, "vel0pad7smp2"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel1Pad0Smp0, "vel1pad0smp0"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel1Pad0Smp1, "vel1pad0smp1"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel1Pad0Smp2, "vel1pad0smp2"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel1Pad1Smp0, "vel1pad1smp0"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel1Pad1Smp1, "vel1pad1smp1"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel1Pad1Smp2, "vel1pad1smp2"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel1Pad2Smp0, "vel1pad2smp0"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel1Pad2Smp1, "vel1pad2smp1"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel1Pad2Smp2, "vel1pad2smp2"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel1Pad3Smp0, "vel1pad3smp0"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel1Pad3Smp1, "vel1pad3smp1"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel1Pad3Smp2, "vel1pad3smp2"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel1Pad4Smp0, "vel1pad4smp0"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel1Pad4Smp1, "vel1pad4smp1"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel1Pad4Smp2, "vel1pad4smp2"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel1Pad5Smp0, "vel1pad5smp0"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel1Pad5Smp1, "vel1pad5smp1"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel1Pad5Smp2, "vel1pad5smp2"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel1Pad6Smp0, "vel1pad6smp0"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel1Pad6Smp1, "vel1pad6smp1"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel1Pad6Smp2, "vel1pad6smp2"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel1Pad7Smp0, "vel1pad7smp0"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel1Pad7Smp1, "vel1pad7smp1"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel1Pad7Smp2, "vel1pad7smp2"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel2Pad0Smp0, "vel2pad0smp0"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel2Pad0Smp1, "vel2pad0smp1"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel2Pad0Smp2, "vel2pad0smp2"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel2Pad1Smp0, "vel2pad1smp0"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel2Pad1Smp1, "vel2pad1smp1"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel2Pad1Smp2, "vel2pad1smp2"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel2Pad2Smp0, "vel2pad2smp0"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel2Pad2Smp1, "vel2pad2smp1"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel2Pad2Smp2, "vel2pad2smp2"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel2Pad3Smp0, "vel2pad3smp0"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel2Pad3Smp1, "vel2pad3smp1"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel2Pad3Smp2, "vel2pad3smp2"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel2Pad4Smp0, "vel2pad4smp0"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel2Pad4Smp1, "vel2pad4smp1"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel2Pad4Smp2, "vel2pad4smp2"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel2Pad5Smp0, "vel2pad5smp0"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel2Pad5Smp1, "vel2pad5smp1"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel2Pad5Smp2, "vel2pad5smp2"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel2Pad6Smp0, "vel2pad6smp0"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel2Pad6Smp1, "vel2pad6smp1"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel2Pad6Smp2, "vel2pad6smp2"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel2Pad7Smp0, "vel2pad7smp0"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel2Pad7Smp1, "vel2pad7smp1"),
            new Sample<DrumSfxSample>(DrumSfxSample.Vel2Pad7Smp2, "vel2pad7smp2"),
        };

        public static readonly IList<Sample<VoxSample>> VoxSamples = new[]
        {
            new Sample<VoxSample>(VoxSample.FullCombo, "FullCombo"),
            new Sample<VoxSample>(VoxSample.Times2, "Times2"),
            new Sample<VoxSample>(VoxSample.Times3, "Times3"),
            new Sample<VoxSample>(VoxSample.Times4, "Times4"),
            new Sample<VoxSample>(VoxSample.Times5, "Times5"),
            new Sample<VoxSample>(VoxSample.Times6, "Times6"),
            new Sample<VoxSample>(VoxSample.TimesMany, "TimesMany"),
            new Sample<VoxSample>(VoxSample.FullBandFullCombo, "FullBandFullCombo"),
            new Sample<VoxSample>(VoxSample.HighScore, "HighScore"),
            new Sample<VoxSample>(VoxSample.FailSound, "FailSound"),
        };

        public class Sample<T>
        {
            public T             Kind;
            public string        File;
            public float         Volume;
            public bool          CanLoop;
            public bool          IsPlaying;

            public Sample(T kind, string file, float volume = 1.0f, bool canLoop = false)
            {
                Kind = kind;
                File = file;
                Volume = volume;
                CanLoop = canLoop;
                IsPlaying = false;
            }

            public Sample(T kind, string file, bool canLoop) : this(kind, file, 1.0f, canLoop)
            {
            }
        }
    }
}