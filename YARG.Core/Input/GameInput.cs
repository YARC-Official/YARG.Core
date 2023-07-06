using System;
using System.Runtime.InteropServices;

namespace YARG.Core.Input
{
    [StructLayout(LayoutKind.Explicit)]
    public readonly struct GameInput<TAction>
        where TAction : unmanaged, Enum
    {
        [FieldOffset(0)]
        // Not TAction, since we can't determine the
        // size of that ahead of time for FieldOffset
        private readonly int _action;

        public TAction Action => _action.Convert<TAction>();

        [field: FieldOffset(sizeof(int))]
        public double Time { get; }

        // For memory/serialization efficiency, a C/C++ union is being
        // emulated here by placing multiple fields at the same offset

        [field: FieldOffset(sizeof(int) + sizeof(double))]
        public int Integer { get; }

        [field: FieldOffset(sizeof(int) + sizeof(double))]
        public float Axis { get; }

        [field: FieldOffset(sizeof(int) + sizeof(double))]
        public bool Button { get; }

        public GameInput(TAction action, double time, int value) : this()
        {
            _action = action.Convert();
            Time = time;
            Integer = value;
        }

        public GameInput(TAction action, double time, float value) : this()
        {
            _action = action.Convert();
            Time = time;
            Axis = value;
        }

        public GameInput(TAction action, double time, bool value) : this()
        {
            _action = action.Convert();
            Time = time;
            Button = value;
        }
    }
}