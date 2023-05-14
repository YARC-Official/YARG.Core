using System;

namespace YARG.Core.Input
{
    public abstract class AbstractGameInput<TAction> where TAction : Enum
    {

        public TAction Action { get; }
        
        public int RawValue { get; protected set; }
        
        public ActionType Type { get; }
        public double     Time { get; set; }
        
        protected AbstractGameInput(TAction action, double time, ActionType type)
        {
            Action = action;
            Time = time;
            Type = type;
        }
    }
}