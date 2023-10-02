using System;
using System.IO;
using System.Threading.Tasks;
using YARG.Core.Extensions;
using YARG.Core.IO;

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
        const int NUM_CATEGORIES = 8;
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
            if (multithreaded)
            {
                var tasks = new Task[NUM_CATEGORIES];
                for (int i = 0; i < NUM_CATEGORIES; ++i)
                {
                    int length = stream.ReadInt32LE();
                    byte[] section = stream.ReadBytes(length);

                    int strIndex = i;
                    tasks[i] = Task.Run(() => { GetArray(strIndex) = ReadStrings(section); });
                }
                Task.WaitAll(tasks);
            }
            else
            {
                for (int i = 0; i < NUM_CATEGORIES; ++i)
                {
                    int length = stream.ReadInt32LE();
                    GetArray(i) = ReadStrings(stream.ReadBytes(length));
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

        private ref string[] GetArray(int index)
        {
            switch(index)
            {
                case 0: return ref titles;
                case 1: return ref artists;
                case 2: return ref albums;
                case 3: return ref genres;
                case 4: return ref years;
                case 5: return ref charters;
                case 6: return ref playlists;
                case 7: return ref sources;
                default:
                    throw new NotImplementedException();
            }
        }
    }
}
