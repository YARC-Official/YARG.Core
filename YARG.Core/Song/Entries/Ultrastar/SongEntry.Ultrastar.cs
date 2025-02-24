using Melanchall.DryWetMidi.Core;
using MoonscraperChartEditor.Song.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Core.Extensions;
using YARG.Core.IO;
using YARG.Core.IO.Ini;
using YARG.Core.IO.Ultrastar;
using YARG.Core.Logging;
using YARG.Core.Venue;

namespace YARG.Core.Song.Entries.Ultrastar
{
    internal class UltrastarEntry : SongEntry
    {
        public ChartFormat _chartFormat => ChartFormat.Ultrastar;
        private readonly DateTime _chartLastWrite;
        private string _location;
        private string _filename;
        private string _cover;
        private string _vocalStemLocation;
        private string _audioLocation;
        private string _video;
        private string _background;
        private UltrastarVersion _ultrastarVersion;

        public override string SortBasedLocation => _location;
        public override string ActualLocation => _location;

        public override EntryType SubType => EntryType.Ultrastar;

        private static readonly string[] ALBUMART_FILES;
        private static readonly string[] DEFAULT_ALBUMART_FILENAMES = new string[4]
        {
            "cover",
            "front",
            "album",
            "co"
        };

        private const int DEFAULT_INTENSITY = 3;

        static UltrastarEntry()
        {
            ALBUMART_FILES = new string[IMAGE_EXTENSIONS.Length * DEFAULT_ALBUMART_FILENAMES.Length];
            for (int j = 0; j < DEFAULT_ALBUMART_FILENAMES.Length; j++)
            {
                for (int i = 0; i < IMAGE_EXTENSIONS.Length; i++)
                {
                    ALBUMART_FILES[(j * IMAGE_EXTENSIONS.Length) + i] = DEFAULT_ALBUMART_FILENAMES[j] + IMAGE_EXTENSIONS[i];
                }
            }
        }

        private UltrastarEntry(string location, string filename, in DateTime chartLastWrite)
        {
            _location = location;
            _filename = filename;
            _chartLastWrite = chartLastWrite;
            _cover = string.Empty;
            _vocalStemLocation = string.Empty;
            _audioLocation = string.Empty;
            _video = string.Empty;
            _background = string.Empty;
        }

        internal override void Serialize(MemoryStream stream, CacheWriteIndices node)
        {
            stream.Write(_filename);
            stream.Write(_chartLastWrite.ToBinary(), Endianness.Little);

            base.Serialize(stream, node);

            stream.Write(_cover);
            stream.Write(_vocalStemLocation);
            stream.Write(_audioLocation);
            stream.Write(_video);
            stream.Write(_background);
        }

        protected new void Deserialize(ref FixedArrayStream stream, CacheReadStrings strings)
        {
            base.Deserialize(ref stream, strings);

            _cover = stream.ReadString();
            _vocalStemLocation = stream.ReadString();
            _audioLocation = stream.ReadString();
            _video = stream.ReadString();
            _background = stream.ReadString();
            (_parsedYear, _yearAsNumber) = ParseYear(_metadata.Year);
        }

        public override SongChart? LoadChart()
        {
            using var data = GetChartData(_filename);
            if (!data.IsAllocated)
            {
                return null;
            }

            var parseSettings = ParseSettings.Default;

            using var stream = data.ToReferenceStream();
            using var reader = new StreamReader(stream);
            return SongChart.FromUltrastar(parseSettings, reader.ReadToEnd());
        }

        public override StemMixer? LoadAudio(float speed, double volume, params SongStem[] ignoreStems)
        {
            var mixer = GlobalAudioHandler.CreateMixer(ToString(), speed, volume, false);
            if (mixer == null)
            {
                YargLogger.LogError("Failed to create mixer!");
                return null;
            }

            var subFiles = GetSubFiles();

            if (_audioLocation != string.Empty
                && (_vocalStemLocation == string.Empty || !ignoreStems.Contains(SongStem.Song)))
            {
                if (subFiles.TryGetValue(_audioLocation, out var file))
                {
                    var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
                    if (!mixer.AddChannel(SongStem.Song, stream))
                    {
                        stream.Dispose();
                        YargLogger.LogFormatError("Failed to load song file {0}", file);
                    }
                }
            }

            if (_vocalStemLocation != string.Empty && !ignoreStems.Contains(SongStem.Vocals))
            {
                if (subFiles.TryGetValue(_vocalStemLocation, out var file))
                {
                    var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
                    if (!mixer.AddChannel(SongStem.Vocals, stream))
                    {
                        stream.Dispose();
                        YargLogger.LogFormatError("Failed to load vocal stem file {0}", file);
                    }
                }
            }

            if (mixer.Channels.Count == 0)
            {
                YargLogger.LogError("Failed to add any stems!");
                mixer.Dispose();
                return null;
            }

            if (GlobalAudioHandler.LogMixerStatus)
            {
                YargLogger.LogFormatInfo("Loaded {0} stems", mixer.Channels.Count);
            }

            return mixer;
        }

        public override StemMixer? LoadPreviewAudio(float speed)
        {
            return LoadAudio(speed, 0, SongStem.Crowd);
        }

        protected FixedArray<byte> GetChartData(string filename)
        {
            var data = FixedArray<byte>.Null;
            string chartPath = Path.Combine(_location, filename);
            if (AbridgedFileInfo.Validate(chartPath, in _chartLastWrite))
            {
                data = FixedArray.LoadFile(chartPath);
            }
            return data;
        }

        public static ScanExpected<UltrastarEntry> ProcessNewEntry(string directory, FileInfo chartInfo, string defaultPlaylist)
        {
            UltrastarModifierCollection ultrastarModifiers = SongUltrastarHandler.ReadSongUltrastarFile(chartInfo.FullName);

            var entry = new UltrastarEntry(directory, chartInfo.Name, AbridgedFileInfo.NormalizedLastWrite(chartInfo));
            entry._metadata.Playlist = defaultPlaylist;

            using var file = FixedArray.LoadFile(chartInfo.FullName);
            var result = ScanChart(entry, in file, ultrastarModifiers);
            return result == ScanResult.Success ? entry : new ScanUnexpected(result);
        }

        public override YARGImage LoadAlbumData()
        {
            var subFiles = GetSubFiles();
            if (!string.IsNullOrEmpty(_cover) && subFiles.TryGetValue(_cover, out var cover))
            {
                var image = YARGImage.Load(cover);
                if (image.IsAllocated)
                {
                    return image;
                }
                YargLogger.LogFormatError("Image at {0} failed to load", cover);
            }

            foreach (string albumName in ALBUMART_FILES)
            {
                if (subFiles.TryGetValue(albumName, out var file))
                {
                    var image = YARGImage.Load(file);
                    if (image.IsAllocated)
                    {
                        return image;
                    }
                    YargLogger.LogFormatError("Image at {0} failed to load", file);
                }
            }
            return YARGImage.Null;
        }

        public override BackgroundResult? LoadBackground(BackgroundType options)
        {
            var subFiles = GetSubFiles();
            if ((options & BackgroundType.Yarground) > 0)
            {
                if (subFiles.TryGetValue("bg.yarground", out var file))
                {
                    var stream = File.OpenRead(file);
                    return new BackgroundResult(BackgroundType.Yarground, stream);
                }
            }

            if ((options & BackgroundType.Video) > 0)
            {
                if (subFiles.TryGetValue(_video, out var video))
                {
                    var stream = File.OpenRead(video);
                    return new BackgroundResult(BackgroundType.Video, stream);
                }

                foreach (var stem in BACKGROUND_FILENAMES)
                {
                    foreach (var format in VIDEO_EXTENSIONS)
                    {
                        if (subFiles.TryGetValue(stem + format, out var file))
                        {
                            var stream = File.OpenRead(file);
                            return new BackgroundResult(BackgroundType.Video, stream);
                        }
                    }
                }
            }

            if ((options & BackgroundType.Image) > 0)
            {
                if (subFiles.TryGetValue(_background, out var file))
                {
                    var image = YARGImage.Load(file!);
                    if (image.IsAllocated)
                    {
                        return new BackgroundResult(image);
                    }
                }
            }
            return null;
        }

        public override FixedArray<byte> LoadMiloData()
        {
            return FixedArray<byte>.Null;
        }

        public override DateTime GetLastWriteTime()
        {
            return _chartLastWrite;
        }

        private Dictionary<string, string> GetSubFiles()
        {
            Dictionary<string, string> files = new();
            if (Directory.Exists(_location))
            {
                foreach (var file in Directory.EnumerateFiles(_location))
                {
                    files.Add(file[(_location.Length + 1)..], file);
                }
            }
            return files;
        }

        protected internal static ScanResult ScanChart(UltrastarEntry entry, in FixedArray<byte> file, UltrastarModifierCollection modifiers)
        {
            ParseChart(entry, in file);

            if (!modifiers.Contains("title"))
            {
                return ScanResult.NoName;
            }

            if (modifiers.Extract("version", out string versionString))
            {
                entry._ultrastarVersion = SongUltrastarHandler.ConvertVersionToEnum(versionString);
            }
            else
            {
                entry._ultrastarVersion = UltrastarVersion.V1_0_0;
            }

            SongMetadata.FillFromUltrastar(ref entry._metadata, entry._ultrastarVersion, modifiers);

            (entry._parsedYear, entry._yearAsNumber) = ParseYear(entry._metadata.Year);
            entry._hash = HashWrapper.Hash(file.ReadOnlySpan);
            entry.SetSortStrings();

            UpdateEntryWithModifiers(ref entry, modifiers);

            if (entry._metadata.SongLength <= 0)
            {
                using var mixer = entry.LoadAudio(0, 0);
                if (mixer != null)
                {
                    entry._metadata.SongLength = (long) (mixer.Length * SongMetadata.MILLISECOND_FACTOR);

                    if (entry._metadata.Preview == (-1, -1))
                    {
                        // TODO: This is not technically correct, rather the preview audio starts 1/3rd of the way through the song
                        // in terms of the part of the song that is playable. So if a song is 300 seconds long, but vocals are
                        // from 0-200, the preview would be at 66.66s not 100s in.
                        // Unless specified otherwise in the ultrastar file.
                        entry._metadata.Preview.Start = entry._metadata.SongLength / 3;
                    }
                }
            }
            return ScanResult.Success;
        }

        private static void ParseChart(UltrastarEntry entry, in FixedArray<byte> file)
        {
            // If we have an ultrastar chart, we have lead vocals
            entry._parts.LeadVocals.ActivateSubtrack(0);
            entry._parts.LeadVocals.Intensity = DEFAULT_INTENSITY;

            var textContainer = new YARGTextContainer<byte>(file, Encoding.UTF8);
            bool foundHarmony = false;
            bool foundHarmony2 = false;
            string line;

            while ((line = YARGTextReader.PeekLine(ref textContainer)).Length > 0)
            {
                if (line.StartsWith("p1", StringComparison.CurrentCultureIgnoreCase))
                {
                    foundHarmony = true;
                }

                else if (line.StartsWith("p2", StringComparison.CurrentCultureIgnoreCase))
                {
                    foundHarmony = true;
                    foundHarmony2 = true;
                    break;
                }

                YARGTextReader.GotoNextLine(ref textContainer);
            }

            if (foundHarmony)
            {
                entry._parts.HarmonyVocals.ActivateSubtrack(0);
                entry._parts.HarmonyVocals.Intensity = DEFAULT_INTENSITY;

                if (foundHarmony2)
                {
                    entry._parts.HarmonyVocals.ActivateSubtrack(1);
                }
            }
        }

        private static void UpdateEntryWithModifiers(ref UltrastarEntry entry, UltrastarModifierCollection modifiers)
        {
            if (modifiers.Extract("audio", out string audioLocation))
            {
                entry._audioLocation = audioLocation;
            }

            if (modifiers.Extract("background", out string background))
            {
                entry._background = background;
            }

            if (modifiers.Extract("cover", out string cover))
            {
                entry._cover = cover;
            }

            if (modifiers.Extract("video", out string video))
            {
                entry._video = video;
            }
        }

        private static (string Parsed, int AsNumber) ParseYear(string str)
        {
            const int MINIMUM_YEAR_DIGITS = 4;
            for (int start = 0; start <= str.Length - MINIMUM_YEAR_DIGITS; ++start)
            {
                int curr = start;
                int number = 0;
                while (curr < str.Length && char.IsDigit(str[curr]))
                {
                    unchecked
                    {
                        number = 10 * number + str[curr] - '0';
                    }
                    ++curr;
                }

                if (curr >= start + MINIMUM_YEAR_DIGITS)
                {
                    return (str[start..curr], number);
                }
            }
            return (str, int.MaxValue);
        }

        public static UltrastarEntry? TryDeserialize(string baseDirectory, ref FixedArrayStream stream, CacheReadStrings strings)
        {
            string directory = Path.Combine(baseDirectory, stream.ReadString());
            var chartFileName = stream.ReadString();
            var chartLastWrite = DateTime.FromBinary(stream.Read<long>(Endianness.Little));
            if (!AbridgedFileInfo.Validate(Path.Combine(directory, chartFileName), chartLastWrite))
            {
                return null;
            }

            var entry = new UltrastarEntry(directory, chartFileName, in chartLastWrite);
            entry.Deserialize(ref stream, strings);
            return entry;
        }

        public static UltrastarEntry ForceDeserialize(string baseDirectory, ref FixedArrayStream stream, CacheReadStrings strings)
        {
            string directory = Path.Combine(baseDirectory, stream.ReadString());
            var chartFileName = stream.ReadString();
            var chartLastWrite = DateTime.FromBinary(stream.Read<long>(Endianness.Little));
            
            var entry = new UltrastarEntry(directory, chartFileName, in chartLastWrite);
            entry.Deserialize(ref stream, strings);
            return entry;
        }
    }
}
