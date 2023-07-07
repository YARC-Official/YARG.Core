using System;
using System.Runtime.CompilerServices;

namespace YARG.Core.Input
{
    public readonly struct GameInput<TAction>
        where TAction : unmanaged, Enum
    {
        public TAction Action { get; }

        public double Time { get; }

        private readonly int _value;

        public int Integer => _value;

        public float Axis => GetValue<float>(_value);

        public bool Button => GetValue<bool>(_value);

        public GameInput(TAction action, double time, int value) : this()
        {
            Action = action;
            Time = time;
            _value = SetValue(value);
        }

        public GameInput(TAction action, double time, float value) : this()
        {
            Action = action;
            Time = time;
            _value = SetValue(value);
        }

        public GameInput(TAction action, double time, bool value) : this()
        {
            Action = action;
            Time = time;
            _value = SetValue(value);
        }

        private static T GetValue<T>(int value)
            where T : unmanaged
        {
            byte bValue = (byte)value;
            short sValue = (short)value;
            return Unsafe.SizeOf<T>() switch
            {
                sizeof(byte) => Unsafe.As<byte, T>(ref bValue),
                sizeof(short) => Unsafe.As<short, T>(ref sValue),
                sizeof(int) => Unsafe.As<int, T>(ref value),
                _ => throw new ArgumentException($"Cannot convert to {typeof(T).Name} from an int!")
            };
        }

        private static int SetValue<T>(T value)
            where T : unmanaged
        {
            return Unsafe.SizeOf<T>() switch
            {
                sizeof(byte) => Unsafe.As<T, byte>(ref value),
                sizeof(short) => Unsafe.As<T, short>(ref value),
                sizeof(int) => Unsafe.As<T, int>(ref value),
                _ => throw new ArgumentException($"Cannot convert from {typeof(T).Name} to an int!")
            };
        }
    }
}