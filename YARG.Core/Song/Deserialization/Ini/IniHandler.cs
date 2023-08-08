using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YARG.Core.Song.Deserialization.Ini
{
    public static class IniHandler
    {
        public static Dictionary<string, IniSection> ReadIniFile(string iniFile, Dictionary<string, Dictionary<string, IniModifierCreator>> sections)
        {
            YARGIniReader reader = new(iniFile);
            Dictionary<string, IniSection> modifierMap = new();
            while (reader.IsStartOfSection())
            {
                if (sections.TryGetValue(reader.Section, out var nodes))
                    modifierMap[reader.Section] = reader.ExtractModifiers(ref nodes);
                else
                    reader.SkipSection();
            }
            return modifierMap;
        }

        private static readonly Dictionary<string, Dictionary<string, IniModifierCreator>> SONG_INI_DICTIONARY = new();
        static IniHandler()
        {
            SONG_INI_DICTIONARY.Add("[song]", new()
            {
                { "album",                                new("album", ModifierCreatorType.SortString) },
                { "album_track",                          new("album_track", ModifierCreatorType.Int32) },
                { "artist",                               new("artist", ModifierCreatorType.SortString) },

                { "background",                           new("background", ModifierCreatorType.String) },
                //{ "banner_link_a",                        new("banner_link_a", ModifierCreatorType.String) },
                //{ "banner_link_b",                        new("banner_link_b", ModifierCreatorType.String) },
                { "bass_type",                            new("bass_type", ModifierCreatorType.UInt32) },
                //{ "boss_battle",                          new("boss_battle", ModifierCreatorType.Bool) },

                //{ "cassettecolor",                        new("cassettecolor", ModifierCreatorType.UInt32) },
                { "charter",                              new("charter", ModifierCreatorType.SortString) },
                { "count",                                new("count", ModifierCreatorType.UInt32) },
                { "cover",                                new("cover", ModifierCreatorType.String) },

                { "dance_type",                           new("dance_type", ModifierCreatorType.UInt32) },
                { "delay",                                new("delay", ModifierCreatorType.Int64) },
                { "diff_band",                            new("diff_band", ModifierCreatorType.Int32) },
                { "diff_bass",                            new("diff_bass", ModifierCreatorType.Int32) },
                { "diff_bass_real",                       new("diff_bass_real", ModifierCreatorType.Int32) },
                { "diff_bass_real_22",                    new("diff_bass_real_22", ModifierCreatorType.Int32) },
                { "diff_bassghl",                         new("diff_bassghl", ModifierCreatorType.Int32) },
                { "diff_dance",                           new("diff_dance", ModifierCreatorType.Int32) },
                { "diff_drums",                           new("diff_drums", ModifierCreatorType.Int32) },
                { "diff_drums_real",                      new("diff_drums_real", ModifierCreatorType.Int32) },
                { "diff_drums_real_ps",                   new("diff_drums_real_ps", ModifierCreatorType.Int32) },
                { "diff_guitar",                          new("diff_guitar", ModifierCreatorType.Int32) },
                { "diff_guitar_coop",                     new("diff_guitar_coop", ModifierCreatorType.Int32) },
                { "diff_guitar_coop_ghl",                 new("diff_guitar_coop_ghl", ModifierCreatorType.Int32) },
                { "diff_guitar_real",                     new("diff_guitar_real", ModifierCreatorType.Int32) },
                { "diff_guitar_real_22",                  new("diff_guitar_real_22", ModifierCreatorType.Int32) },
                { "diff_guitarghl",                       new("diff_guitarghl", ModifierCreatorType.Int32) },
                { "diff_keys",                            new("diff_keys", ModifierCreatorType.Int32) },
                { "diff_keys_real",                       new("diff_keys_real", ModifierCreatorType.Int32) },
                { "diff_keys_real_ps",                    new("diff_keys_real_ps", ModifierCreatorType.Int32) },
                { "diff_rhythm",                          new("diff_rhythm", ModifierCreatorType.Int32) },
                { "diff_rhythm_ghl",                      new("diff_rhythm_ghl", ModifierCreatorType.Int32) },
                { "diff_vocals",                          new("diff_vocals", ModifierCreatorType.Int32) },
                { "diff_vocals_harm",                     new("diff_vocals_harm", ModifierCreatorType.Int32) },
                { "drum_fallback_blue",                   new("drum_fallback_blue", ModifierCreatorType.Bool) },

                //{ "early_hit_window_size",                new("early_hit_window_size", ModifierCreatorType.String) },
                { "eighthnote_hopo",                      new("eighthnote_hopo", ModifierCreatorType.UInt32) },
                { "end_events",                           new("end_events", ModifierCreatorType.Bool) },
                //{ "eof_midi_import_drum_accent_velocity", new("eof_midi_import_drum_accent_velocity", ModifierCreatorType.UInt16) },
                //{ "eof_midi_import_drum_ghost_velocity",  new("eof_midi_import_drum_ghost_velocity", ModifierCreatorType.UInt16) },

                { "five_lane_drums",                      new("five_lane_drums", ModifierCreatorType.Bool) },
                { "frets",                                new("charter", ModifierCreatorType.SortString) },

                { "genre",                                new("genre", ModifierCreatorType.SortString) },
                { "guitar_type",                          new("guitar_type", ModifierCreatorType.UInt32) },

                { "hopo_frequency",                       new("hopo_frequency", ModifierCreatorType.Int64) },
                { "hopofreq",                             new("hopofreq", ModifierCreatorType.Int32) },

                { "icon",                                 new("icon", ModifierCreatorType.String) },

                { "keys_type",                            new("keys_type", ModifierCreatorType.UInt32) },
                { "kit_type",                             new("kit_type", ModifierCreatorType.UInt32) },

                //{ "link_name_a",                          new("link_name_a", ModifierCreatorType.String) },
                //{ "link_name_b",                          new("link_name_b", ModifierCreatorType.String) },
                { "loading_phrase",                       new("loading_phrase", ModifierCreatorType.String) },
                { "lyrics",                               new("lyrics", ModifierCreatorType.Bool) },

                { "modchart",                             new("modchart", ModifierCreatorType.Bool) },
                { "multiplier_note",                      new("multiplier_note", ModifierCreatorType.Int32) },

                { "name",                                 new("name", ModifierCreatorType.SortString) },

                { "playlist",                             new("playlist", ModifierCreatorType.SortString) },
                { "playlist_track",                       new("playlist_track", ModifierCreatorType.Int32) },
                { "preview",                              new("preview", ModifierCreatorType.UInt64Array) },
                { "preview_end_time",                     new("preview_end_time", ModifierCreatorType.UInt64) },
                { "preview_start_time",                   new("preview_start_time", ModifierCreatorType.UInt64) },

                { "pro_drum",                             new("pro_drums", ModifierCreatorType.Bool) },
                { "pro_drums",                            new("pro_drums", ModifierCreatorType.Bool) },

                { "rating",                               new("rating", ModifierCreatorType.UInt32) },
                { "real_bass_22_tuning",                  new("real_bass_22_tuning", ModifierCreatorType.UInt32) },
                { "real_bass_tuning",                     new("real_bass_tuning", ModifierCreatorType.UInt32) },
                { "real_guitar_22_tuning",                new("real_guitar_22_tuning", ModifierCreatorType.UInt32) },
                { "real_guitar_tuning",                   new("real_guitar_tuning", ModifierCreatorType.UInt32) },
                { "real_keys_lane_count_left",            new("real_keys_lane_count_left", ModifierCreatorType.UInt32) },
                { "real_keys_lane_count_right",           new("real_keys_lane_count_right", ModifierCreatorType.UInt32) },

                //{ "scores",                               new("scores", ModifierCreatorType.String) },
                //{ "scores_ext",                           new("scores_ext", ModifierCreatorType.String) },
                { "song_length",                          new("song_length", ModifierCreatorType.UInt64) },
                { "star_power_note",                      new("multiplier_note", ModifierCreatorType.Int32) },
                { "sub_genre",                            new("sub_genre", ModifierCreatorType.SortString) },
                { "sub_playlist",                         new("sub_playlist", ModifierCreatorType.SortString) },
                { "sustain_cutoff_threshold",             new("sustain_cutoff_threshold", ModifierCreatorType.Int64) },
                //{ "sysex_high_hat_ctrl",                  new("sysex_high_hat_ctrl", ModifierCreatorType.Bool) },
                //{ "sysex_open_bass",                      new("sysex_open_bass", ModifierCreatorType.Bool) },
                //{ "sysex_pro_slide",                      new("sysex_pro_slide", ModifierCreatorType.Bool) },
                //{ "sysex_rimshot",                        new("sysex_rimshot", ModifierCreatorType.Bool) },
                //{ "sysex_slider",                         new("sysex_slider", ModifierCreatorType.Bool) },

                { "tags",                                 new("tags", ModifierCreatorType.String) },
                { "track",                                new("album_track", ModifierCreatorType.Int32) },
                { "tutorial",                             new("tutorial", ModifierCreatorType.Bool) },

                { "unlock_completed",                     new("unlock_completed", ModifierCreatorType.UInt32) },
                { "unlock_id",                            new("unlock_id", ModifierCreatorType.String) },
                { "unlock_require",                       new("unlock_require", ModifierCreatorType.String) },
                { "unlock_text",                          new("unlock_text", ModifierCreatorType.String) },

                { "version",                              new("version", ModifierCreatorType.UInt32) },
                { "video",                                new("video", ModifierCreatorType.String) },
                { "video_end_time",                       new("video_end_time", ModifierCreatorType.Int64) },
                { "video_loop",                           new("video_loop", ModifierCreatorType.Bool) },
                { "video_start_time",                     new("video_start_time", ModifierCreatorType.Int64) },
                { "vocal_gender",                         new("vocal_gender", ModifierCreatorType.UInt32) },

                { "year",                                 new("year", ModifierCreatorType.String) }
            });
        }

        public static IniSection ReadSongIniFile(string iniFile)
        {
            var modifiers = ReadIniFile(iniFile, SONG_INI_DICTIONARY);
            if (modifiers.Count == 0)
                return new();
            return modifiers.First().Value;
        }
    }
}
