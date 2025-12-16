using System.Collections.Generic;

namespace YARG.Core.IO.Ini
{
    public static class SongIniHandler
    {
        public static IniModifierCollection ReadSongIniFile(string iniPath)
        {
            var modifiers = YARGIniReader.ReadIniFile(iniPath, SONG_INI_LOOKUP_MAP);
            if (!modifiers.TryGetValue("[song]", out var collection))
            {
                collection = new IniModifierCollection();
            }
            return collection;
        }

        private static readonly Dictionary<string, Dictionary<string, IniModifierOutline>> SONG_INI_LOOKUP_MAP;
        public static readonly Dictionary<string, IniModifierOutline> SONG_INI_OUTLINES;

        static SongIniHandler()
        {
            SONG_INI_OUTLINES = new()
            {
                { "album",                                new("album", ModifierType.String ) },
                { "album_track",                          new("album_track", ModifierType.Int32) },
                { "artist",                               new("artist", ModifierType.String) },

                { "background",                           new("background", ModifierType.String) },
                //{ "banner_link_a",                        new("banner_link_a", ModifierType.String) },
                //{ "banner_link_b",                        new("banner_link_b", ModifierType.String) },
                { "bass_type",                            new("bass_type", ModifierType.UInt32) },
                //{ "boss_battle",                          new("boss_battle", ModifierType.Bool) },

                //{ "cassettecolor",                        new("cassettecolor", ModifierType.UInt32) },
                { "charter",                              new("charter", ModifierType.String) },
                { "charter_bass",                         new("charter_bass", ModifierType.String) },
                { "charter_drums",                        new("charter_drums", ModifierType.String) },
                { "charter_elite_drums",                  new("charter_elite_drums", ModifierType.String) },
                { "charter_guitar",                       new("charter_guitar", ModifierType.String) },
                { "charter_keys",                         new("charter_keys", ModifierType.String) },
                { "charter_lower_diff",                   new("charter_lower_diff", ModifierType.String) },
                { "charter_pro_bass",                     new("charter_pro_bass", ModifierType.String) },
                { "charter_pro_keys",                     new("charter_pro_keys", ModifierType.String) },
                { "charter_pro_guitar",                   new("charter_pro_guitar", ModifierType.String) },
                { "charter_venue",                        new ("charter_venue", ModifierType.String) },
                { "charter_vocals",                       new("charter_vocals", ModifierType.String) },
                { "count",                                new("count", ModifierType.UInt32) },
                { "cover",                                new("cover", ModifierType.String) },
                { "credit_album_art_by",                  new("credit_album_art_designed_by", ModifierType.String) },
                { "credit_album_art_designed_by",         new("credit_album_art_designed_by", ModifierType.String) },
                { "credit_album_cover",                   new("credit_album_art_designed_by", ModifierType.String) },
                { "credit_arranged_by",                   new("credit_arranged_by", ModifierType.String) },
                { "credit_composed_by",                   new("credit_composed_by", ModifierType.String) },
                { "credit_courtesy_of",                   new("credit_courtesy_of", ModifierType.String) },
                { "credit_engineered_by",                 new("credit_engineered_by", ModifierType.String) },
                { "credit_license",                       new("credit_license", ModifierType.String) },
                { "credit_mastered_by",                   new("credit_mastered_by", ModifierType.String) },
                { "credit_mixed_by",                      new("credit_mixed_by", ModifierType.String) },
                { "credit_other",                         new("credit_other", ModifierType.String) },
                { "credit_performed_by",                  new("credit_performed_by", ModifierType.String) },
                { "credit_produced_by",                   new("credit_produced_by", ModifierType.String) },
                { "credit_published_by",                  new("credit_published_by", ModifierType.String) },
                { "credit_written_by",                    new("credit_written_by", ModifierType.String) },

                { "dance_type",                           new("dance_type", ModifierType.UInt32) },
                { "delay",                                new("delay", ModifierType.Int64) },
                { "diff_band",                            new("diff_band", ModifierType.Int32) },
                { "diff_bass",                            new("diff_bass", ModifierType.Int32) },
                { "diff_bass_real",                       new("diff_bass_real", ModifierType.Int32) },
                { "diff_bass_real_22",                    new("diff_bass_real_22", ModifierType.Int32) },
                { "diff_bassghl",                         new("diff_bassghl", ModifierType.Int32) },
                { "diff_dance",                           new("diff_dance", ModifierType.Int32) },
                { "diff_drums",                           new("diff_drums", ModifierType.Int32) },
                { "diff_drums_real",                      new("diff_drums_real", ModifierType.Int32) },
                { "diff_drums_real_ps",                   new("diff_drums_real_ps", ModifierType.Int32) },
                { "diff_elite_drums",                     new("diff_elite_drums", ModifierType.Int32) },
                { "diff_guitar",                          new("diff_guitar", ModifierType.Int32) },
                { "diff_guitar_coop",                     new("diff_guitar_coop", ModifierType.Int32) },
                { "diff_guitar_coop_ghl",                 new("diff_guitar_coop_ghl", ModifierType.Int32) },
                { "diff_guitar_real",                     new("diff_guitar_real", ModifierType.Int32) },
                { "diff_guitar_real_22",                  new("diff_guitar_real_22", ModifierType.Int32) },
                { "diff_guitarghl",                       new("diff_guitarghl", ModifierType.Int32) },
                { "diff_keys",                            new("diff_keys", ModifierType.Int32) },
                { "diff_keys_real",                       new("diff_keys_real", ModifierType.Int32) },
                { "diff_keys_real_ps",                    new("diff_keys_real_ps", ModifierType.Int32) },
                { "diff_rhythm",                          new("diff_rhythm", ModifierType.Int32) },
                { "diff_rhythm_ghl",                      new("diff_rhythm_ghl", ModifierType.Int32) },
                { "diff_vocals",                          new("diff_vocals", ModifierType.Int32) },
                { "diff_vocals_harm",                     new("diff_vocals_harm", ModifierType.Int32) },
                { "drum_fallback_blue",                   new("drum_fallback_blue", ModifierType.Bool) },

                //{ "early_hit_window_size",                new("early_hit_window_size", ModifierType.String) },
                { "eighthnote_hopo",                      new("eighthnote_hopo", ModifierType.Bool) },
                { "end_events",                           new("end_events", ModifierType.Bool) },
                //{ "eof_midi_import_drum_accent_velocity", new("eof_midi_import_drum_accent_velocity", ModifierType.UInt16) },
                //{ "eof_midi_import_drum_ghost_velocity",  new("eof_midi_import_drum_ghost_velocity", ModifierType.UInt16) },

                { "five_lane_drums",                      new("five_lane_drums", ModifierType.Bool) },
                { "frets",                                new("frets", ModifierType.String) },

                { "genre",                                new("genre", ModifierType.String) },
                { "guitar_type",                          new("guitar_type", ModifierType.UInt32) },

                { "hopo_frequency",                       new("hopo_frequency", ModifierType.Int64) },
                { "hopofreq",                             new("hopofreq", ModifierType.Int32) },

                { "icon",                                 new("icon", ModifierType.String) },

                { "keys_type",                            new("keys_type", ModifierType.UInt32) },
                { "kit_type",                             new("kit_type", ModifierType.UInt32) },

                //{ "link_name_a",                          new("link_name_a", ModifierType.String) },
                //{ "link_name_b",                          new("link_name_b", ModifierType.String) },
                { "link_bandcamp",                        new("link_bandcamp", ModifierType.String) },
                { "link_bluesky",                         new("link_bluesky", ModifierType.String) },
                { "link_facebook",                        new("link_facebook", ModifierType.String) },
                { "link_instagram",                       new("link_instagram", ModifierType.String) },
                { "link_spotify",                         new("link_spotify", ModifierType.String) },
                { "link_twitter",                         new("link_twitter", ModifierType.String) },
                { "link_other",                           new("link_other", ModifierType.String) },
                { "link_youtube",                         new("link_youtube", ModifierType.String) },
                { "loading_phrase",                       new("loading_phrase", ModifierType.String) },
                { "location",                             new("location", ModifierType.String) },
                { "lyrics",                               new("lyrics", ModifierType.Bool) },

                { "modchart",                             new("modchart", ModifierType.Bool) },
                { "multiplier_note",                      new("multiplier_note", ModifierType.Int32) },

                { "name",                                 new("name", ModifierType.String) },

                { "playlist",                             new("playlist", ModifierType.String) },
                { "playlist_track",                       new("playlist_track", ModifierType.Int32) },
                { "preview",                              new("preview", ModifierType.Int64Array) },
                { "preview_end_time",                     new("preview_end_time", ModifierType.Int64) },
                { "preview_start_time",                   new("preview_start_time", ModifierType.Int64) },

                { "pro_drum",                             new("pro_drums", ModifierType.Bool) },
                { "pro_drums",                            new("pro_drums", ModifierType.Bool) },

                { "rating",                               new("rating", ModifierType.UInt32) },
                { "real_bass_22_tuning",                  new("real_bass_22_tuning", ModifierType.UInt32) },
                { "real_bass_tuning",                     new("real_bass_tuning", ModifierType.UInt32) },
                { "real_guitar_22_tuning",                new("real_guitar_22_tuning", ModifierType.UInt32) },
                { "real_guitar_tuning",                   new("real_guitar_tuning", ModifierType.UInt32) },
                { "real_keys_lane_count_left",            new("real_keys_lane_count_left", ModifierType.UInt32) },
                { "real_keys_lane_count_right",           new("real_keys_lane_count_right", ModifierType.UInt32) },

                //{ "scores",                               new("scores", ModifierType.String) },
                //{ "scores_ext",                           new("scores_ext", ModifierType.String) },
                { "song_length",                          new("song_length", ModifierType.Int64) },
                { "star_power_note",                      new("multiplier_note", ModifierType.Int32) },
                { "sub_genre",                            new("sub_genre", ModifierType.String) },
                { "sub_playlist",                         new("sub_playlist", ModifierType.String) },
                { "sustain_cutoff_threshold",             new("sustain_cutoff_threshold", ModifierType.Int64) },
                //{ "sysex_high_hat_ctrl",                  new("sysex_high_hat_ctrl", ModifierType.Bool) },
                //{ "sysex_open_bass",                      new("sysex_open_bass", ModifierType.Bool) },
                //{ "sysex_pro_slide",                      new("sysex_pro_slide", ModifierType.Bool) },
                //{ "sysex_rimshot",                        new("sysex_rimshot", ModifierType.Bool) },
                //{ "sysex_slider",                         new("sysex_slider", ModifierType.Bool) },

                { "tags",                                 new("tags", ModifierType.String) },
                { "track",                                new("album_track", ModifierType.Int32) },
                { "tuning_offset_cents",                  new("tuning_offset_cents", ModifierType.Int16) },
                { "tutorial",                             new("tutorial", ModifierType.Bool) },

                { "unlock_completed",                     new("unlock_completed", ModifierType.UInt32) },
                { "unlock_id",                            new("unlock_id", ModifierType.String) },
                { "unlock_require",                       new("unlock_require", ModifierType.String) },
                { "unlock_text",                          new("unlock_text", ModifierType.String) },

                { "version",                              new("version", ModifierType.UInt32) },
                { "video",                                new("video", ModifierType.String) },
                { "video_end_time",                       new("video_end_time", ModifierType.Int64) },
                { "video_loop",                           new("video_loop", ModifierType.Bool) },
                { "video_start_time",                     new("video_start_time", ModifierType.Int64) },
                { "vocal_gender",                         new("vocal_gender", ModifierType.UInt32) },
                { "vocal_scroll_speed",                   new("vocal_scroll_speed", ModifierType.Int16) },

                { "year",                                 new("year", ModifierType.String) },
            };

            SONG_INI_LOOKUP_MAP = new()
            {
                { "[song]", SONG_INI_OUTLINES }
            };
        }
    }
}
