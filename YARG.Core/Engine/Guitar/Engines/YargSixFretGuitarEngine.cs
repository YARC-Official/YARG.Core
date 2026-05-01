using System.Collections.Generic;
using YARG.Core.Chart;
using YARG.Core.Input;

namespace YARG.Core.Engine.Guitar.Engines
{
    /// <summary>
    /// Six-fret (Guitar Hero Live) guitar engine.
    /// </summary>
    /// <remarks>
    /// <para>
    /// GH Live uses 2 rows of 3 frets each:
    /// White keys (W): W1, W2, W3 (top/high row)
    /// Black keys (B): B1, B2, B3 (bottom/low row)
    /// </para>
    /// <para>
    /// <b>Core rule: only the fret number (1, 2, 3) matters, not the row (black/white).</b>
    /// </para>
    /// <para>
    /// <b>Hammer-on:</b> lower fret number → higher fret number.
    /// E.g. B1→W2 (fret 1→2), B2→W3 (fret 2→3).
    /// </para>
    /// <para>
    /// <b>Pull-off:</b> same fret number or higher → lower.
    /// E.g. W2→B2 (fret 2→2, same number), B2→W2 (fret 2→2, same number),
    /// W3→B2 (fret 3→2), B3→W1 (fret 3→1).
    /// </para>
    /// <para>
    /// <b>Vertical HOPO (same fret, different row):</b> always a pull-off.
    /// The originating fret must be released. E.g. W3→B3 requires releasing W3;
    /// B2→W2 requires releasing B2.
    /// </para>
    /// <para>
    /// <b>Anchoring:</b> held anchor frets must have a strictly lower fret number
    /// than the target note. E.g. B1(fret 1) anchored on W2(fret 2) is valid;
    /// B2(fret 2) anchored on W2(fret 2) is invalid.
    /// </para>
    /// <para>
    /// <b>Reversed arpeggios (right to left):</b> when playing extended sustains
    /// from right to left with nearby HOPO notes (e.g. B3→W2→B1 or W3→B2→W1),
    /// all corresponding frets can optionally be held before strumming the first note.
    /// </para>
    /// <para>
    /// <b>Chords:</b> HOPO chords and bar chords (2-note chords in same fret lane)
    /// are supported. All frets of a HOPO chord must be tapped simultaneously.
    /// </para>
    /// <para>
    /// Source: GuitarHero Fandom Wiki — "Hammer-ons and Pull-offs"
    /// (https://guitarhero.fandom.com/wiki/Hammer-ons_and_Pull-offs)
    /// </para>
    /// </remarks>
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

        /// <summary>
        /// Converts a bit position (from GetMostSignificantBit) to a GH Live fret number (1-3).
        /// Bit positions 0-5 map to frets B1-W3. Bit 6 is open, bit 7+ is wildcard.
        /// Mapping: B1=1, B2=2, B3=3, W1=1, W2=2, W3=3 (only fret number matters).
        /// </summary>
        private static int GetFretNumberFromBitPosition(int bitPosition)
        {
            // Open (bit 6) and wildcard (bit 7+) have no fret number
            if (bitPosition > 6)
            {
                return 0;
            }

            // Regular frets: bit 0-5 map to fret numbers 1-3
            // B1(0)→1, B2(1)→2, B3(2)→3, W1(3)→1, W2(4)→2, W3(5)→3
            return (bitPosition % 3) + 1;
        }

        /// <summary>
        /// Overrides ghost input detection for six-fret guitar.
        /// GH Live rule: only fret number matters, not row.
        /// Hammer-on: lower fret number → higher fret number (e.g. B1→W2).
        /// Pull-off: same or higher fret number → lower (e.g. W2→B2, B2→W2, B3→W1).
        /// </summary>
        protected override bool CheckForGhostInput(GuitarNote note)
        {
            // First note cannot be ghosted, nor can a note be ghosted if a button is unpressed (pulloff)
            if (note.PreviousNote is null || !IsFretPress)
            {
                return false;
            }

            // Note can only be ghosted if it's in timing window
            if (!IsNoteInWindow(note))
            {
                return false;
            }

            // GH Live: compare fret numbers (1-3), not bit positions
            int currentFretNumber = GetFretNumberFromBitPosition(GetMostSignificantBit(EffectiveButtonMask));
            int previousFretNumber = GetFretNumberFromBitPosition(GetMostSignificantBit(LastButtonMask));

            // Hammer-on only when fret number increases (e.g. fret 1 → fret 2)
            // Same fret number (e.g. B2→W2) is a pull-off, not a hammer-on
            bool isHammerOn = currentFretNumber > previousFretNumber;

            // Input is a hammer-on and the button pressed is not part of the note mask (incorrect fret)
            if (isHammerOn && (EffectiveButtonMask & note.NoteMask) == 0)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Overrides anchoring validation for six-fret guitar.
        /// GH Live rule: anchor fret numbers must be strictly lower than target fret number.
        /// E.g. anchoring B1(fret 1) on W2(fret 2) is valid, but B2(fret 2) on W2(fret 2) is not.
        /// </summary>
        protected override bool IsAnchoringValid(int anchorButtons, int targetFretValue)
        {
            if (anchorButtons == 0)
            {
                return true;
            }

            // targetFretValue is a bit mask (e.g. 1 << fretBitPosition)
            // Convert to fret number for comparison
            int targetFretNumber = GetFretNumberFromBitPosition(GetMostSignificantBit(targetFretValue));

            // Check each anchor fret individually
            for (int i = 0; i < 6; i++)
            {
                if ((anchorButtons & (1 << i)) != 0)
                {
                    int anchorFretNumber = GetFretNumberFromBitPosition(i);
                    if (anchorFretNumber >= targetFretNumber)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        protected override byte[] CreateCodaFretMask() => new byte[6];

        protected override int GetCodaFretCount() => 6;
    }
}
