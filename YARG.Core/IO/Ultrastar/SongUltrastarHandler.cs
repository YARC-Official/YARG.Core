using System;
using System.Collections.Generic;

namespace YARG.Core.IO.Ultrastar
{
    public enum UltrastarVersion
    {
        Unknown,
        V1_0_0,
        V1_1_0,
        V1_2_0,
        V2_0_0,
    };

    public static class SongUltrastarHandler
    {
        public static UltrastarModifierCollection ReadSongUltrastarFile(string ultrastarPath)
        {
            return YARGUltrastarReader.ReadUltrastarFile(ultrastarPath, SONG_ULTRASTAR_OUTLINES, SONG_ULTRASTAR_DEPRECATION_REPLACEMENTS);
        }

        public static UltrastarVersion ConvertVersionToEnum(string versionNumber)
        {
            string enumString = versionNumber.Replace(".", "_");

            if (Enum.TryParse(enumString, out UltrastarVersion version))
            {
                return version;
            }
            else
            {
                return UltrastarVersion.Unknown;
            }
        }

        public static string ConvertEnumToVersion(UltrastarVersion version)
        {
            return version.ToString().Replace("_", ".");
        }

        public static readonly Dictionary<string, UltrastarModifierOutline> SONG_ULTRASTAR_OUTLINES;
        public static readonly Dictionary<string, string> SONG_ULTRASTAR_DEPRECATION_REPLACEMENTS;

        static SongUltrastarHandler()
        {
            SONG_ULTRASTAR_OUTLINES = new()
            {
                {"#artist", new("artist", ModifierType.String) },
                {"#audio", new("audio", ModifierType.String) },
                {"#audiourl", new("audiourl", ModifierType.String) },
                {"#background", new("background", ModifierType.String) },
                {"#backgroundurl", new("backgroundurl", ModifierType.String) },
                {"#bpm", new("bpm", ModifierType.Double) },
                {"#cover", new("cover", ModifierType.String) },
                {"#coverurl", new("coverurl", ModifierType.String) },
                {"#creator", new("creator", ModifierType.String) },
                {"#edition", new("edition", ModifierType.String) },
                {"#end", new("end", ModifierType.Double) },
                {"#gap", new("gap", ModifierType.Double) },
                {"#genre", new("genre", ModifierType.String) },
                {"#instrumental", new("instrumental", ModifierType.String) },
                {"#language", new("language", ModifierType.String) },
                {"#medleyend", new("medleyend", ModifierType.Double) },
                {"#medleyendbeat", new("medleyendbeat", ModifierType.Double) },
                {"#medleystart", new("medleystart", ModifierType.Double) },
                {"#medleystartbeat", new("medleystartbeat", ModifierType.Double) },
                {"#previewend", new("previewend", ModifierType.Double) },
                {"#previewstart", new("previewstart", ModifierType.Double) },
                {"#start", new("start", ModifierType.Double) },
                {"#tags", new("tags", ModifierType.String) },
                {"#title", new("title", ModifierType.String) },
                {"#version", new("version", ModifierType.String) },
                {"#video", new("video", ModifierType.String) },
                {"#videourl", new("videourl", ModifierType.String) },
                {"#videogap", new("artist", ModifierType.Double) },
                {"#vocals", new("vocals", ModifierType.String) },
                {"#year", new("year", ModifierType.String) },

                // Deprecated tags
                {"#mp3", new("audio", ModifierType.String) }, // Replaced by #audio
                {"#website", new("audiourl", ModifierType.String) }, // Replaced by #audiourl
                {"#instrumentalaudio", new("instrumental", ModifierType.String) }, // Replaced by #instrumental
                {"#vocalsaudio", new("vocals", ModifierType.String) }, // Replaced by #vocals
                {"#audiogap", new("gap", ModifierType.Double) }, // Replaced by #gap
                {"#duetsingerp1", new("p1", ModifierType.String) }, // Replaced by #p1
                {"#duetsingerp2", new("p2", ModifierType.String) }, // Replaced by #p2
            };

            SONG_ULTRASTAR_DEPRECATION_REPLACEMENTS = new()
            {
                {"#mp3", "#audio"},
                {"#website", "#audiourl"},
                {"#instrumentalaudio", "#instrumental"},
                {"#vocalsaudio", "#vocals"},
                {"#audiogap", "#gap"},
                {"#duetsingerp1", "#p1"},
                {"#duetsingerp2", "#duetsingerp2"},
            };
        }
    }
}
