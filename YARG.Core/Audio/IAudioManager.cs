using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using YARG.Core.Song;

namespace YARG.Core.Audio
{
    public interface IAudioManager
    {
        public AudioOptions Options { get; set; }

        public IList<string> SupportedFormats { get; }

        public bool IsAudioLoaded { get; }

        public float MasterVolume { get; set; }

        public void Initialize();
        public void Unload();

        public IList<IMicDevice> GetAllInputDevices();

        public Task<ISongContext> LoadSong(IDictionary<SongStem, Stream> stems, float speed);
        public Task<ISongContext> LoadMogg(Stream stream, List<MoggStemMap> stemMaps, float speed);
        public Task<ISongContext> LoadCustomAudio(Stream stream, float speed);

        public void ForceUnloadSong();

        public double GetVolumeSetting(SongStem stem);
        public void SetVolumeSetting(SongStem stem, float volume);
    }

    public interface ISfxManager<TSfxSample>
        where TSfxSample : Enum
    {
        public float SfxVolume { get; set; }

        public void LoadSoundEffects();
        public void UnloadSoundEffects();

        public void PlaySoundEffect(TSfxSample sample);
    }

    // For methods that are not specific to any particular audio interface
    public static class AudioManagerExtensions
    {
        public static Task<ISongContext> LoadAudio(this IAudioManager manager, SongMetadata song, float speed,
            params SongStem[] ignoreStems)
        {
            if (song.IniData != null)
            {
                return manager.LoadIniAudio(song.IniData, speed, ignoreStems);
            }
            else
            {
                return manager.LoadRBCONAudio(song.RBData!, speed, ignoreStems);
            }
        }

        public static Task<ISongContext> LoadCustomAudio(this IAudioManager manager, string file, float speed)
        {
            return manager.LoadCustomAudio(new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read), speed);
        }

        public static Task<ISongContext> LoadPreviewAudio(this IAudioManager manager, SongMetadata song, float speed,
            out bool usesPreviewFile)
        {
            usesPreviewFile = false;
            if (song.IniData != null)
            {
                var preview = song.IniData.GetPreviewAudioStream();
                if (preview != null)
                {
                    usesPreviewFile = true;
                    return manager.LoadCustomAudio(preview, 1);
                }
                return manager.LoadIniAudio(song.IniData, speed, SongStem.Crowd);
            }
            else
            {
                return manager.LoadRBCONAudio(song.RBData!, speed, SongStem.Crowd);
            }
        }

        private static Task<ISongContext> LoadIniAudio(this IAudioManager manager, SongMetadata.IIniMetadata iniData,
            float speed, params SongStem[] ignoreStems)
        {
            var stems = iniData.GetAudioStreams(ignoreStems);
            return manager.LoadSong(stems, speed);
        }

        private static Task<ISongContext> LoadRBCONAudio(this IAudioManager manager, SongMetadata.IRBCONMetadata rbData,
            float speed, params SongStem[] ignoreStems)
        {
            var rbmetadata = rbData.SharedMetadata;

            List<MoggStemMap> stemMaps = new();
            if (rbmetadata.DrumIndices != Array.Empty<int>() && !ignoreStems.Contains(SongStem.Drums))
            {
                switch (rbmetadata.DrumIndices.Length)
                {
                    //drum (0 1): stereo kit --> (0 1)
                    case 2:
                        stemMaps.Add(new(SongStem.Drums, rbmetadata.DrumIndices, rbmetadata.DrumStemValues));
                        break;
                    //drum (0 1 2): mono kick, stereo snare/kit --> (0) (1 2)
                    case 3:
                        stemMaps.Add(new(SongStem.Drums1, rbmetadata.DrumIndices[0..1], rbmetadata.DrumStemValues[0..2]));
                        stemMaps.Add(new(SongStem.Drums2, rbmetadata.DrumIndices[1..3], rbmetadata.DrumStemValues[2..6]));
                        break;
                    //drum (0 1 2 3): mono kick, mono snare, stereo kit --> (0) (1) (2 3)
                    case 4:
                        stemMaps.Add(new(SongStem.Drums1, rbmetadata.DrumIndices[0..1], rbmetadata.DrumStemValues[0..2]));
                        stemMaps.Add(new(SongStem.Drums2, rbmetadata.DrumIndices[1..2], rbmetadata.DrumStemValues[2..4]));
                        stemMaps.Add(new(SongStem.Drums3, rbmetadata.DrumIndices[2..4], rbmetadata.DrumStemValues[4..8]));
                        break;
                    //drum (0 1 2 3 4): mono kick, stereo snare, stereo kit --> (0) (1 2) (3 4)
                    case 5:
                        stemMaps.Add(new(SongStem.Drums1, rbmetadata.DrumIndices[0..1], rbmetadata.DrumStemValues[0..2]));
                        stemMaps.Add(new(SongStem.Drums2, rbmetadata.DrumIndices[1..3], rbmetadata.DrumStemValues[2..6]));
                        stemMaps.Add(new(SongStem.Drums3, rbmetadata.DrumIndices[3..5], rbmetadata.DrumStemValues[6..10]));
                        break;
                    //drum (0 1 2 3 4 5): stereo kick, stereo snare, stereo kit --> (0 1) (2 3) (4 5)
                    case 6:
                        stemMaps.Add(new(SongStem.Drums1, rbmetadata.DrumIndices[0..2], rbmetadata.DrumStemValues[0..4]));
                        stemMaps.Add(new(SongStem.Drums2, rbmetadata.DrumIndices[2..4], rbmetadata.DrumStemValues[4..8]));
                        stemMaps.Add(new(SongStem.Drums3, rbmetadata.DrumIndices[4..6], rbmetadata.DrumStemValues[8..12]));
                        break;
                }
            }

            if (rbmetadata.BassIndices != Array.Empty<int>() && !ignoreStems.Contains(SongStem.Bass))
                stemMaps.Add(new(SongStem.Bass, rbmetadata.BassIndices, rbmetadata.BassStemValues));

            if (rbmetadata.GuitarIndices != Array.Empty<int>() && !ignoreStems.Contains(SongStem.Guitar))
                stemMaps.Add(new(SongStem.Guitar, rbmetadata.GuitarIndices, rbmetadata.GuitarStemValues));

            if (rbmetadata.KeysIndices != Array.Empty<int>() && !ignoreStems.Contains(SongStem.Keys))
                stemMaps.Add(new(SongStem.Keys, rbmetadata.KeysIndices, rbmetadata.KeysStemValues));

            if (rbmetadata.VocalsIndices != Array.Empty<int>() && !ignoreStems.Contains(SongStem.Vocals))
                stemMaps.Add(new(SongStem.Vocals, rbmetadata.VocalsIndices, rbmetadata.VocalsStemValues));

            if (rbmetadata.TrackIndices != Array.Empty<int>() && !ignoreStems.Contains(SongStem.Song))
                stemMaps.Add(new(SongStem.Song, rbmetadata.TrackIndices, rbmetadata.TrackStemValues));

            if (rbmetadata.CrowdIndices != Array.Empty<int>() && !ignoreStems.Contains(SongStem.Crowd))
                stemMaps.Add(new(SongStem.Crowd, rbmetadata.CrowdIndices, rbmetadata.CrowdStemValues));

            var stream = rbData.GetMoggStream();
            if (stream is null)
                throw new Exception("Failed to load MOGG stream!");

            return manager.LoadMogg(stream, stemMaps, speed);
        }
    }
}