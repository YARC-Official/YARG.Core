using System;

namespace YARG.Core.IO
{
    public abstract class YARGTextContainer<TChar>
        where TChar : IConvertible
    {
        protected int _next;
        public readonly TChar[] Data;
        public readonly int Length;
        public int Position;
        
        public int Next => _next;
        
        protected YARGTextContainer(TChar[] data)
        {
            Data = data;
            Length = data.Length;
        }

        public bool IsCurrentCharacter(char cmp)
        {
            return Data[Position].ToChar(null).Equals(cmp);
        }

        public bool IsEndOfFile()
        {
            return Position >= Length;
        }

        public abstract char SkipWhiteSpace();
    }
}
