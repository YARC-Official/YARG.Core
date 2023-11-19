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
    public sealed partial class SongMetadata
    {
        public SongChart? LoadChart()
        {
            if (IniData != null)
            {
                return LoadIniChart();
            }
            else if (RBData != null)
            {
                return LoadCONChart();
            }

            // This is an invalid state, notify about it
            string errorMessage = $"No chart data available for song {Name} - {Artist}!";
            YargTrace.Fail(errorMessage);
            throw new Exception(errorMessage);
        }

        private SongChart? LoadIniChart()
        {
            var stream = IniData!.GetChartStream();
            if (stream == null)
                return null;

            if (IniData.Type != ChartType.Chart)
                return SongChart.FromMidi(_parseSettings, MidFileLoader.LoadMidiFile(stream));

            using var reader = new StreamReader(stream);
            return SongChart.FromDotChart(_parseSettings, reader.ReadToEnd());
        }

        private SongChart? LoadCONChart()
        {
            MidiFile midi;
            ReadingSettings readingSettings = MidiSettingsLatin1.Instance; // RBCONs are always Latin-1
            // Read base MIDI
            using (var midiStream = RBData!.GetMidiStream())
            {
                if (midiStream == null)
                    return null;
                midi = MidiFile.Read(midiStream, readingSettings);
            }

            // Merge update MIDI
            var shared = RBData.SharedMetadata;
            if (shared.UpdateMidi != null)
            {
                if (!shared.UpdateMidi.IsStillValid())
                    return null;

                using var midiStream = new FileStream(shared.UpdateMidi.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
                var update = MidiFile.Read(midiStream, readingSettings);
                midi.Merge(update);
            }

            // Merge upgrade MIDI
            if (shared.Upgrade != null)
            {
                using var midiStream = shared.Upgrade.GetUpgradeMidiStream();
                if (midiStream == null)
                    return null;
                var update = MidiFile.Read(midiStream, readingSettings);
                midi.Merge(update);
            }

            return SongChart.FromMidi(_parseSettings, midi);
        }
    }
}
