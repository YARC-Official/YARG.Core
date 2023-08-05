﻿using System;
using EasySharpIni;
using EasySharpIni.Converters;
using EasySharpIni.Models;
using YARG.Core.Chart;
using YARG.Core.Ini;

namespace YARG.Core.Song
{
    public partial class SongMetadata
    {
        private const string INTEGER_GENERAL_DEFAULT = "-1";
        private const string INTEGER_TIME_DEFAULT = "-1000";
        private const string BOOLEAN_GENERAL_DEFAULT = "false";

        private static readonly IntConverter IntConverter = new();
        private static readonly BooleanConverter BooleanConverter = new();

        private static readonly TimeConverter DecimalTimeConverter = new()
        {
            IntegerScaleFactor = 0.001,
            AllowTimeSpans = false,
        };

        private static readonly TimeConverter IntegerTimeConverter = new()
        {
            IntegerScaleFactor = 0.001,
            AllowTimeSpans = false,
            AllowDecimals = false,
        };

        private SongMetadata(IniSection section)
        {
            Name = section.GetField("name").Get();
            Artist = section.GetField("artist").Get();
            Album = section.GetField("album").Get();
            Genre = section.GetField("genre").Get();
            Year = section.GetField("year").Get();
            Charter = section.GetField("charter").Get();
            Source = section.GetField("icon").Get();

            // .ini songs are assumed to be masters and not covers
            IsMaster = true;

            LoadingPhrase = section.GetField("loading_phrase");

            PlaylistTrack = section.GetField("playlist_track", INTEGER_GENERAL_DEFAULT).Get(IntConverter);
            AlbumTrack = section.GetField("album_track", INTEGER_GENERAL_DEFAULT).Get(IntConverter);
            if (AlbumTrack < 1)
            {
                // Legacy tag
                AlbumTrack = section.GetField("track", INTEGER_GENERAL_DEFAULT).Get(IntConverter);
            }

            SongLength = section.GetField("song_length", INTEGER_TIME_DEFAULT).Get(IntegerTimeConverter);
            PreviewStart = section.GetField("preview_start_time", INTEGER_TIME_DEFAULT).Get(IntegerTimeConverter);
            PreviewEnd = section.GetField("preview_end_time", INTEGER_TIME_DEFAULT).Get(IntegerTimeConverter);

            ChartOffset = section.GetField("delay").Get(DecimalTimeConverter);

            VideoStartTime = section.GetField("video_start_time", "0").Get(IntegerTimeConverter);
            VideoEndTime = section.GetField("video_end_time", INTEGER_TIME_DEFAULT).Get(IntegerTimeConverter);

            BandDifficulty = section.GetField("diff_band", INTEGER_GENERAL_DEFAULT).Get(IntConverter);

            var drumsType = DrumsType.Unknown;
            if (section.GetField("pro_drums", BOOLEAN_GENERAL_DEFAULT).Get(BooleanConverter))
            {
                drumsType = DrumsType.FourLane;
            }
            else if (section.GetField("five_lane_drums", BOOLEAN_GENERAL_DEFAULT).Get(BooleanConverter))
            {
                drumsType = DrumsType.FiveLane;
            }

            int hopoThreshold = section.GetField("hopo_frequency", INTEGER_GENERAL_DEFAULT).Get(IntConverter);
            bool eighthNoteHopo = section.GetField("eighthnote_hopo", BOOLEAN_GENERAL_DEFAULT).Get(BooleanConverter);
            int susCutoffThreshold = section.GetField("sustain_cutoff_threshold", INTEGER_GENERAL_DEFAULT).Get(IntConverter);

            int starPowerNote = section.GetField("multiplier_note", INTEGER_GENERAL_DEFAULT).Get(IntConverter);
            if (starPowerNote < 0)
            {
                // Legacy tag from Phase Shift
                starPowerNote = section.GetField("star_power_note", INTEGER_GENERAL_DEFAULT).Get(IntConverter);
            }

            ParseSettings = new()
            {
                DrumsType = drumsType,

                HopoThreshold = hopoThreshold,
                EighthNoteHopo = eighthNoteHopo,
                SustainCutoffThreshold = susCutoffThreshold,

                StarPowerNote = starPowerNote,
            };
        }

        public static SongMetadata FromIni(string filePath)
        {
            // Had some reports that ini parsing might throw an exception, leaving this in for now
            // in as I don't know the cause just yet and I want to investigate it further. -Riley
            IniFile file;
            try
            {
                file = new IniFile(filePath);
                file.Parse();
            }
            catch (Exception ex)
            {
                YargTrace.LogException(ex, "Error while parsing song.ini!");
                return null;
            }

            // Check for the [song]/[Song] section
            string sectionName = file.ContainsSection("song") ? "song" : "Song";
            if (!file.ContainsSection(sectionName))
                return null;

            // Load metadata
            var section = file.GetSection(sectionName);
            return new SongMetadata(section);
        }
    }
}