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

        // Store reference to all parts for hit testing
        protected readonly IReadOnlyList<VocalsPart> _allParts;
        private readonly int _botPartIndex;

        // Resolved bot part for the current tick after applying the per-phrase fallback:
        // if the assigned _botPartIndex has no active phrase, fall back to the lowest-numbered
        // part that does. Updated in UpdateBot, consumed by CheckSingingHit so the bot scores
        // against whatever line it's actually singing.
        private int _currentBotEffectivePartIndex;

        // Per-part delta for the current tick. Updated in UpdateHitLogic after
        // per-part credit is committed, for external consumption (e.g., coordinator).
        private readonly double[] _lastTickPartDeltas;

        // Per-part hit accumulator for single-mic free vocals
        private readonly double[] _singleMicPartHits;
        // Bitmask of parts that the single mic is hitting this tick
        private uint _singleMicHittingParts;

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
            _currentBotEffectivePartIndex = _botPartIndex;

            // Initialize fields for single-mic per-part accumulation
            _lastTickPartDeltas = new double[allParts.Count];
            _singleMicPartHits = new double[allParts.Count];
            _singleMicHittingParts = 0u;

            // Build countdowns from all parts for free vocals; exclude percussion so
            // percussion-only stretches show the countdown wheel instead of being
            // hidden as a continuous note stream.
            GetWaitCountdowns(PartyVocalsCountdownNotes.ExcludingPercussion(allParts.ToList()));
        }

        private VocalNote? FindActivePhraseInPart(int partIndex)
        {
            foreach (var partPhrase in _allParts[partIndex].NotePhrases)
            {
                var pn = partPhrase.PhraseParentNote;
                if (CurrentTick >= pn.Tick && CurrentTick <= pn.TotalTickEnd)
                {
                    return pn;
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
            VocalNote? botPhrase = FindActivePhraseInPart(_botPartIndex);
            int effectiveIndex = _botPartIndex;
            if (botPhrase is null)
            {
                for (int i = 0; i < _allParts.Count; i++)
                {
                    if (i == _botPartIndex) continue;
                    var fallback = FindActivePhraseInPart(i);
                    if (fallback is not null)
                    {
                        botPhrase = fallback;
                        effectiveIndex = i;
                        break;
                    }
                }
            }
            _currentBotEffectivePartIndex = effectiveIndex;

            // Search botPhrase directly instead of using GetNoteInPhraseAtSongTick, which
            // short-circuits to the base engine's CarriedVocalNote — that's populated from
            // the primary chart (HARM1 for all Free bots), so it would return a HARM1 note
            // even when botPhrase is HARM2/HARM3. Result: the needle would always sit on
            // HARM1 regardless of the bot's assigned harmony index.
            VocalNote? singNote = null;
            if (botPhrase is not null)
            {
                foreach (var childNote in botPhrase.ChildNotes)
                {
                    if (!childNote.IsPercussion
                        && CurrentTick >= childNote.Tick
                        && CurrentTick <= childNote.TotalTickEnd)
                    {
                        singNote = childNote;
                        break;
                    }
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

            // Reset the "currently hitting parts" bitmask for single-mic
            _singleMicHittingParts = 0u;

            if (!wasSinging)
                return false;

            var lastTick = Math.Max(
                SyncTrack.TimeToTick(CurrentTime - maxLeniency),
                savedLastSingTick);
            var ticksSinceLast = CurrentTick - lastTick;

            if (ticksSinceLast == 0)
                return false;

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
                            _singleMicPartHits[partIndex] += ticksSinceLast * hitPercent;
                            if (hitPercent > 0f)
                            {
                                anyMicHit = true;
                                representativeHitNote ??= note;
                                _singleMicHittingParts |= 1u << partIndex;
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
