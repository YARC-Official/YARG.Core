using System.Collections.Generic;
using YARG.Core.Chart;
using YARG.Core.Input;

namespace YARG.Core.Engine.Guitar.Engines
{
    public class YargSixFretGuitarEngine : YargFiveFretGuitarEngine
    {
        public YargSixFretGuitarEngine(InstrumentDifficulty<GuitarNote> chart, SyncTrack syncTrack,
            GuitarEngineParameters engineParameters, bool isBot)
            : base(chart, syncTrack, engineParameters, isBot)
        {
        }

        protected override int GetChordLowestFretMask(GuitarNote note)
        {
            var chordMask = 0;
            for (var fret = GuitarAction.GreenFret; fret <= GuitarAction.White3Fret; fret++)
            {
                chordMask = 1 << (int) fret;

                // If the current fret mask is part of the chord, break
                if ((chordMask & note.NoteMask) == chordMask)
                {
                    break;
                }
            }

            return chordMask;
        }

        protected override byte[] CreateCodaFretMask() => new byte[6];

        protected override int GetCodaFretCount() => 6;
    }
}
