using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using YARG.Core.Extensions;

namespace YARG.Core.Input
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct GameInput
    {
        public double Time { get; }
        public int    Action { get; }

        private readonly int _value;

        public int   Integer => _value;
        public float Axis => GetValue<float>(_value);
        public bool  Button => GetValue<bool>(_value);

        public GameInput(double time, int action, int value) : this()
        {
            Time = time;
            Action = action;
            _value = SetValue(value);
        }

        public GameInput(double time, int action, float value) : this()
        {
            Time = time;
            Action = action;
            _value = SetValue(value);
        }

        public GameInput(double time, int action, bool value) : this()
        {
            Time = time;
            Action = action;
            _value = SetValue(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static GameInput Create<TAction>(double time, TAction action, int value)
            where TAction : unmanaged, Enum
        {
            return new GameInput(time, action.Convert(), value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static GameInput Create<TAction>(double time, TAction action, float value)
            where TAction : unmanaged, Enum
        {
            return new GameInput(time, action.Convert(), value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static GameInput Create<TAction>(double time, TAction action, bool value)
            where TAction : unmanaged, Enum
        {
            return new GameInput(time, action.Convert(), value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TAction GetAction<TAction>()
            where TAction : unmanaged, Enum
        {
            return Action.Convert<TAction>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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