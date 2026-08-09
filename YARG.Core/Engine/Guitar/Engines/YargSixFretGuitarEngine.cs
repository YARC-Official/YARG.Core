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
    /// <b>Fret numbering:</b> each fret has a number 1-3. B1=1, B2=2, B3=3,
    /// W1=1, W2=2, W3=3. The fret number is used solely to determine HOPO
    /// direction (hammer-on vs pull-off). Color (black/white) matters for
    /// hit detection and anchoring — <b>frets are NOT interchangeable</b>.
    /// </para>
    /// <para>
    /// Each fret (B1, W1, B2, W2, B3, W3) is a
    /// distinct button. Holding B1 does not satisfy a W1 note, and vice versa.
    /// </para>
    /// <para>
    /// <b>Anchoring:</b> frets with a strictly lower fret number than the target
    /// note can optionally be held as anchors (e.g. B2 anchors W3). Same-fret-number
    /// buttons in a different row (e.g. B3 anchoring W3) are NOT valid anchors.
    /// </para>
    /// <para>
    /// <b>HOPO rules:</b>
    /// Hammer-on: lower fret number → higher fret number (e.g. B1→W2, B2→B3).
    /// Pull-off: same or lower fret number → lower (e.g. W2→B2, W3→B2, B3→W1).
    /// Only frets to the left of a HOPO note (regardless of color) can optionally
    /// be held.
    /// </para>
    /// <para>
    /// <b>Vertical HOPO (same fret number, different row, e.g. B1→W1 or B2→W2):</b>
    /// always treated as a pull-off. The originating fret must be released —
    /// if it is still held, the input is a ghost.
    /// </para>
    /// <para>
    /// <b>Chords:</b> HOPO chords and barres require exact button presses.
    /// All frets of a chord must be tapped simultaneously — no interchangeability.
    /// </para>
    /// <para>
    /// Source: GuitarHero Fandom Wiki — "Hammer-ons and Pull-offs"
    /// (https://guitarhero.fandom.com/wiki/Hammer-ons_and_Pull-offs)
    /// </para>
    /// </remarks>
    public class YargSixFretGuitarEngine : YargFiveFretGuitarEngine
    {
        /// <summary>
        /// Bit mask for the 6 fret buttons (B1-W3). Excludes Open (bit 6) and Wildcard (bit 7).
        /// </summary>
        private const int FRET_BUTTON_MASK = 0x3F;

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
        /// Converts a 0-indexed bit position (0-5) to a GH Live fret number (1-3).
        /// Bit 0=B1, 1=B2, 2=B3, 3=W1, 4=W2, 5=W3 → fret numbers 1,2,3,1,2,3.
        /// Returns 0 for out-of-range positions (Open, Wildcard, etc.).
        /// </summary>
        private static int GetFretNumberFromBitPosition(int bitPosition)
        {
            if (bitPosition is < 0 or > 5)
            {
                return 0;
            }

            return (bitPosition % 3) + 1;
        }

        /// <summary>
        /// Finds the fret with the highest fret number (1-3) in a mask, considering only
        /// fret buttons (bits 0-5). In 6-fret, B3 (bit 2) has a lower bit position than
        /// W1 (bit 3), but B3 is fret 3 (higher than W1's fret 1). We must iterate all
        /// bits to find the highest fret number rather than relying on <see cref="GetMostSignificantBit"/>
        /// which only finds the highest bit position.
        /// Returns the 0-indexed bit position of that fret, or -1 if no fret buttons are held.
        /// </summary>
        private static int GetHighestFretBitPosition(int mask)
        {
            int fretBits = mask & FRET_BUTTON_MASK;
            if (fretBits == 0)
            {
                return -1;
            }

            int maxFretNumber = 0;
            int maxBitPosition = -1;
            for (int i = 0; i < 6; i++)
            {
                if ((fretBits & (1 << i)) != 0)
                {
                    int fretNumber = GetFretNumberFromBitPosition(i);
                    if (fretNumber > maxFretNumber || (fretNumber == maxFretNumber && i > maxBitPosition))
                    {
                        maxFretNumber = fretNumber;
                        maxBitPosition = i;
                    }
                }
            }
            return maxBitPosition;
        }

        /// <summary>
        /// Overrides ghost input detection for six-fret guitar.
        /// Determines whether a fret press during a HOPO sequence is a valid
        /// hammer-on or pull-off, or an invalid (ghost) input.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <b>HOPO direction</b> is determined by fret number (1-3), not bit position:
        /// - Hammer-on: fret number increases (e.g. B1→W2, fret 1→2).
        /// - Pull-off: same or lower fret number (e.g. W2→B2, W3→B1).
        /// </para>
        /// <para>
        /// <b>No interchangeability</b>: the note's exact fret must be held.
        /// Pressing W1 for a B1 note is NOT valid and IS a ghost input.
        /// </para>
        /// <para>
        /// <b>Vertical HOPO</b> (same fret number, different row, e.g. B1→W1, B2→W2):
        /// always treated as a pull-off. The originating fret must be released —
        /// if it is still held, the input is a ghost.
        /// </para>
        /// </remarks>
        protected override bool CheckForGhostInput(GuitarNote note)
        {
            // First note cannot be ghosted, nor can a note be ghosted if a button is unpressed (pull-off)
            if (note.PreviousNote is null || !IsFretPress)
            {
                return false;
            }

            // Note can only be ghosted if it's in timing window
            if (!IsNoteInWindow(note))
            {
                return false;
            }

            // Only fret button bits (0-5) matter for HOPO; exclude open/wildcard (bits 6+)
            int currentFrets = EffectiveButtonMask & FRET_BUTTON_MASK;
            int lastFrets = LastButtonMask & FRET_BUTTON_MASK;

            // Need fret buttons held in both states to determine HOPO direction
            if (currentFrets == 0 || lastFrets == 0)
            {
                return false;
            }

            // Fret numbers (1-3) of the highest fret held in each state.
            // We must iterate all bits to find the highest fret number, because in 6-fret
            // the highest bit position does not always correspond to the highest fret number
            // (e.g. B3 is bit 2 but W1 is bit 3; B3 is fret 3, W1 is fret 1).
            int currentBitPosition = GetHighestFretBitPosition(currentFrets);
            int lastBitPosition = GetHighestFretBitPosition(lastFrets);

            if (currentBitPosition < 0 || lastBitPosition < 0)
            {
                return false;
            }

            int currentFretNumber = GetFretNumberFromBitPosition(currentBitPosition);
            int previousFretNumber = GetFretNumberFromBitPosition(lastBitPosition);

            // Hammer-on: fret number increases (e.g. fret 1 → fret 2)
            bool isHammerOn = currentFretNumber > previousFretNumber;

            // Vertical transition: same fret number, different row (e.g. B1→W1, B2→W2)
            bool isVerticalTransition = currentFretNumber == previousFretNumber;

            // Note's exact fret mask (no interchangeable equivalents — every fret is distinct)
            int noteFretMask = note.NoteMask & FRET_BUTTON_MASK;
            if (noteFretMask == 0)
            {
                return false; // Open/wildcard notes are not subject to ghost checks
            }

            // Hammer-on check: the note's exact fret must be among the held frets.
            // If not, the player hammered on to a wrong fret → ghost.
            if (isHammerOn && (currentFrets & noteFretMask) == 0)
            {
                if (!IsGhostInTrillLeniencyWindow(currentBitPosition))
                {
                    return true;
                }
            }

            // Vertical transition check (single notes only): the originating fret
            // (same number, different row) must be released. If it is still held,
            // the input is a ghost.
            if (isVerticalTransition && !note.IsChord)
            {
                int commonFrets = currentFrets & lastFrets;
                if (commonFrets != 0)
                {
                    // noteFretBit is the bit position of the note's fret
                    int noteFretBit = GetMostSignificantBit(noteFretMask) - 1;
                    for (int i = 0; i < 6; i++)
                    {
                        if ((commonFrets & (1 << i)) != 0)
                        {
                            int heldFretNumber = GetFretNumberFromBitPosition(i);
                            if (heldFretNumber == currentFretNumber && i != noteFretBit)
                            {
                                // Originating fret (same number, different row) still held → ghost
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Overrides anchoring validation for six-fret guitar.
        /// Frets are NOT interchangeable — each button (B1, W1, B2, W2, B3, W3)
        /// is distinct. The target fret must be held; only frets with a strictly
        /// lower fret number can be held as additional anchors.
        /// </summary>
        protected override bool IsAnchoringValid(int anchorButtons, int targetFretValue)
        {
            if (anchorButtons == 0)
            {
                return true;
            }

            // targetFretValue is a single-bit mask. GetMostSignificantBit returns bit_position + 1,
            // so subtract 1 for the 0-indexed bit position.
            int targetBitPosition = GetMostSignificantBit(targetFretValue) - 1;
            if (targetBitPosition < 0)
            {
                // targetFretValue == 0 means an open note target. Any extra fret (anchorButtons != 0)
                // makes an open note unhittable via anchoring, matching the base 5-fret behavior
                // where GetMostSignificantBit(anchorButtons) < 0 is always false.
                return false;
            }
            int targetFretNumber = GetFretNumberFromBitPosition(targetBitPosition);

            // Target fret must be held (i.e., NOT in the XOR-based anchorButtons,
            // which contains the target bit only when it is missing from the held buttons).
            // Since frets are NOT interchangeable, the exact target fret is required.
            if ((anchorButtons & targetFretValue) != 0)
            {
                // Target fret is in anchorButtons → it was NOT held → anchoring invalid
                return false;
            }

            // Check each anchor bit: must have a strictly lower fret number
            for (int i = 0; i < 6; i++)
            {
                if ((anchorButtons & (1 << i)) != 0)
                {
                    int anchorFretNumber = GetFretNumberFromBitPosition(i);
                    if (anchorFretNumber >= targetFretNumber)
                    {
                        // Anchor fret is same or higher than target — invalid
                        // (same fret number, different row is NOT interchangeable)
                        return false;
                    }
                    // anchorFretNumber < targetFretNumber — valid anchor, continue
                }
            }

            return true;
        }

        protected override byte[] CreateCodaFretMask() => new byte[6];

        protected override int GetCodaFretCount() => 6;
    }
}
