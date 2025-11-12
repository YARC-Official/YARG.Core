using System;
using System.Text;
using YARG.Core.Song;

namespace YARG.Core.IO
{
    public struct DTAEntry
    {
        public static readonly DTAEntry Empty = new()
        {
            Intensities = RBIntensities.Default,
            MetadataEncoding = YARGTextReader.UTF8Strict
        };

        public TextSpan? Name;
        public TextSpan? Artist;
        public TextSpan? Album;
        public string? Genre;
        public string? Charter;
        public string? Source;
        public string? Playlist;
        public string? LoadingPhrase;
        public int? YearAsNumber;

        public long? SongLength;
        public SongRating? SongRating;

        public (long Start, long End)? Preview;
        public bool? IsMaster;
        public bool? UGC;

        public int? AlbumTrack;

        public string? SongID;
        public uint? AnimTempo;
        public string? DrumBank;
        public string? VocalPercussionBank;
        public uint? VocalSongScrollSpeed;
        public VocalGender? VocalGender;
        public uint? VocalTonicNote;
        public SongTonality? SongTonality;
        public int? TuningOffsetCents;
        public uint? VenueVersion;

        public string[]? Soloes;
        public string[]? VideoVenues;

        public int[]? RealGuitarTuning;
        public int[]? RealBassTuning;

        public RBAudio<int>? Indices;
        public int[]? CrowdChannels;

        public string? Location;
        public float[]? Pans;
        public float[]? Volumes;
        public float[]? Cores;
        public long? HopoThreshold;
        public Encoding MetadataEncoding;

        public RBIntensities Intensities;
        public bool DiscUpdate;

        public void LoadData(string nodename, YARGTextContainer<byte> container)
        {
            while (YARGDTAReader.StartNode(ref container))
            {
                string name = YARGDTAReader.GetNameOfNode(ref container, false);
                switch (name)
                {
                    case "name": Name = YARGDTAReader.ExtractTextBytes(ref container); break;
                    case "artist": Artist = YARGDTAReader.ExtractTextBytes(ref container); break;
                    case "master": IsMaster = YARGDTAReader.ExtractBoolean_FlippedDefault(ref container); break;
                    case "context":
                        unsafe
                        {
                            int scopeLevel = 1;
                            while (!container.IsAtEnd())
                            {
                                int ch = container.Get();
                                if (ch == ')')
                                {
                                    --scopeLevel;
                                    break;
                                }
                                ++container.Position;

                                switch (ch)
                                {
                                    case '{': ++scopeLevel; break;
                                    case '}': --scopeLevel; break;
                                }
                            }

                            if (scopeLevel != 0)
                            {
                                throw new Exception("Invalid Context - Unbalanced brace count!");
                            }
                            break;
                        }
                    case "song":
                        while (YARGDTAReader.StartNode(ref container))
                        {
                            string descriptor = YARGDTAReader.GetNameOfNode(ref container, false);
                            switch (descriptor)
                            {
                                case "name": Location = YARGDTAReader.ExtractText(ref container); break;
                                case "tracks":
                                {
                                    var indices = RBAudio<int>.Empty;
                                    while (YARGDTAReader.StartNode(ref container))
                                    {
                                        while (YARGDTAReader.StartNode(ref container))
                                        {
                                            switch (YARGDTAReader.GetNameOfNode(ref container, false))
                                            {
                                                case "drum": indices.Drums = YARGDTAReader.ExtractIntegerArray<int>(ref container); break;
                                                case "bass": indices.Bass = YARGDTAReader.ExtractIntegerArray<int>(ref container); break;
                                                case "guitar": indices.Guitar = YARGDTAReader.ExtractIntegerArray<int>(ref container); break;
                                                case "keys": indices.Keys = YARGDTAReader.ExtractIntegerArray<int>(ref container); break;
                                                case "vocals": indices.Vocals = YARGDTAReader.ExtractIntegerArray<int>(ref container); break;
                                            }
                                            YARGDTAReader.EndNode(ref container);
                                        }
                                        YARGDTAReader.EndNode(ref container);
                                    }
                                    Indices = indices;
                                    break;
                                }
                                case "crowd_channels": CrowdChannels = YARGDTAReader.ExtractIntegerArray<int>(ref container); break;
                                //case "vocal_parts": VocalParts = YARGDTAReader.ExtractUInt16(ref container); break;
                                case "pans": Pans = YARGDTAReader.ExtractFloatArray(ref container); break;
                                case "vols": Volumes = YARGDTAReader.ExtractFloatArray(ref container); break;
                                case "cores": Cores = YARGDTAReader.ExtractFloatArray(ref container); break;
                                case "hopo_threshold": HopoThreshold = YARGDTAReader.ExtractInteger<long>(ref container); break;
                            }
                            YARGDTAReader.EndNode(ref container);
                        }
                        break;
                    case "song_vocals": while (YARGDTAReader.StartNode(ref container)) YARGDTAReader.EndNode(ref container); break;
                    case "song_scroll_speed": VocalSongScrollSpeed = YARGDTAReader.ExtractInteger<uint>(ref container); break;
                    case "tuning_offset_cents": TuningOffsetCents = YARGDTAReader.ExtractInteger<int>(ref container); break;
                    case "bank": VocalPercussionBank = YARGDTAReader.ExtractText(ref container); break;
                    case "anim_tempo":
                    {
                        string val = YARGDTAReader.ExtractText(ref container);
                        AnimTempo = val switch
                        {
                            "kTempoSlow" => 16,
                            "kTempoMedium" => 32,
                            "kTempoFast" => 64,
                            _ => uint.Parse(val)
                        };
                        break;
                    }
                    case "preview": Preview = (YARGDTAReader.ExtractInteger<long>(ref container), YARGDTAReader.ExtractInteger<long>(ref container)); break;
                    case "rank":
                        while (YARGDTAReader.StartNode(ref container))
                        {
                            string descriptor = YARGDTAReader.GetNameOfNode(ref container, false);
                            int diff = YARGDTAReader.ExtractInteger<int>(ref container);
                            switch (descriptor)
                            {
                                case "drum":
                                case "drums": Intensities.FourLaneDrums = (short) diff; break;

                                case "guitar": Intensities.FiveFretGuitar = (short) diff; break;
                                case "bass": Intensities.FiveFretBass = (short) diff; break;
                                case "vocals": Intensities.LeadVocals = (short) diff; break;
                                case "keys": Intensities.Keys = (short) diff; break;

                                case "realGuitar":
                                case "real_guitar": Intensities.ProGuitar = (short) diff; break;

                                case "realBass":
                                case "real_bass": Intensities.ProBass = (short) diff; break;

                                case "realKeys":
                                case "real_keys": Intensities.ProKeys = (short) diff; break;

                                case "realDrums":
                                case "real_drums": Intensities.ProDrums = (short) diff; break;

                                case "harmVocals":
                                case "vocal_harm": Intensities.HarmonyVocals = (short) diff; break;

                                case "band": Intensities.Band = (short) diff; break;
                            }
                            YARGDTAReader.EndNode(ref container);
                        }
                        break;
                    case "solo": Soloes = YARGDTAReader.ExtractStringArray(ref container); break;
                    case "genre": Genre = YARGDTAReader.ExtractText(ref container); break;
                    case "decade": /*Decade = YARGDTAReader.ExtractText(ref container);*/ break;
                    case "vocal_gender": VocalGender = YARGDTAReader.ExtractText(ref container) == "male" ? Song.VocalGender.Male : Song.VocalGender.Female; break;
                    case "format": /*Format = YARGDTAReader.Extract<uint>(ref container);*/ break;
                    case "version": VenueVersion = YARGDTAReader.ExtractInteger<uint>(ref container); break;
                    case "fake": /*IsFake = YARGDTAReader.ExtractText(ref container);*/ break;
                    case "downloaded": /*Downloaded = YARGDTAReader.ExtractText(ref container);*/ break;
                    case "game_origin":
                    {
                        string str = YARGDTAReader.ExtractText(ref container);
                        if (str == "#ifdef")
                        {
                            string conditional = YARGDTAReader.ExtractText(ref container);
                            if (conditional == "CUSTOMSOURCE")
                            {
                                Source = YARGDTAReader.ExtractText(ref container);
                            }
                            else
                            {
                                Source = "customs";
                            }
                        }
                        else
                        {
                            Source = str;
                        }
                        break;
                    }
                    case "song_id": SongID = YARGDTAReader.ExtractText(ref container); break;
                    case "rating": SongRating = (SongRating) YARGDTAReader.ExtractInteger<uint>(ref container); break;
                    case "short_version": /*ShortVersion = YARGDTAReader.Extract<uint>(ref container);*/ break;
                    case "album_art": /*HasAlbumArt = YARGDTAReader.ExtractBoolean(ref container);*/ break;
                    case "year_released":
                    case "year_recorded": YearAsNumber = YARGDTAReader.ExtractInteger<int>(ref container); break;
                    case "album_name": Album = YARGDTAReader.ExtractTextBytes(ref container); break;
                    case "album_track_number": AlbumTrack = YARGDTAReader.ExtractInteger<int>(ref container); break;
                    case "pack_name": Playlist = YARGDTAReader.ExtractText(ref container); break;
                    case "base_points": /*BasePoints = YARGDTAReader.Extract<uint>(ref container);*/ break;
                    case "band_fail_cue": /*BandFailCue = YARGDTAReader.ExtractText(ref container);*/ break;
                    case "drum_bank": DrumBank = YARGDTAReader.ExtractText(ref container); break;
                    case "song_length": SongLength = YARGDTAReader.ExtractInteger<long>(ref container); break;
                    case "sub_genre": /*Subgenre = YARGDTAReader.ExtractText(ref container);*/ break;
                    case "author": Charter = YARGDTAReader.ExtractText(ref container); break;
                    case "guide_pitch_volume": /*GuidePitchVolume = YARGDTAReader.Extract<float>(ref container);*/ break;
                    case "encoding":
                        MetadataEncoding = YARGDTAReader.ExtractText(ref container).ToLower() switch
                        {
                            "latin1" => YARGTextReader.Latin1,
                            "utf-8" or
                            "utf8" => YARGTextReader.UTF8Strict,
                            _ => container.Encoding
                        };
                        break;
                    case "vocal_tonic_note": VocalTonicNote = YARGDTAReader.ExtractInteger<uint>(ref container); break;
                    case "song_tonality": SongTonality = YARGDTAReader.ExtractBoolean(ref container) ? Song.SongTonality.Minor : Song.SongTonality.Major; break;
                    case "alternate_path": /*AlternatePath = YARGDTAReader.ExtractBoolean(ref container);*/ break;
                    case "real_guitar_tuning": RealGuitarTuning = YARGDTAReader.ExtractIntegerArray<int>(ref container); break;
                    case "real_bass_tuning": RealBassTuning = YARGDTAReader.ExtractIntegerArray<int>(ref container); break;
                    case "video_venues": VideoVenues = YARGDTAReader.ExtractStringArray(ref container); break;
                    case "loading_phrase": LoadingPhrase = YARGDTAReader.ExtractText(ref container); break;
                    case "extra_authoring":
                    {
                        foreach (string str in YARGDTAReader.ExtractStringArray(ref container))
                        {
                            if (str == "disc_update")
                            {
                                DiscUpdate = true;
                            }
                        }
                    }
                    break;
                }
                YARGDTAReader.EndNode(ref container);
            }
        }

        public static DTAEntry Create(string nodename, YARGTextContainer<byte> container)
        {
            var entry = Empty;
            entry.LoadData(nodename, container);
            return entry;
        }
    }
}
