using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.Audio;
using YARG.Core.IO;
using YARG.Core.Logging;

namespace YARG.Core.Song
{
    /// <summary>
    /// Files written when Demucs separates a full mix.
    /// Vocals, bass, and drums map to those stems. Demucs "other" becomes the song stem,
    /// which is the backing left after those parts are removed.
    /// </summary>
    internal static class DemucsStemFiles
    {
        public const long PlaceholderMaxBytes = 4096;
        public const string MarkerFileName = ".yarg-demucs";

        public static string GetSidecarDirectory(string actualLocation)
        {
            if (Directory.Exists(actualLocation))
            {
                return Path.Combine(actualLocation, "yarg-demucs");
            }

            return actualLocation + ".demucs";
        }

        public static bool HasMarker(string directory)
        {
            return !string.IsNullOrEmpty(directory) && File.Exists(Path.Combine(directory, MarkerFileName));
        }

        public static bool FolderMissesCoreStems(string directory)
        {
            if (!Directory.Exists(directory))
            {
                return true;
            }

            bool vocals = false;
            bool bass = false;
            bool drums = false;
            try
            {
                foreach (var file in Directory.EnumerateFiles(directory))
                {
                    var info = new FileInfo(file);
                    if (info.Length <= PlaceholderMaxBytes || !IsSupportedAudio(info.Name))
                    {
                        continue;
                    }

                    NoteStem(Path.GetFileNameWithoutExtension(info.Name), ref vocals, ref bass, ref drums);
                    if (vocals && bass && drums)
                    {
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, "Failed to check stem files");
                return false;
            }

            return !vocals && !bass && !drums;
        }

        public static bool ArchiveMissesCoreStems(Dictionary<string, SngFileListing> listings)
        {
            bool vocals = false;
            bool bass = false;
            bool drums = false;
            foreach (var pair in listings)
            {
                if (pair.Value.Length <= PlaceholderMaxBytes)
                {
                    continue;
                }

                NoteStem(Path.GetFileNameWithoutExtension(pair.Key), ref vocals, ref bass, ref drums);
                if (vocals && bass && drums)
                {
                    return false;
                }
            }

            return !vocals && !bass && !drums;
        }

        public static bool TryExportSongStem(string directory, string destinationWithoutExtension, out string exportedPath)
        {
            if (TryCopyStem(directory, "song.pre-demucs", destinationWithoutExtension, out exportedPath))
            {
                return true;
            }

            return TryCopyStem(directory, "song", destinationWithoutExtension, out exportedPath);
        }

        public static bool InstallIntoSongFolder(string directory, string vocalsPath, string bassPath, string drumsPath, string otherPath)
        {
            if (!SourcesExist(vocalsPath, bassPath, drumsPath, otherPath))
            {
                return false;
            }

            Directory.CreateDirectory(directory);
            bool magmaVocal = File.Exists(Path.Combine(directory, "magma.rbproj"))
                || File.Exists(Path.Combine(directory, "vocal.wav"));

            // Copy first so a failed read does not move the original full mix out of the way.
            string staging = Path.Combine(directory, ".demucs-staging");
            Directory.CreateDirectory(staging);
            try
            {
                File.Copy(otherPath, Path.Combine(staging, "song.wav"), true);
                File.Copy(vocalsPath, Path.Combine(staging, "vocals.wav"), true);
                File.Copy(bassPath, Path.Combine(staging, "bass.wav"), true);
                File.Copy(drumsPath, Path.Combine(staging, "drums.wav"), true);

                RetireStem(directory, "song");
                RetireStem(directory, "vocals");
                RetireStem(directory, "vocal");
                RetireStem(directory, "vocals_1");
                RetireStem(directory, "vocals_2");
                RetireStem(directory, "bass");
                RetireStem(directory, "drums");
                RetireStem(directory, "drums_1");
                RetireStem(directory, "drums_2");
                RetireStem(directory, "drums_3");
                RetireStem(directory, "drums_4");
                RetirePlaceholder(directory, "guitar");
                RetirePlaceholder(directory, "keys");
                RetirePlaceholder(directory, "rhythm");

                File.Copy(Path.Combine(staging, "song.wav"), Path.Combine(directory, "song.wav"), true);
                File.Copy(Path.Combine(staging, "vocals.wav"), Path.Combine(directory, "vocals.wav"), true);
                File.Copy(Path.Combine(staging, "bass.wav"), Path.Combine(directory, "bass.wav"), true);
                File.Copy(Path.Combine(staging, "drums.wav"), Path.Combine(directory, "drums.wav"), true);
                if (magmaVocal)
                {
                    File.Copy(Path.Combine(staging, "vocals.wav"), Path.Combine(directory, "vocal.wav"), true);
                }

                File.WriteAllText(Path.Combine(directory, MarkerFileName), "demucs");
                return true;
            }
            finally
            {
                try
                {
                    if (Directory.Exists(staging))
                    {
                        Directory.Delete(staging, true);
                    }
                }
                catch (Exception ex)
                {
                    YargLogger.LogException(ex, "Failed to delete Demucs staging files");
                }
            }
        }

        public static bool InstallSidecar(string directory, string vocalsPath, string bassPath, string drumsPath, string otherPath)
        {
            if (!SourcesExist(vocalsPath, bassPath, drumsPath, otherPath))
            {
                return false;
            }

            Directory.CreateDirectory(directory);
            File.Copy(vocalsPath, Path.Combine(directory, "vocals.wav"), true);
            File.Copy(bassPath, Path.Combine(directory, "bass.wav"), true);
            File.Copy(drumsPath, Path.Combine(directory, "drums.wav"), true);
            File.Copy(otherPath, Path.Combine(directory, "song.wav"), true);
            File.WriteAllText(Path.Combine(directory, MarkerFileName), "demucs");
            return true;
        }

        public static bool TryLoadMixer(string directory, string name, float speed, double volume, bool clampStemVolume,
            SongStem[] ignoreStems, out StemMixer? mixer)
        {
            mixer = null;
            if (!HasMarker(directory))
            {
                return false;
            }

            mixer = GlobalAudioHandler.CreateMixer(name, speed, volume, clampStemVolume, normalize: true);
            if (mixer == null)
            {
                YargLogger.LogError("Failed to create mixer for Demucs stems");
                return true;
            }

            TryAdd(mixer, Path.Combine(directory, "song.wav"), SongStem.Song, ignoreStems);
            TryAdd(mixer, Path.Combine(directory, "vocals.wav"), SongStem.Vocals, ignoreStems);
            TryAdd(mixer, Path.Combine(directory, "bass.wav"), SongStem.Bass, ignoreStems);
            TryAdd(mixer, Path.Combine(directory, "drums.wav"), SongStem.Drums1, ignoreStems);

            if (mixer.Channels.Count == 0)
            {
                YargLogger.LogError("Demucs stem files could not be loaded");
                mixer.Dispose();
                mixer = null;
            }

            return true;
        }

        private static void TryAdd(StemMixer mixer, string path, SongStem stem, SongStem[] ignoreStems)
        {
            if (ignoreStems != null && Array.IndexOf(ignoreStems, stem) >= 0)
            {
                return;
            }

            if (!File.Exists(path))
            {
                return;
            }

            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
            if (!mixer.AddChannel(stream, stem))
            {
                stream.Dispose();
                YargLogger.LogFormatError("Failed to load Demucs stem {0}", path);
            }
        }

        private static bool TryCopyStem(string directory, string stemName, string destinationWithoutExtension, out string exportedPath)
        {
            exportedPath = string.Empty;
            string? bestPath = null;
            long bestLength = PlaceholderMaxBytes;
            foreach (var format in IniAudio.SupportedFormats)
            {
                string path = Path.Combine(directory, stemName + format);
                if (!File.Exists(path))
                {
                    continue;
                }

                long length = new FileInfo(path).Length;
                if (length <= bestLength)
                {
                    continue;
                }

                bestPath = path;
                bestLength = length;
                exportedPath = destinationWithoutExtension + format;
            }

            if (bestPath == null)
            {
                exportedPath = string.Empty;
                return false;
            }

            File.Copy(bestPath, exportedPath, true);
            return true;
        }

        private static void RetireStem(string directory, string stemName)
        {
            foreach (var format in IniAudio.SupportedFormats)
            {
                string path = Path.Combine(directory, stemName + format);
                if (!File.Exists(path))
                {
                    continue;
                }

                string backup = Path.Combine(directory, stemName + ".pre-demucs" + format);
                if (!File.Exists(backup))
                {
                    File.Move(path, backup);
                }
                else
                {
                    File.Delete(path);
                }
            }
        }

        private static void RetirePlaceholder(string directory, string stemName)
        {
            foreach (var format in IniAudio.SupportedFormats)
            {
                string path = Path.Combine(directory, stemName + format);
                if (!File.Exists(path) || new FileInfo(path).Length > PlaceholderMaxBytes)
                {
                    continue;
                }

                string backup = Path.Combine(directory, stemName + ".pre-demucs" + format);
                if (!File.Exists(backup))
                {
                    File.Move(path, backup);
                }
                else
                {
                    File.Delete(path);
                }
            }
        }

        private static bool SourcesExist(string vocalsPath, string bassPath, string drumsPath, string otherPath)
        {
            return File.Exists(vocalsPath) && File.Exists(bassPath) && File.Exists(drumsPath) && File.Exists(otherPath);
        }

        private static bool IsSupportedAudio(string fileName)
        {
            foreach (var format in IniAudio.SupportedFormats)
            {
                if (fileName.EndsWith(format, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void NoteStem(string stem, ref bool vocals, ref bool bass, ref bool drums)
        {
            if (IsVocal(stem))
            {
                vocals = true;
            }
            else if (stem.Equals("bass", StringComparison.OrdinalIgnoreCase))
            {
                bass = true;
            }
            else if (stem.Equals("drums", StringComparison.OrdinalIgnoreCase) ||
                     stem.StartsWith("drums_", StringComparison.OrdinalIgnoreCase))
            {
                drums = true;
            }
        }

        private static bool IsVocal(string stem)
        {
            return stem.Equals("vocal", StringComparison.OrdinalIgnoreCase)
                || stem.Equals("vocals", StringComparison.OrdinalIgnoreCase)
                || stem.StartsWith("vocals_", StringComparison.OrdinalIgnoreCase);
        }
    }
}
