using System.Collections.Generic;
using YARG.Core.Chart;
using YARG.Core.Input;

namespace YARG.Core.Engine.Guitar
{
    public abstract class GuitarEngine : BaseEngine<GuitarNote, GuitarInput, GuitarAction, GuitarEngineParameters, 
        GuitarStats, GuitarEngineState>
    {
        protected GuitarEngine(List<GuitarNote> notes, GuitarEngineParameters engineParameters) : base(notes, engineParameters)
        {
        }
    }
}