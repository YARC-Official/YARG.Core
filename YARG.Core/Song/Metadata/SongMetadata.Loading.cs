using Melanchall.DryWetMidi.Core;
using MoonscraperChartEditor.Song.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Core.Extensions;
using YARG.Core.Venue;

namespace YARG.Core.Song
{
    [Flags]
    public enum LoadingOptions
    {
        Chart = 1 << 0,
        Audio = 1 << 1,
        AlbumArt = 1 << 2,
        BG_Image = 1 << 3,
        BG_Video = 1 << 4,
        BG_Venue = 1 << 5,
        Preview = 1 << 6,
        Milo = 1 << 7,
    }

    public class BackgroundResult
    {
        public readonly BackgroundType Type;
        public readonly Stream? Stream;

        public BackgroundResult(BackgroundType type, Stream? stream)
        {
            Type = type;
            Stream = stream;
        }
    }

    public struct LoadingContainer
    {
        public readonly LoadingOptions Options;
        private Task<SongChart?>? _chartTask;
        private Task<List<AudioChannel>>? _audioTask;
        private Task<byte[]?>? _albumTask;
        private Task<BackgroundResult?>? _backgroundTask;
        private Task<byte[]?>? _miloTask;

        public LoadingContainer(LoadingOptions options)
        {
            Options = options;
            _chartTask = null;
            _audioTask = null;
            _albumTask = null;
            _backgroundTask = null;
            _miloTask = null;
        }

        public Task<SongChart?>? Chart
        {
            readonly get => _chartTask;
            set
            {
                if (_chartTask != null)
                {
                    throw new InvalidOperationException();
                }
                _chartTask = value;
            }
        }

        public Task<List<AudioChannel>>? Audio
        {
            readonly get => _audioTask;
            set
            {
                if (_audioTask != null)
                {
                    throw new InvalidOperationException();
                }
                _audioTask = value;
            }
        }

        public Task<byte[]?>? Album
        {
            readonly get => _albumTask;
            set
            {
                if (_albumTask != null)
                {
                    throw new InvalidOperationException();
                }
                _albumTask = value;
            }
        }

        public Task<BackgroundResult?>? Background
        {
            readonly get => _backgroundTask;
            set
            {
                if (_backgroundTask != null)
                {
                    throw new InvalidOperationException();
                }
                _backgroundTask = value;
            }
        }

        public Task<byte[]?>? Milo
        {
            readonly get => _miloTask;
            set
            {
                if (_miloTask != null)
                {
                    throw new InvalidOperationException();
                }
                _miloTask = value;
            }
        }
    }

    public partial class SongMetadata
    {
        public LoadingContainer LoadMulti(LoadingOptions options, params SongStem[] ignoreStems)
        {
            var container = new LoadingContainer(options);
            if ((options & LoadingOptions.Chart) > 0)
            {
                container.Chart = Task.Run(LoadChart);
            }

            if ((options & LoadingOptions.Audio) > 0)
            {
                container.Audio = Task.Run(() => LoadAudioStreams(ignoreStems));
            }
            else if ((options & LoadingOptions.Preview) > 0)
            {
                container.Audio = Task.Run(LoadPreviewAudio);
            }

            if ((options & LoadingOptions.AlbumArt) > 0)
            {
                container.Album = Task.Run(LoadAlbumData);
            }

            if ((options & (LoadingOptions.BG_Image | LoadingOptions.BG_Video | LoadingOptions.BG_Venue)) > 0)
            {
                container.Background = Task.Run(() => LoadBackground(options));
            }

            if ((options & LoadingOptions.Milo) > 0)
            {
                container.Milo = Task.Run(LoadMiloData);
            }
            return container;
        }

        public virtual SongChart? LoadChart()
        {
            // This is an invalid state, notify about it
            string errorMessage = $"No chart data available for song {Name} - {Artist}!";
            YargTrace.Fail(errorMessage);
            throw new Exception(errorMessage);
        }

        public virtual List<AudioChannel> LoadAudioStreams(params SongStem[] ignoreStems)
        {
            // This is an invalid state, notify about it
            string errorMessage = $"No audio data available for song {Name} - {Artist}!";
            YargTrace.Fail(errorMessage);
            throw new Exception(errorMessage);
        }

        public virtual List<AudioChannel> LoadPreviewAudio()
        {
            // This is an invalid state, notify about it
            string errorMessage = $"No preview audio data available for song {Name} - {Artist}!";
            YargTrace.Fail(errorMessage);
            throw new Exception(errorMessage);
        }

        public virtual byte[]? LoadAlbumData()
        {
            // This is an invalid state, notify about it
            string errorMessage = $"No album data available for song {Name} - {Artist}!";
            YargTrace.Fail(errorMessage);
            throw new Exception(errorMessage);
        }

        public virtual BackgroundResult? LoadBackground(LoadingOptions options)
        {
            // This is an invalid state, notify about it
            string errorMessage = $"No background data available for song {Name} - {Artist}!";
            YargTrace.Fail(errorMessage);
            throw new Exception(errorMessage);
        }

        public virtual byte[]? LoadMiloData()
        {
            // This is an invalid state, notify about it
            string errorMessage = $"No milo data available for song {Name} - {Artist}!";
            YargTrace.Fail(errorMessage);
            throw new Exception(errorMessage);
        }
    }
}
