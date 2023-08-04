using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YARG.Core.Deserialization.Ini
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
                { "album",                                new("album", ModifierNodeType.SORTSTRING) },
                { "album_track",                          new("album_track", ModifierNodeType.UINT16) },
                { "artist",                               new("artist", ModifierNodeType.SORTSTRING) },

                { "background",                           new("background", ModifierNodeType.STRING) },
                { "banner_link_a",                        new("banner_link_a", ModifierNodeType.STRING) },
                { "banner_link_b",                        new("banner_link_b", ModifierNodeType.STRING) },
                { "bass_type",                            new("bass_type", ModifierNodeType.UINT32) },
                { "boss_battle",                          new("boss_battle", ModifierNodeType.BOOL) },

                { "cassettecolor",                        new("cassettecolor", ModifierNodeType.UINT32) },
                { "charter",                              new("charter", ModifierNodeType.SORTSTRING) },
                { "count",                                new("count", ModifierNodeType.UINT32) },
                { "cover",                                new("cover", ModifierNodeType.STRING) },

                { "dance_type",                           new("dance_type", ModifierNodeType.UINT32) },
                { "delay",                                new("delay", ModifierNodeType.DOUBLE) },
                { "diff_band",                            new("diff_band", ModifierNodeType.INT32) },
                { "diff_bass",                            new("diff_bass", ModifierNodeType.INT32) },
                { "diff_bass_real",                       new("diff_bass_real", ModifierNodeType.INT32) },
                { "diff_bass_real_22",                    new("diff_bass_real_22", ModifierNodeType.INT32) },
                { "diff_bassghl",                         new("diff_bassghl", ModifierNodeType.INT32) },
                { "diff_dance",                           new("diff_dance", ModifierNodeType.INT32) },
                { "diff_drums",                           new("diff_drums", ModifierNodeType.INT32) },
                { "diff_drums_real",                      new("diff_drums_real", ModifierNodeType.INT32) },
                { "diff_drums_real_ps",                   new("diff_drums_real_ps", ModifierNodeType.INT32) },
                { "diff_guitar",                          new("diff_guitar", ModifierNodeType.INT32) },
                { "diff_guitar_coop",                     new("diff_guitar_coop", ModifierNodeType.INT32) },
                { "diff_guitar_coop_ghl",                 new("diff_guitar_coop_ghl", ModifierNodeType.INT32) },
                { "diff_guitar_real",                     new("diff_guitar_real", ModifierNodeType.INT32) },
                { "diff_guitar_real_22",                  new("diff_guitar_real_22", ModifierNodeType.INT32) },
                { "diff_guitarghl",                       new("diff_guitarghl", ModifierNodeType.INT32) },
                { "diff_keys",                            new("diff_keys", ModifierNodeType.INT32) },
                { "diff_keys_real",                       new("diff_keys_real", ModifierNodeType.INT32) },
                { "diff_keys_real_ps",                    new("diff_keys_real_ps", ModifierNodeType.INT32) },
                { "diff_rhythm",                          new("diff_rhythm", ModifierNodeType.INT32) },
                { "diff_rhythm_ghl",                      new("diff_rhythm_ghl", ModifierNodeType.INT32) },
                { "diff_vocals",                          new("diff_vocals", ModifierNodeType.INT32) },
                { "diff_vocals_harm",                     new("diff_vocals_harm", ModifierNodeType.INT32) },
                { "drum_fallback_blue",                   new("drum_fallback_blue", ModifierNodeType.BOOL) },

                { "early_hit_window_size",                new("early_hit_window_size", ModifierNodeType.STRING) },
                { "eighthnote_hopo",                      new("eighthnote_hopo", ModifierNodeType.UINT32) },
                { "end_events",                           new("end_events", ModifierNodeType.BOOL) },
                { "eof_midi_import_drum_accent_velocity", new("eof_midi_import_drum_accent_velocity", ModifierNodeType.UINT16) },
                { "eof_midi_import_drum_ghost_velocity",  new("eof_midi_import_drum_ghost_velocity", ModifierNodeType.UINT16) },

                { "five_lane_drums",                      new("five_lane_drums", ModifierNodeType.BOOL) },
                { "frets",                                new("charter", ModifierNodeType.SORTSTRING) },

                { "genre",                                new("genre", ModifierNodeType.SORTSTRING) },
                { "guitar_type",                          new("guitar_type", ModifierNodeType.UINT32) },

                { "hopo_frequency",                       new("hopo_frequency", ModifierNodeType.INT64) },

                { "icon",                                 new("icon", ModifierNodeType.STRING) },

                { "keys_type",                            new("keys_type", ModifierNodeType.UINT32) },
                { "kit_type",                             new("kit_type", ModifierNodeType.UINT32) },

                { "link_name_a",                          new("link_name_a", ModifierNodeType.STRING) },
                { "link_name_b",                          new("link_name_b", ModifierNodeType.STRING) },
                { "loading_phrase",                       new("loading_phrase", ModifierNodeType.STRING) },
                { "lyrics",                               new("lyrics", ModifierNodeType.BOOL) },

                { "modchart",                             new("modchart", ModifierNodeType.BOOL) },
                { "multiplier_note",                      new("multiplier_note", ModifierNodeType.UINT16) },

                { "name",                                 new("name", ModifierNodeType.SORTSTRING) },

                { "playlist",                             new("playlist", ModifierNodeType.SORTSTRING) },
                { "playlist_track",                       new("playlist_track", ModifierNodeType.UINT16) },
                { "preview",                              new("preview", ModifierNodeType.DOUBLEARRAY) },
                { "preview_end_time",                     new("preview_end_time", ModifierNodeType.DOUBLE) },
                { "preview_start_time",                   new("preview_start_time", ModifierNodeType.DOUBLE) },

                { "pro_drum",                             new("pro_drums", ModifierNodeType.BOOL) },
                { "pro_drums",                            new("pro_drums", ModifierNodeType.BOOL) },

                { "rating",                               new("rating", ModifierNodeType.UINT32) },
                { "real_bass_22_tuning",                  new("real_bass_22_tuning", ModifierNodeType.UINT32) },
                { "real_bass_tuning",                     new("real_bass_tuning", ModifierNodeType.UINT32) },
                { "real_guitar_22_tuning",                new("real_guitar_22_tuning", ModifierNodeType.UINT32) },
                { "real_guitar_tuning",                   new("real_guitar_tuning", ModifierNodeType.UINT32) },
                { "real_keys_lane_count_left",            new("real_keys_lane_count_left", ModifierNodeType.UINT32) },
                { "real_keys_lane_count_right",           new("real_keys_lane_count_right", ModifierNodeType.UINT32) },

                { "scores",                               new("scores", ModifierNodeType.STRING) },
                { "scores_ext",                           new("scores_ext", ModifierNodeType.STRING) },
                { "song_length",                          new("song_length", ModifierNodeType.UINT64) },
                { "star_power_note",                      new("multiplier_note", ModifierNodeType.UINT16) },
                { "sub_genre",                            new("sub_genre", ModifierNodeType.SORTSTRING) },
                { "sub_playlist",                         new("sub_playlist", ModifierNodeType.SORTSTRING) },
                { "sustain_cutoff_threshold",             new("sustain_cutoff_threshold", ModifierNodeType.INT64) },
                { "sysex_high_hat_ctrl",                  new("sysex_high_hat_ctrl", ModifierNodeType.BOOL) },
                { "sysex_open_bass",                      new("sysex_open_bass", ModifierNodeType.BOOL) },
                { "sysex_pro_slide",                      new("sysex_pro_slide", ModifierNodeType.BOOL) },
                { "sysex_rimshot",                        new("sysex_rimshot", ModifierNodeType.BOOL) },
                { "sysex_slider",                         new("sysex_slider", ModifierNodeType.BOOL) },

                { "tags",                                 new("tags", ModifierNodeType.STRING) },
                { "track",                                new("album_track", ModifierNodeType.UINT16) },
                { "tutorial",                             new("tutorial", ModifierNodeType.BOOL) },

                { "unlock_completed",                     new("unlock_completed", ModifierNodeType.UINT32) },
                { "unlock_id",                            new("unlock_id", ModifierNodeType.STRING) },
                { "unlock_require",                       new("unlock_require", ModifierNodeType.STRING) },
                { "unlock_text",                          new("unlock_text", ModifierNodeType.STRING) },

                { "version",                              new("version", ModifierNodeType.UINT32) },
                { "video",                                new("video", ModifierNodeType.STRING) },
                { "video_end_time",                       new("video_end_time", ModifierNodeType.DOUBLE) },
                { "video_loop",                           new("video_loop", ModifierNodeType.BOOL) },
                { "video_start_time",                     new("video_start_time", ModifierNodeType.DOUBLE) },
                { "vocal_gender",                         new("vocal_gender", ModifierNodeType.UINT32) },

                { "year",                                 new("year", ModifierNodeType.STRING) }
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
