using System;
using System.IO;
using System.Threading.Tasks;
using YARG.Core.Song.Deserialization;

namespace YARG.Core.Song.Cache
{
    public class CategoryCacheWriteNode
    {
        public int title;
        public int artist;
        public int album;
        public int genre;
        public int year;
        public int charter;
        public int playlist;
        public int source;
    }

    public sealed class CategoryCacheStrings
    {
        public string[] titles = Array.Empty<string>();
        public string[] artists = Array.Empty<string>();
        public string[] albums = Array.Empty<string>();
        public string[] genres = Array.Empty<string>();
        public string[] years = Array.Empty<string>();
        public string[] charters = Array.Empty<string>();
        public string[] playlists = Array.Empty<string>();
        public string[] sources = Array.Empty<string>();

        public CategoryCacheStrings(FileStream stream, bool multithreaded)
        {
            string[][] strings =
            {
                titles,
                artists,
                albums,
                genres,
                years,
                charters,
                playlists,
                sources
            };

            int numCategories = strings.Length;
            if (multithreaded)
            {
                var tasks = new Task[numCategories];
                for (int i = 0; i < numCategories; ++i)
                {
                    int length = stream.ReadInt32LE();
                    byte[] section = stream.ReadBytes(length);
                    tasks[i] = Task.Run(() => { strings[i] = ReadStrings(section); });
                }
                Task.WaitAll(tasks);
            }
            else
            {
                for (int i = 0; i < numCategories; ++i)
                {
                    int length = stream.ReadInt32LE();
                    strings[i] = ReadStrings(stream.ReadBytes(length));
                }
            }

            static string[] ReadStrings(byte[] section)
            {
                YARGBinaryReader reader = new(section);
                int count = reader.ReadInt32();
                string[] strings = new string[count];
                for (int i = 0; i < count; ++i)
                    strings[i] = reader.ReadLEBString();
                return strings;
            }
        }
    }
}
