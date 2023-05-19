namespace YARG.Core
{
    public enum InstrumentType
    {
        FiveFretGuitar,
        SixFretGuitar,
        Drums,
        FiveLaneDrums,
        ProGuitar,
        ProKeys,
        Vocals,
        Dj,
    }

    public enum Instrument
    {
        Guitar,
        Bass,
        Rhythm,
        Keys,
        GuitarCoop,
        
        Drums,
        ProDrums,
        
        ProGuitar,
        ProBass,
        ProKeys,
        
        Vocals,
        Harmony,

        SixFretGuitar,
        SixFretBass,
        SixFretRhythm,

        Invalid = -1,
    }

    public enum Difficulty
    {
        Easy,
        Medium,
        Hard,
        Expert,
        ExpertPlus
    }
}