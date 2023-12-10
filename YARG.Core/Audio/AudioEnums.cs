namespace YARG.Core.Audio
{
    public enum SongStem
    {
        Song,
        Guitar,
        Bass,
        Rhythm,
        Keys,
        Vocals,
        Vocals1,
        Vocals2,
        Drums, // TODO: Should this be equivalent to Drums1?
        Drums1,
        Drums2,
        Drums3,
        Drums4,
        Crowd,
        Preview
    }

    public enum SfxSample
    {
        NoteMiss,
        StarPowerAward,
        StarPowerGain,
        StarPowerDeploy,
        StarPowerRelease,
        Clap,
        StarGain,
        StarGold,
    }

    public enum DSPType
    {
        Gain,
    }
}
