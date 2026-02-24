namespace YARG.Core.Song
{
    public enum ChartFormat
    {
        Mid,
        Midi,
        Chart,
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
        Male,
        Female,
        Nonbinary,
        Other,
        Unspecified,
    }
}