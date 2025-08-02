using System.Collections.Generic;
using YARG.Core.IO;

namespace YARG.Core.Song.Cache
{
    internal class CONModifcationGroup<TValue>
    {
        public readonly AbridgedFileInfo           Root;
        public readonly Dictionary<string, TValue> Values;
        public          FixedArray<byte>?          Data;

        public CONModifcationGroup(in AbridgedFileInfo root)
        {
            Root = root;
            Values = new Dictionary<string, TValue>();
            Data = null;
        }
    }
}
