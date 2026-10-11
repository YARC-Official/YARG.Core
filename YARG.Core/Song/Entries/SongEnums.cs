namespace YARG.Core.Song
{
    public enum ChartFormat
    {
        Mid,
        Midi,
        Chart,
        UltraStar
    };

    public enum EntryType
    {
        Ini,
        Sng,
        ExCON,
        CON,
    }

    public enum VocalGender : byte
    {
        Female,
        Male,
        Nonbinary,
        Other,
        Unspecified,
    }
}
