using Melanchall.DryWetMidi.Core;
using MoonscraperChartEditor.Song.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YARG.Core.Chart;
using YARG.Core.Extensions;

namespace YARG.Core.Song
{
    public partial class SongMetadata
    {
        public virtual SongChart? LoadChart()
        {
            // This is an invalid state, notify about it
            string errorMessage = $"No chart data available for song {Name} - {Artist}!";
            YargTrace.Fail(errorMessage);
            throw new Exception(errorMessage);
        }
    }
}
