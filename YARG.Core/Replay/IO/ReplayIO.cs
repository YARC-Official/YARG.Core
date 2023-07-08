namespace YARG.Core.Replay.IO
{
    public enum ReplayReadResult
    {
        Valid,
        NotAReplay,
        InvalidVersion,
        Corrupted,
    }

    public static class ReplayIO
    {
        private const long MAGIC   = 0x59414C5047524159;
        private const int  VERSION = 23_07_07;


    }
}