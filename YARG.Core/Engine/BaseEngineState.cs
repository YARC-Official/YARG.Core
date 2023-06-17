namespace YARG.Core.Engine
{
    public abstract class BaseEngineState
    {

        public int NoteIndex;

        public virtual void Reset()
        {
            NoteIndex = 0;
        }
        
    }
}