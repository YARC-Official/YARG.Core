namespace YARG.Core.Audio
{
    public interface IStemController
    {
        void SetVolume(SongStem stem, double volume, double duration = 0);
        void SetReverb(SongStem stem, bool reverb);
        void SetWhammyPitch(SongStem stem, float percent);
    }
}
