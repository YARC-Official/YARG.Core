using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using YARG.Core.Extensions;

namespace YARG.Core.Input
{
    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    public readonly struct GameInput
    {
        [FieldOffset(0)]
        public readonly double Time;
        [FieldOffset(8)]
        public readonly int Action;

        // Union emulation, each of these fields will share the same memory
        [FieldOffset(12)]
        public readonly int Integer;
        [FieldOffset(12)]
        public readonly float Axis;
        [FieldOffset(12)]
        public readonly bool Button;

        public GameInput(double time, int action, int value) : this()
        {
            Time = time;
            Action = action;
            Integer = value;
        }

        public GameInput(double time, int action, float value) : this()
        {
            Time = time;
            Action = action;
            Axis = value;
        }

        public GameInput(double time, int action, bool value) : this()
        {
            Time = time;
            Action = action;
            Button = value;
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
    }
}