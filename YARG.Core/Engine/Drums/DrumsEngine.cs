using System.Collections.Generic;
using YARG.Core.Chart;
using YARG.Core.Input;

namespace YARG.Core.Engine.Drums
{
    public abstract class DrumsEngine : BaseEngine<DrumNote, DrumAction, DrumsEngineParameters,
        DrumStats, DrumsEngineState>
    {
        protected DrumsEngine(List<DrumNote> notes, DrumsEngineParameters engineParameters) : base(notes, engineParameters)
        {
        }
    }
}