namespace YARG.Core.Engine.Logging
{
    public class ConsistentEngineEvent : BaseEngineEvent
    {
        public string Message = string.Empty;

        public ConsistentEngineEvent(double eventTime) : base(EngineEventType.Consistent, eventTime)
        {
        }

        public override bool Equals(BaseEngineEvent? engineEvent)
        {
            if (!base.Equals(engineEvent))
            {
                return false;
            }

            if (engineEvent?.GetType() != typeof(ConsistentEngineEvent)) return false;

            var consistentEngineEvent = engineEvent as ConsistentEngineEvent;

            return consistentEngineEvent?.Message == Message;
        }
    }
}