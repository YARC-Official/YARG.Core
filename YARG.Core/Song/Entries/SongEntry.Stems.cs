using System;
using System.IO;

namespace YARG.Core.Song
{
    public enum StemSeparation
    {
        Present,
        Missing,
        Demucs,
    }

    public abstract partial class SongEntry
    {
        protected StemSeparation? _stemSeparation;
        protected bool _stemSilenceKnown;

        /// <summary>
        /// Missing means vocals, bass, and drums are all absent, so the full mix is the song stem.
        /// Demucs means those stems were generated and the song stem is Demucs "other".
        /// </summary>
        public StemSeparation GetStemSeparation()
        {
            return _stemSeparation ??= ComputeStemSeparation();
        }

        public void InvalidateStemSeparation()
        {
            _stemSeparation = null;
            _stemSilenceKnown = false;
        }

        /// <summary>
        /// CON songs often list drums, bass, and vocals even when those channels are silent
        /// and the full mix lives in the song stem. The library probes that audio once.
        /// </summary>
        public virtual bool NeedsStemSilenceProbe()
        {
            return false;
        }

        public void SetProbedStemSeparation(StemSeparation value)
        {
            if (value != StemSeparation.Demucs && ComputeStemSeparation() == StemSeparation.Demucs)
            {
                _stemSeparation = StemSeparation.Demucs;
                _stemSilenceKnown = true;
                return;
            }

            _stemSeparation = value;
            _stemSilenceKnown = true;
        }

        public virtual bool TryOpenOggAudio(out Stream audio, out int[] drumChannels, out int[] bassChannels, out int[] vocalChannels)
        {
            audio = null!;
            drumChannels = Array.Empty<int>();
            bassChannels = Array.Empty<int>();
            vocalChannels = Array.Empty<int>();
            return false;
        }

        public virtual bool TryExportFullMix(string destinationWithoutExtension, out string exportedPath)
        {
            exportedPath = string.Empty;
            return false;
        }

        /// <summary>
        /// Channel indices of the song stem inside a packed multi-channel mix.
        /// False when the exported file is already that mix.
        /// </summary>
        public virtual bool TryGetSongStemChannels(out int[] channels)
        {
            channels = Array.Empty<int>();
            return false;
        }

        public virtual bool TryInstallDemucsStems(string vocalsPath, string bassPath, string drumsPath, string otherPath)
        {
            return false;
        }

        protected virtual StemSeparation ComputeStemSeparation()
        {
            return StemSeparation.Present;
        }
    }
}
