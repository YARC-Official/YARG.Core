using System;
using System.Collections.Generic;
using System.Linq;
using YARG.Core.Chart;
using YARG.Core.Input;

namespace YARG.Core.Engine.Vocals.Engines
{
    /// <summary>
    /// Free-match vocals engine that allows a single mic to match notes across ALL HARM
    /// parts simultaneously, not just one. Subclasses <see cref="YargVocalsEngine"/> to
    /// inherit the concrete pitch-matching formula (<see cref="YargVocalsEngine.CanVocalNoteBeHit"/>)
    /// and the solo bot, then overrides the hit-detection orchestration to traverse every
    /// part at the current tick and accumulate per-part fractional hits + a per-tick bitmask.
    ///
    /// Designed as a sub-engine for <see cref="PartyVocalsCoordinatorEngine"/> (one per mic),
    /// but also usable standalone for single-mic free vocals.
    /// </summary>
    public class YargFreeVocalsEngine : YargVocalsEngine
    {
        public int CurrentTargetHarmonyIndex { get; private set; }

        /// <summary>
        /// Whether each HARM part has any phrase content in this chart. The Harmony
        /// track always exposes 3 placeholder parts even if the song only charts
        /// HARM1+HARM2, so the HUD uses this to hide empty-lane meters.
        /// </summary>
        public bool PartHasContent(int partIndex)
        {
            if (partIndex < 0 || partIndex >= _allParts.Count) return false;
            return _allParts[partIndex].NotePhrases.Count > 0;
        }

        public int PartCount => _allParts.Count;

        /// <summary>
        /// Per-tick deltas credited to each part for the current tick. This property
        /// is updated in UpdateHitLogic after per-part credit is committed, allowing
        /// external systems to read the exact credit assigned for this tick.
        /// </summary>
        public IReadOnlyList<double> LastTickPartDeltas => _lastTickPartDeltas;

        /// <summary>
        /// Chart tick at which the most recent per-part credit accumulation began.
        /// Together with <see cref="LastTickPartDeltas"/> this gives the exact
        /// covered span of the just-committed credit, ending at the earlier of
        /// the current tick and the scoring phrase end. Only meaningful on a tick where
        /// a delta was committed (the masks/deltas are zero otherwise). Used by
        /// PartyVocalsCoordinatorEngine to track the temporal union of
        /// broadcast-covered ticks per part.
        /// </summary>
        public uint LastCreditSpanStartTick { get; private set; }

        // Store reference to all parts for hit testing
        protected readonly IReadOnlyList<VocalsPart> _allParts;
        private readonly int _botPartIndex;

        // Per-part delta for the current tick. Updated in UpdateHitLogic after
        // per-part credit is committed, for external consumption (e.g., coordinator).
        private readonly double[] _lastTickPartDeltas;

        // Per-part hit accumulator for single-mic free vocals
        private readonly double[] _singleMicPartHits;
        // Bitmask of parts that the single mic is hitting this tick
        private uint _singleMicHittingParts;
        // Bitmask of the parts above whose CURRENT matched note is pitched (has a real
        // MIDI pitch). A bit set in _singleMicHittingParts but not here means that part
        // was matched through a talkie (non-pitched) note this tick. Consumed by
        // PartyVocalsCoordinatorEngine to classify a mic's matched set as all-talkie
        // (broadcast credit) vs any-pitched (conserved bucket).
        private uint _singleMicHittingPitchedParts;

        public YargFreeVocalsEngine(
            InstrumentDifficulty<VocalNote> primaryChart,
            IReadOnlyList<VocalsPart> allParts,
            SyncTrack syncTrack,
            VocalsEngineParameters engineParameters,
            bool isBot,
            int botPartIndex = 0,
            bool isSubEngine = false)
            : base(primaryChart, syncTrack, engineParameters, isBot)
        {
            // As a PartyVocalsCoordinatorEngine sub-engine, this instance shares its
            // note objects with the coordinator (and the other mics). It is not
            // authoritative for star power, so it must not strip SP flags on a miss —
            // otherwise one mic's miss clears SP from a phrase the coordinator hit.
            StripStarPowerOnMiss = !isSubEngine;

            _allParts = allParts;
            _botPartIndex = Math.Max(0, Math.Min(botPartIndex, allParts.Count - 1));

            // Initialize fields for single-mic per-part accumulation
            _lastTickPartDeltas = new double[allParts.Count];
            _singleMicPartHits = new double[allParts.Count];
            _singleMicHittingParts = 0u;
            _singleMicHittingPitchedParts = 0u;

            // Build countdowns from all parts for free vocals; exclude percussion so
            // percussion-only stretches show the countdown wheel instead of being
            // hidden as a continuous note stream.
            GetWaitCountdowns(PartyVocalsCountdownNotes.ExcludingPercussion(allParts.ToList()));
        }

        public override void Reset(bool keepCurrentButtons = false)
        {
            // The hitting masks are per-tick transients normally re-cleared by
            // AccumulateMicPartHits; a rewind/practice seek must not leave the
            // pre-seek match exposed through GetMicHittingParts().
            // (Safety: BaseEngine's constructor calls this virtual before the
            // derived fields exist — these two are plain scalar fields with
            // inline initializers, so clearing them here is safe; nothing
            // array-backed is touched.)
            _singleMicHittingParts = 0u;
            _singleMicHittingPitchedParts = 0u;

            base.Reset(keepCurrentButtons);
        }

        private VocalNote? FindBotTargetInPart(int partIndex, out bool hasActivePhrase)
        {
            hasActivePhrase = false;
            foreach (var partPhrase in _allParts[partIndex].NotePhrases)
            {
                if (partPhrase.Tick > CurrentTick)
                    break;

                hasActivePhrase |= CurrentTick <= partPhrase.TickEnd;
                // The note can still be sounding after the phrase that owns it has ended.
                foreach (var note in partPhrase.PhraseParentNote.ChildNotes)
                {
                    if (note.Tick > CurrentTick)
                        break;
                    if (!note.IsPercussion && CurrentTick <= note.TotalTickEnd)
                        return note;
                }
            }
            return null;
        }

        protected override void UpdateBot(double songTime)
        {
            if (!IsBot)
            {
                return;
            }

            IsStarPowerInputActive = CanStarPowerActivate && !IsStarPowerInputActive;

            var phrase = Notes[NoteIndex];

            // Find the active phrase for the bot. Prefer the assigned _botPartIndex; if no
            // phrase covers the current tick there, fall back to the lowest-numbered part
            // that does have an active phrase. This keeps a bot audible on sections where
            // its assigned HARM line isn't charted (common when a song collapses dual leads
            // into HARM1/2/3 but leaves gaps on individual parts).
            var singNote = FindBotTargetInPart(_botPartIndex, out bool hasActivePhrase);
            if (singNote is null && !hasActivePhrase)
            {
                for (int i = 0; i < _allParts.Count; i++)
                {
                    if (i == _botPartIndex) continue;
                    singNote = FindBotTargetInPart(i, out hasActivePhrase);
                    if (singNote is not null || hasActivePhrase)
                        break;
                }
            }
            if (singNote is not null)
            {
                // Bots are queued extra updates to account for in-between "inputs"
                PitchSang = singNote.PitchAtSongTime(songTime);
                HasSang = true;

                OnSing?.Invoke(true);

                // Drive the visual "on notes" state for bots: VocalsPlayer's needle path
                // anchors to _lastTargetNote when _lastHitTime is recent, otherwise it
                // applies AnchorPitchToOctave which adds a 12-semitone offset when
                // _lastTargetNote is null. This is the *sole* OnTargetNoteChanged source
                // for bots — CheckSingingHit deliberately skips its emit when IsBot (see
                // there) so it can't fight this one and make the needle jump to harm1.
                OnTargetNoteChanged?.Invoke(singNote);
                OnHit?.Invoke(true);
            }
            else
            {
                // Stop hitting to prevent the hit particles from showing up too much
                OnHit?.Invoke(false);
            }

            // Handle percussion notes
            var percussion = GetNextPercussionNote(phrase, CurrentTick);
            if (percussion is not null && songTime >= percussion.Time)
            {
                HasHit = true;
            }
        }

        protected override void UpdateHitLogic(double time)
        {
            // Quit early if there are no notes left
            if (NoteIndex >= Notes.Count)
            {
                HasSang = false;
                // Chart exhausted: AccumulateMicPartHits (which re-clears these
                // masks every tick) never runs again, so clear them here or
                // GetMicHittingParts()/GetMicHittingPitchedParts() would keep
                // exposing the last match forever after the chart ends.
                _singleMicHittingParts = 0u;
                _singleMicHittingPitchedParts = 0u;
                return;
            }

            UpdateBot(time);

            var phrase = Notes[NoteIndex];
            PhraseTicksTotal ??= GetTicksInPhrase(phrase);

            // Save singing state before CheckSingingHit consumes HasSang / LastSingTick
            bool wasSinging = HasSang;
            uint savedLastSingTick = LastSingTick;

            CheckForNoteHit();

            // Snapshot per-part hits before accumulation to compute per-tick delta
            // (coordinator reads LastTickPartDeltas every tick for ambiguity scoring)
            Span<double> prevHits = stackalloc double[_allParts.Count];
            for (int j = 0; j < _allParts.Count; j++)
                prevHits[j] = _singleMicPartHits[j];

            // Per-part hit accumulation using pre-consumption singing state
            AccumulateMicPartHits(wasSinging, savedLastSingTick, out _);

            // Compute per-tick delta for external consumption (coordinator reads these every tick)
            for (int j = 0; j < _allParts.Count; j++)
                _lastTickPartDeltas[j] = _singleMicPartHits[j] - prevHits[j];

            // Check for the end of a phrase
            if (CurrentTick > phrase.TickEnd)
            {
                bool hasNotes = PhraseTicksTotal.Value != 0;
                bool isLastPhrase = NoteIndex == Notes.Count - 1;

                // For single-mic free vocals, reset per-phrase state and run the standard phrase-end flow
                // Note: _singleMicPartHits is maintained for single-mic HUD display
                var percentHit = PhraseTicksHit / PhraseTicksTotal.Value;
                if (!hasNotes)
                {
                    percentHit = 1.0;
                }

                bool hit = percentHit >= EngineParameters.PhraseHitPercent;
                if (hit)
                {
                    EngineStats.TicksHit += PhraseTicksTotal.Value;
                    HitNote(phrase);
                }
                else
                {
                    var ticksHit = (uint) Math.Round(PhraseTicksHit);

                    EngineStats.TicksHit += ticksHit;
                    EngineStats.TicksMissed += PhraseTicksTotal.Value - ticksHit;

                    MissNote(phrase, percentHit);
                }

                PhraseTicksHit = 0;
                PhraseTicksTotal = null;

                if (hasNotes)
                {
                    OnPhraseHit?.Invoke(percentHit / EngineParameters.PhraseHitPercent, hit, isLastPhrase);
                }

                UpdateCarriedNote(phrase);
            }
        }

        protected override void CheckForNoteHit()
        {
            CheckSingingHit();
            CheckPercussionHit();
        }

        private bool AccumulateMicPartHits(bool wasSinging, uint savedLastSingTick, out VocalNote? representativeHitNote)
        {
            var maxLeniency = 1.0 / EngineParameters.ApproximateVocalFps;
            bool anyMicHit = false;
            representativeHitNote = null;

            // Reset the "currently hitting parts" bitmasks for single-mic
            _singleMicHittingParts = 0u;
            _singleMicHittingPitchedParts = 0u;

            if (!wasSinging)
                return false;

            var lastTick = Math.Max(
                SyncTrack.TimeToTick(CurrentTime - maxLeniency),
                savedLastSingTick);
            if (CurrentTick <= lastTick)
                return false;

            // A note can stay active between phrases, but only its overlap with the current
            // scoring window contributes to the meter or talkie coverage.
            var phrase = Notes[NoteIndex];
            uint scoringStart = Math.Max(lastTick, phrase.Tick);
            uint scoringEnd = Math.Min(CurrentTick, phrase.TickEnd);
            uint scoringTicks = scoringEnd > scoringStart ? scoringEnd - scoringStart : 0;
            LastCreditSpanStartTick = scoringStart;

            // Accumulate hits for all parts to feed the HUD's HARM1/2/3 %
            for (int partIndex = 0; partIndex < _allParts.Count; partIndex++)
            {
                foreach (var partPhrase in _allParts[partIndex].NotePhrases)
                {
                    foreach (var note in partPhrase.PhraseParentNote.ChildNotes)
                    {
                        if (note.IsPercussion) continue;
                        if (CurrentTick < note.Tick || CurrentTick > note.TotalTickEnd) continue;

                        if (CanVocalNoteBeHit(note, out float hitPercent))
                        {
                            _singleMicPartHits[partIndex] += scoringTicks * hitPercent;
                            if (hitPercent > 0f)
                            {
                                anyMicHit = true;
                                representativeHitNote ??= note;
                                _singleMicHittingParts |= 1u << partIndex;
                                if (!note.IsNonPitched)
                                {
                                    _singleMicHittingPitchedParts |= 1u << partIndex;
                                }
                            }
                        }
                    }
                }
            }

            return anyMicHit;
        }

        private void CheckSingingHit()
        {
            if (!HasSang)
            {
                return;
            }

            HasSang = false;
            var lastSingTick = LastSingTick;
            LastSingTick = CurrentTick;

            // If the last sing detected was on the same tick (or less), skip it
            // since we've already handled that tick.
            if (lastSingTick >= CurrentTick)
            {
                return;
            }

            // Find the current phrase
            if (NoteIndex >= Notes.Count)
            {
                return;
            }

            var phrase = Notes[NoteIndex];

            // Check for singing hits against all parts
            bool hitAnyNote = false;
            float bestHitPercent = 0f;
            int bestPartIndex = CurrentTargetHarmonyIndex;
            VocalNote? bestNote = null;

            // Tie hysteresis: also track the CURRENT target part's own matched percent.
            // The ascending-index strict-`>` scan below makes the lowest-index maximizer
            // win every frame, so an equal-percent candidate would displace the part
            // being displayed. Seeding the switch decision with the current part's own
            // percent means a candidate must be STRICTLY greater to take over.
            bool currentPartMatched = false;
            float currentPartPercent = 0f;
            VocalNote? currentPartNote = null;

            // Check each part for active notes
            for (int partIndex = 0; partIndex < _allParts.Count; partIndex++)
            {
                var part = _allParts[partIndex];

                // Get notes from this part's phrases
                foreach (var partPhrase in part.NotePhrases)
                {
                    foreach (var note in partPhrase.PhraseParentNote.ChildNotes)
                    {
                        if (!note.IsPercussion &&
                            CurrentTick >= note.Tick &&
                            CurrentTick <= note.TotalTickEnd)
                        {
                            if (CanVocalNoteBeHit(note, out float hitPercent))
                            {
                                hitAnyNote = true;

                                if (partIndex == CurrentTargetHarmonyIndex)
                                {
                                    // ANY match on the current part marks it matched — even a
                                    // 0% window-boundary match — so the retain branch below can
                                    // supply the current part's note in the all-zero edge. Capture
                                    // the FIRST matched note even at 0% (bestNote is null there),
                                    // then only upgrade the recorded percent/note when strictly greater.
                                    currentPartMatched = true;
                                    if (hitPercent > currentPartPercent)
                                    {
                                        currentPartPercent = hitPercent;
                                        currentPartNote = note;
                                    }
                                    else if (currentPartNote is null)
                                    {
                                        currentPartNote = note;
                                    }
                                }

                                // For free vocals, we take the best hit percent from any note
                                if (hitPercent > bestHitPercent)
                                {
                                    bestHitPercent = hitPercent;
                                    bestPartIndex = partIndex;
                                    bestNote = note;
                                }
                            }
                        }
                    }
                }
            }

            if (hitAnyNote)
            {
                // Retain the current part unless a candidate strictly beats its own
                // matched percent (equal candidates never displace; the current part's
                // note wins the target-note emit so the needle stays on the retained lane)
                if (currentPartMatched && bestHitPercent <= currentPartPercent)
                {
                    bestPartIndex = CurrentTargetHarmonyIndex;
                    bestNote = currentPartNote ?? bestNote;
                }

                // Update target harmony index only if it changed (retains last value when no match)
                if (bestPartIndex != CurrentTargetHarmonyIndex)
                {
                    CurrentTargetHarmonyIndex = bestPartIndex;
                }

                // Real mics: always fire target note change so visuals can snap to the
                // current note. On solo-only charts the part index never changes (always
                // 0), so an on-change guard would never fire — leaving slot.TargetNote
                // null and suppressing the trail.
                //
                // Bots are excluded: UpdateBot already emits OnTargetNoteChanged for the
                // bot's assigned/effective part every tick. If CheckSingingHit also fired
                // here — with the *globally* best-matching part, which is frequently harm1
                // on unison/octave-equivalent pitches — the two emits fight and the bot's
                // needle jumps between its own lane and harm1. Keeping this !IsBot is the
                // invariant UpdateBot's emit relies on.
                if (!IsBot)
                {
                    OnTargetNoteChanged?.Invoke(bestNote!);
                }

                // Scale the hit by chart ticks elapsed since the last sing, matching
                // YargVocalsEngine. PhraseTicksTotal is in chart ticks (hundreds to
                // thousands per phrase); previously we just added bestHitPercent
                // (a 0-1 value) once per UpdateHitLogic, so PhraseTicksHit could never
                // approach PhraseTicksTotal and every phrase graded as "messy" no matter
                // how perfectly the singer hit the notes.
                var maxLeniency = 1.0 / EngineParameters.ApproximateVocalFps;
                var lastTick = Math.Max(
                    SyncTrack.TimeToTick(CurrentTime - maxLeniency),
                    lastSingTick);
                var ticksSinceLast = CurrentTick - lastTick;
                PhraseTicksHit += ticksSinceLast * bestHitPercent;

                // Drive the visual "on note" state for real-mic singers. Without this,
                // VocalsPlayer's single-mic path never sees _lastHitTime set, so the
                // hitting particle trail never plays. Mirrors YargVocalsEngine.
                OnHit?.Invoke(true);

                // Trigger hit event
                if (HasHit)
                {
                    if (IsSoloActive)
                    {
                        Solos[CurrentSoloIndex].NotesHit++;
                    }

                    // Singing (or any noise) can result in a call to CheckPercussionHit() as well, so we need to check SingToActivateStarPower here.
                    if (CanStarPowerActivate && EngineParameters.SingToActivateStarPower)
                    {
                        ActivateStarPower();
                    }
                }
            }
            else
            {
                OnHit?.Invoke(false);

                // Singing (or any noise) can result in a call to CheckPercussionHit() as well, so we need to check SingToActivateStarPower here.
                if (HasHit && CanStarPowerActivate && EngineParameters.SingToActivateStarPower)
                {
                    ActivateStarPower();
                }
            }

            HasHit = false;
        }

        private void CheckPercussionHit()
        {
            if (!HasHit)
            {
                return;
            }

            HasHit = false;

            // Find the current phrase
            if (NoteIndex >= Notes.Count)
            {
                return;
            }

            var phrase = Notes[NoteIndex];

            // Handle percussion notes
            var percussion = GetNextPercussionNote(phrase, CurrentTick);
            if (percussion is not null && CurrentTime >= percussion.Time)
            {
                AddScore(percussion);
                OnNoteHit?.Invoke(NoteIndex, percussion);
            }
        }

        /// <summary>
        /// Get the current pitch for the single mic. Used by the coordinator to read
        /// the sub-engine's current pitch state for visual feedback.
        /// </summary>
        public float GetCurrentPitch() => PitchSang;

        /// <summary>
        /// Get the bitmask of parts that the single mic is hitting this tick.
        /// Used by the coordinator for visual feedback.
        /// </summary>
        public uint GetMicHittingParts() => _singleMicHittingParts;

        /// <summary>
        /// Get the bitmask of parts that the single mic is hitting this tick through a
        /// PITCHED note (a bit in <see cref="GetMicHittingParts"/> without the matching
        /// bit here means that part was matched via a talkie/non-pitched note). Used by
        /// the coordinator to classify a mic's matched set as all-talkie (broadcast
        /// credit) vs any-pitched (conserved bucket).
        /// </summary>
        public uint GetMicHittingPitchedParts() => _singleMicHittingPitchedParts;

        /// <summary>
        /// Submit a pitch reading for the single mic. Used by the coordinator under
        /// composition to push per-mic pitch into each sub-engine.
        /// </summary>
        public void SetMicPitch(float pitch)
        {
            PitchSang = pitch;
            HasSang = true;
            OnSing?.Invoke(true);
        }

    }
}
