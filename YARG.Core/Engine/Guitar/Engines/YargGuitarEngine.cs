using System.Collections.Generic;
using YARG.Core.Chart;

namespace YARG.Core.Engine.Guitar.Engines
{
    public class YargGuitarEngine : GuitarEngine
    {

        public YargGuitarEngine(List<GuitarNote> notes, GuitarEngineParameters engineParameters) : base(notes, engineParameters)
        {
            
        }

        protected override void ProcessInputs()
        {
            throw new System.NotImplementedException();
        }

        protected override bool UpdateHitLogic(double time)
        {
            throw new System.NotImplementedException();
        }

        protected override bool CanNoteBeHit(GuitarNote note)
        {
            throw new System.NotImplementedException();
        }
    }
}