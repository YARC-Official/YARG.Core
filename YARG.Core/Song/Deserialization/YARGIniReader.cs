using System.Collections.Generic;
using System.Text;
using YARG.Core.Song.Deserialization.Ini;

namespace YARG.Core.Song.Deserialization
{
    public sealed class YARGIniReader
    {
        private readonly YARGTXTReader reader;
        private readonly byte[] data;
        private readonly int length;

        private string sectionName = string.Empty;
        public string Section { get { return sectionName; } }

        public YARGIniReader(YARGTXTReader reader)
        {
            this.reader = reader;
            data = reader.Data;
            length = data.Length;
        }

        public YARGIniReader(byte[] data) : this(new YARGTXTReader(data)) { }

        public YARGIniReader(string path) : this(new YARGTXTReader(path)) { }

        public bool IsStartOfSection()
        {
            if (reader.IsEndOfFile())
                return false;

            if (reader.PeekByte() != '[')
            {
                SkipSection();
                if (reader.IsEndOfFile())
                    return false;
            }

            int position = reader.Position;
            sectionName = Encoding.UTF8.GetString(data, position, reader.Next - position).TrimEnd().ToLower();
            return true;
        }

        public void SkipSection()
        {
            reader.GotoNextLine();
            int position = reader.Position;
            while (GetDistanceToTrackCharacter(position, out int next))
            {
                int point = position + next - 1;
                while (point > position && YARGTXTReader_Base.IsWhitespace(data[point]) && data[point] != '\n')
                    --point;

                if (data[point] == '\n')
                {
                    reader.Position = position + next;
                    reader.SetNextPointer();
                    return;
                }

                position += next + 1;
            }

            reader.Position = length;
            reader.SetNextPointer();
        }

        public bool IsStillCurrentSection()
        {
            return !reader.IsEndOfFile() && reader.PeekByte() != '[';
        }

        public IniSection ExtractModifiers(ref Dictionary<string, IniModifierCreator> validNodes)
        {
            Dictionary<string, List<IniModifier>> modifiers = new();
            reader.GotoNextLine();
            while (IsStillCurrentSection())
            {
                string name = reader.ExtractModifierName().ToLower();
                if (validNodes.TryGetValue(name, out var node))
                {
                    var mod = node.CreateModifier(reader);
                    if (modifiers.TryGetValue(node.outputName, out var list))
                        list.Add(mod);
                    else
                        modifiers.Add(node.outputName, new() { mod });
                }
                reader.GotoNextLine();
            }
            return new IniSection(modifiers);
        }

        private bool GetDistanceToTrackCharacter(int position, out int i)
        {
            int distanceToEnd = length - position;
            i = 0;
            while (i < distanceToEnd)
            {
                if (data[position + i] == '[')
                    return true;
                ++i;
            }
            return false;
        }
    }
}
