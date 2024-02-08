using System;

namespace YARG.Core.Utility
{
    public interface IBinaryDataWriter : IDisposable
    {
        public void Write(bool value);
        public void Write(byte value);
        public void Write(byte[] buffer);
        public void Write(byte[] buffer, int index, int count);
        public void Write(char ch);
        public void Write(char[] chars);
        public void Write(char[] chars, int index, int count);
        public void Write(decimal value);
        public void Write(double value);
        public void Write(short value);
        public void Write(int value);
        public void Write(long value);
        public void Write(ReadOnlySpan<byte> buffer);
        public void Write(ReadOnlySpan<char> buffer);
        public void Write(sbyte value);
        public void Write(float value);
        public void Write(string value);
        public void Write(ushort value);
        public void Write(uint value);
        public void Write(ulong value);
    }
}