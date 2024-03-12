using System;
using Cysharp.Text;

namespace YARG.Core.Logging
{
    public class ConsoleYargLogListener : BaseYargLogListener
    {
        public ConsoleYargLogListener(IYargLogFormatter formatter) : base(formatter)
        {
        }

        public override void WriteLogItem(ref Utf16ValueStringBuilder output)
        {
            var segment = output.AsArraySegment();
            Console.WriteLine(segment.Array!, segment.Offset, segment.Count);
        }
    }
}