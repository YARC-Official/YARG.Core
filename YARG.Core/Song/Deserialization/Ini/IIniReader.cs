using System;
using System.Collections.Generic;
using System.Text;
using YARG.Core.Song.Deserialization.Ini;

namespace YARG.Core.Song.Deserialization.Ini
{
    public interface IIniReader
    {
        public string Section { get; }
        public bool IsStartOfSection();
        public void SkipSection();
        public bool IsStillCurrentSection();
        public IniSection ExtractModifiers(ref Dictionary<string, IniModifierCreator> validNodes);
    }
}
