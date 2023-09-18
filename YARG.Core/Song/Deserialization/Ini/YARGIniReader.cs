using System.Collections.Generic;
using System.Text;

namespace YARG.Core.Song.Deserialization.Ini
{
    public sealed class YARGIniReader : IIniReader
    {
        private readonly YARGTXTReader reader;

        private string sectionName = string.Empty;
        public string Section { get { return sectionName; } }

        public YARGIniReader(YARGTXTReader reader)
        {
            this.reader = reader;
        }

        public bool IsStartOfSection()
        {
            if (reader.IsEndOfFile())
                return false;

            if (reader.Peek() != '[')
            {
                SkipSection();
                if (reader.IsEndOfFile())
                    return false;
            }

            int position = reader.Position;
            sectionName = Encoding.UTF8.GetString(reader.Data, position, reader.Next - position).TrimEnd().ToLower();
            return true;
        }

        public void SkipSection()
        {
            reader.GotoNextLine();
            int position = reader.Position;
            while (GetDistanceToTrackCharacter(position, out int next))
            {
                int point = position + next - 1;
                while (point > position)
                {
                    char character = (char) reader.Data[point];
                    if (!ITXTReader.IsWhitespace(character) || character == '\n')
                        break;
                    --point;
                }

                if (reader.Data[point] == '\n')
                {
                    reader.Position = position + next;
                    reader.SetNextPointer();
                    return;
                }

                position += next + 1;
            }

            reader.Position = reader.Length;
            reader.SetNextPointer();
        }

        public bool IsStillCurrentSection()
        {
            return !reader.IsEndOfFile() && reader.Peek() != '[';
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
            int distanceToEnd = reader.Length - position;
            i = 0;
            while (i < distanceToEnd)
            {
                if (reader.Data[position + i] == '[')
                    return true;
                ++i;
            }
            return false;
        }
    }
}
