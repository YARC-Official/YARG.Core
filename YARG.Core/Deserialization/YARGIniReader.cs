using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using YARG.Core.Deserialization.Ini;

namespace YARG.Core.Deserialization
{
    public unsafe class YARGIniReader
    {
        private readonly YARGTXTReader reader;
        private readonly byte* ptr;
        private readonly int length;

        private string sectionName = string.Empty;
        public string Section { get { return sectionName; } }

        public YARGIniReader(YARGTXTReader reader)
        {
            this.reader = reader;
            ptr = reader.Ptr;
            length = reader.Length;
        }

        public YARGIniReader(YARGFile file) : this(new YARGTXTReader(file)) { }

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

            sectionName = Encoding.UTF8.GetString(reader.CurrentPtr, reader.Length - reader.Position).TrimEnd().ToLower();
            return true;
        }

        public void SkipSection()
        {
            reader.GotoNextLine();
            int position = reader.Position;
            while (GetDistanceToTrackCharacter(position, out int next))
            {
                int point = position + next - 1;
                while (point > position && ptr[point] <= 32 && ptr[point] != '\n')
                    --point;

                if (ptr[point] == '\n')
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

        public Dictionary<string, List<IniModifier>> ExtractModifiers(ref Dictionary<string, IniModifierCreator> validNodes)
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
            return modifiers;
        }

        private bool GetDistanceToTrackCharacter(int position, out int i)
        {
            int distanceToEnd = length - position;
            byte* curr = ptr + position;
            i = 0;
            while (i < distanceToEnd)
            {
                if (curr[i] == '[')
                    return true;
                ++i;
            }
            return false;
        }
    }
}
