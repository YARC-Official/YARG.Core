using System.Threading.Tasks;
using YARG.Core.Extensions;
using YARG.Core.IO;

namespace YARG.Core.Song
{
    internal sealed class CacheWriteIndices
    {
        public int Title;
        public int Artist;
        public int Album;
        public int Genre;
        public int Year;
        public int Charter;
        public int Playlist;
        public int Source;
    }

    internal class CacheReadStrings
    {
        public const int NUM_CATEGORIES = 9;

        private string[][] _categories = new string[NUM_CATEGORIES][];

        public string[] Titles    => _categories[0];
        public string[] Artists   => _categories[1];
        public string[] Albums    => _categories[2];
        public string[] Genres    => _categories[3];
        public string[] Years     => _categories[4];
        public string[] Charters  => _categories[5];
        public string[] Playlists => _categories[6];
        public string[] Sources   => _categories[7];
        public string[] Stars     => _categories[8];

        public unsafe CacheReadStrings(FixedArrayStream* stream)
        {
            Parallel.ForEach(new CacheLoopable() { Stream = stream, Count = NUM_CATEGORIES },
                node =>
            {
                int count = node.Slice.Read<int>(Endianness.Little);
                var strings = _categories[node.Index] = new string[count];
                for (int i = 0; i < count; ++i)
                {
                    strings[i] = node.Slice.ReadString();
                }
            });
        }
    }
}
