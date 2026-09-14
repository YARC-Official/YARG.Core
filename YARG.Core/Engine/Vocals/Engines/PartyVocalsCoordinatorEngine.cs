using System;
using System.Collections.Generic;
using System.Linq;
using YARG.Core.Chart;
using YARG.Core.Input;

namespace YARG.Core.Engine.Vocals.Engines
{
    /// <summary>
    /// Per-HARM result for one phrase, delivered via
    /// <see cref="PartyVocalsCoordinatorEngine.OnPartyVocalsPhrase"/>. Only parts that actually have
    /// notes in the phrase ("available parts") are included, in ascending <see cref="PartIndex"/>
    /// order (lowest HARM number first). A part is "Awesome" when <see cref="Meter"/> &gt;= the
    /// engine's PhraseHitPercent threshold. Top-level (not nested) so Unity-side callers can reach
    /// it via `using YARG.Core.Engine.Vocals.Engines;`.
    /// </summary>
    public readonly struct PartyPartResult
    {
        // 0 = HARM1, 1 = HARM2, 2 = HARM3 (ordinal position in the song's part list).
        public int PartIndex { get; }
        public double Meter { get; } // raw canonical meter; >= PhraseHitPercent = Awesome

        public PartyPartResult(int partIndex, double meter)
        {
            PartIndex = partIndex;
            Meter = meter;
        }
    }

    public sealed class PartyVocalsCoordinatorEngine : VocalsEngine
    {
        public static InstrumentDifficulty<VocalNote> BuildMergedTrack(
            IReadOnlyList<VocalsPart> allParts, InstrumentDifficulty<VocalNote> baseTrack)
        {
            var merged = new List<VocalNote>();
            var seen = new HashSet<(uint tick, uint tickEnd)>();

            foreach (var part in allParts)
            {
                foreach (var phrase in part.NotePhrases)
                {
                    var note = phrase.PhraseParentNote;
                    if (seen.Add((note.Tick, note.TickEnd)))
                    {
                        merged.Add(note);
                    }
                }
            }

            merged.Sort((a, b) => a.Tick.CompareTo(b.Tick));

            // StarPower Phrase events live per-part, so the merged track must union
            // them too — otherwise TotalStarPowerPhrases counts only HARM1's phrases
            // while StarPowerPhrasesHit counts hits across all parts.
            var mergedPhrases = new List<Phrase>();
            var seenPhrases = new HashSet<(uint tick, uint tickEnd, PhraseType type)>();
            foreach (var part in allParts)
            {
                foreach (var phrase in part.OtherPhrases)
                {
                    if (seenPhrases.Add((phrase.Tick, phrase.TickEnd, phrase.Type)))
                    {
                        mergedPhrases.Add(phrase);
                    }
                }
            }
            mergedPhrases.Sort((a, b) => a.Tick.CompareTo(b.Tick));

            return new InstrumentDifficulty<VocalNote>(
                baseTrack.Instrument, Difficulty.Expert,
                merged, mergedPhrases, new(baseTrack.TextEvents));
        }

        // Multi-mic state owned directly by the coordinator (was hoisted into
        // YargFreeVocalsEngine in Phase 3 so the subclass could see them).
        private readonly int _micCount;
        private readonly VocalsPart[] _allParts;
        private readonly bool[] _partHasContent;
        private readonly double[] _canonicalMeters;
        private readonly uint[] _phraseTicksTotalPerPart;
        private readonly double[,] _micPartHits;
        private readonly double[,] _lastWindowSnapshot;
        private readonly double[] _cumulativeAssignedTicks;
        private readonly double[] _lastTickMicDeltas;

        // Per-mic bitmask of which parts each mic is currently hitting THIS TICK.
        // Populated by reading each sub-engine's GetMicHittingParts after driving its tick,
        // and read per-tick by the ambiguity classifier for scoring. This is a single-tick
        // transient (the sub-engine resets it every AccumulateMicPartHits call), so it is
        // NOT suitable for the visual layer, which reads one frame later — see below.
        private readonly uint[] _micCurrentlyHittingParts;

        // Per-mic bitmask of parts each mic hit at ANY point during the current visual frame.
        // OR-accumulated across ticks and reset per frame (ResetMicSangFlags), mirroring
        // _micSangThisTick. The visual-facing GetMicHittingParts returns this so the per-mic
        // trail's on-note gate sees a stable per-frame signal instead of the single-tick
        // transient above (which is 0 on most ticks and killed the trail).
        private readonly uint[] _micHittingPartsThisFrame;

        // Unified per-mic "sang this tick" flag - source of truth for needle visibility
        private readonly bool[] _micSangThisTick;

        // Coordinator-specific ambiguity scoring state
        private readonly double[] _harmDirectTicks;
        private readonly double[] _ambiguityBuckets;
        private readonly double[,] _bucketPerMic;
        private readonly uint[] _micHitMaskScratch;
        private readonly int[] _bucketOrder;
        private double _lastMeterRefreshTime;
        private const double METER_UPDATE_INTERVAL_SECONDS = 0.1;

        // Sub-engines (one per mic, composition)
        private readonly YargFreeVocalsEngine[] _subEngines;

        // Per-phrase tracking for the coordinator's own phrase-end logic
        private uint? _coordinatorPhraseTicksTotal;

        // Percussion notes this coordinator has already scored or missed. Tracked locally
        // instead of via VocalNote.WasHit/WasMissed: the note objects are shared with the
        // sub-engines and other players' engines, so setting hit/miss state on them leaks
        // across engines (the same hazard the SP-earning bug documents). This set is the
        // coordinator's private "consumed" ledger, fed to GetNextPercussionNote so a note
        // isn't re-offered after it's resolved. Cleared on Reset (rewind/practice).
        private readonly HashSet<VocalNote> _resolvedPercussion = new();

        public PartyVocalsCoordinatorEngine(
            InstrumentDifficulty<VocalNote> noteTrack,
            IReadOnlyList<VocalsPart> allParts,
            SyncTrack syncTrack,
            VocalsEngineParameters engineParameters,
            bool isBot,
            int micCount,
            int botPartIndex = 0)
            : base(noteTrack, syncTrack, engineParameters, isBot)
        {
            int partCount = allParts.Count;

            _micCount = micCount;
            _allParts = allParts.ToArray();
            _partHasContent = new bool[partCount];
            _canonicalMeters = new double[partCount];
            _phraseTicksTotalPerPart = new uint[partCount];
            _micPartHits = new double[micCount, partCount];
            _lastWindowSnapshot = new double[micCount, partCount];
            _cumulativeAssignedTicks = new double[partCount];
            _lastTickMicDeltas = new double[micCount];
            _micCurrentlyHittingParts = new uint[micCount];
            _micHittingPartsThisFrame = new uint[micCount];
            _micSangThisTick = new bool[micCount];

            _harmDirectTicks = new double[partCount];
            _ambiguityBuckets = new double[1 << partCount];
            _bucketPerMic = new double[micCount, 1 << partCount];
            _micHitMaskScratch = new uint[micCount];
            _bucketOrder = ComputeBucketOrder(partCount);

            // Build one sub-engine per mic (composition).
            //
            // NOTE (shared mutable note state): every sub-engine is constructed with the
            // SAME `noteTrack` the coordinator scores on, so coordinator.Notes and each
            // subEngine.Notes are the *same* VocalNote objects. CloneAsInstrumentDifficulty
            // also reuses note references across players, so a bot and a live player share
            // these objects too. Engine operations that mutate note flags (StripStarPower
            // on miss, SetHitState) therefore leak between engines.
            //
            // `isSubEngine: true` is a targeted fix for the worst symptom: it stops a
            // sub-engine's miss from stripping the StarPower flag off a phrase the
            // coordinator hit (which made real singers earn no SP while bots did). It does
            // NOT fix the residual cross-player leak (one player's coordinator missing an
            // SP phrase can still strip it for another player sharing the same notes).
            //
            // The more correct fix is to give each engine its own copy of the notes (or
            // make notes immutable), eliminating the whole class of cross-engine leakage.
            // That touches shared, hot data across all instruments, so it's a broader change
            // worth discussing with the wider project rather than doing here.
            _subEngines = new YargFreeVocalsEngine[micCount];
            for (int i = 0; i < micCount; i++)
            {
                _subEngines[i] = new YargFreeVocalsEngine(
                    noteTrack,
                    allParts,
                    syncTrack,
                    engineParameters,
                    isBot,
                    botPartIndex: i,
                    isSubEngine: true);
            }

            for (int j = 0; j < partCount; j++)
            {
                _partHasContent[j] = allParts[j].NotePhrases.Count > 0;
            }

            // Forward visual events from the primary sub-engine (mic 0) to the
            // coordinator so the existing VocalsPlayer — which subscribes to the
            // coordinator's events — receives needle/sing/hit feedback. The
            // sub-engines do the actual pitch matching and fire these events on
            // their own delegates; without forwarding, the coordinator never fires
            // them and the HUD shows no feedback.
            _subEngines[0].OnTargetNoteChanged += note => OnTargetNoteChanged?.Invoke(note);
            _subEngines[0].OnSing += singing => OnSing?.Invoke(singing);
            _subEngines[0].OnHit += hit => OnHit?.Invoke(hit);

            GetWaitCountdowns(PartyVocalsCountdownNotes.ExcludingPercussion(allParts.ToList()));
        }

        public override void Reset(bool keepCurrentButtons = false)
        {
            // Drop the local percussion ledger so notes are hittable again after a
            // rewind/practice seek (mirrors how WasHit/WasMissed clear on note reset).
            _resolvedPercussion.Clear();

            // Reset every sub-engine so practice/rewind starts fresh — without this,
            // sub-engines retain advanced NoteIndex, CurrentTime, LastSingTick, and
            // per-part accumulators from before the seek.
            // (Null check: BaseEngine's constructor calls Reset() before our fields are set.)
            if (_subEngines != null)
            {
                foreach (var sub in _subEngines)
                {
                    sub.Reset(keepCurrentButtons);
                }

                // Clear all coordinator-owned phrase/visual state.
                ResetPhraseState();
            }

            base.Reset(keepCurrentButtons);
        }

        public IReadOnlyList<YargFreeVocalsEngine> SubEngines => _subEngines;

        /// <summary>
        /// The harmony part index the primary mic is currently matching (0=HARM1, 1=HARM2,
        /// 2=HARM3). Drives particle/trail color in the HUD. For ambiguous matches (multiple
        /// parts satisfied by the same pitch), the lowest index wins — the sub-engine iterates
        /// parts in ascending order and keeps the first best-percent match.
        ///
        /// When multi-mic support is added, the coordinator will disambiguate across mics:
        /// e.g., if mic 0 and mic 1 both match HARM1+HARM2 at the same pitch, mic 0 claims
        /// HARM1 (lower index) and mic 1 claims HARM2. This property would then return mic 0's
        /// resolved part. The disambiguation strategy is a coordinator responsibility, not the
        /// sub-engine's.
        /// </summary>
        public int DisplayedHarmonyIndex => _micCount > 0 ? _subEngines[0].CurrentTargetHarmonyIndex : 0;

        public bool PartHasContent(int partIndex)
        {
            if (partIndex < 0 || partIndex >= _allParts.Length) return false;
            return _partHasContent[partIndex];
        }

        public int PartCount => _allParts.Length;

        public bool PartInCurrentPhrase(int partIndex) =>
            partIndex >= 0 && partIndex < _phraseTicksTotalPerPart.Length
            && _phraseTicksTotalPerPart[partIndex] > 0u;

        // True if the part has a (non-percussion) note in the NEXT master phrase. Used by
        // the HUD count-in drain so a meter can warn the player a phrase before their line
        // returns. Computed on demand (the next phrase hasn't started, so the per-tick
        // _phraseTicksTotalPerPart array doesn't cover it).
        public bool PartInNextPhrase(int partIndex)
        {
            if (partIndex < 0 || partIndex >= _allParts.Length) return false;
            int next = NoteIndex + 1;
            if (next < 0 || next >= Notes.Count) return false;
            return GetTicksInPhraseForPart(_allParts[partIndex], Notes[next]) > 0u;
        }

        // Progress 0..1 through the CURRENT phrase's tick span. Drives the count-in drain.
        // Uses the phrase span (Tick..TickEnd), NOT PhraseTicksTotal — that is the sung-hit
        // tick total and would drain far too fast on sparse phrases.
        public double CurrentPhraseProgress
        {
            get
            {
                if (NoteIndex < 0 || NoteIndex >= Notes.Count) return 0.0;
                var phrase = Notes[NoteIndex];
                uint span = phrase.TickEnd - phrase.Tick;
                if (span == 0) return 0.0;
                double p = ((double) CurrentTick - phrase.Tick) / span;
                return p < 0.0 ? 0.0 : (p > 1.0 ? 1.0 : p);
            }
        }

        // Duration of the current phrase in seconds. Lets the HUD convert a real-time
        // count-in hold (e.g. 500ms) into a fraction of CurrentPhraseProgress.
        public double CurrentPhraseDurationSeconds
        {
            get
            {
                if (NoteIndex < 0 || NoteIndex >= Notes.Count) return 0.0;
                var phrase = Notes[NoteIndex];
                return phrase.TimeEnd - phrase.Time;
            }
        }

        public IReadOnlyList<double> CanonicalMeters => _canonicalMeters;

        public double AwesomeThreshold => EngineParameters.PhraseHitPercent;

        public uint GetMicHittingParts(int micIndex)
        {
            if (micIndex < 0 || micIndex >= _micCount) return 0u;
            // Per-frame accumulation, not the single-tick transient: the visual layer reads
            // this a frame after the engine ticked, so it must reflect any hit during the
            // frame, not just the last tick's (which is usually 0).
            return _micHittingPartsThisFrame[micIndex];
        }

        public float GetMicPitch(int micIndex)
        {
            if (micIndex < 0 || micIndex >= _micCount) return 0f;
            return _subEngines[micIndex].GetCurrentPitch();
        }

        public void ResetMicSangFlags()
        {
            // Reset per-frame needle/trail signals together: both are accumulated across the
            // frame's ticks and consumed by the visual layer after Update.
            Array.Clear(_micSangThisTick, 0, _micSangThisTick.Length);
            Array.Clear(_micHittingPartsThisFrame, 0, _micHittingPartsThisFrame.Length);
        }
        public bool DidMicSingThisTick(int mic) =>
            mic >= 0 && mic < _micCount && _micSangThisTick[mic];

        #region VocalsEngine Abstract Implementations

        protected override void MutateStateWithInput(GameInput gameInput)
        {
            int mic = PartyVocalsInput.UnpackMic(gameInput.Action);
            var action = PartyVocalsInput.UnpackAction(gameInput.Action);

            switch (action)
            {
                case VocalsAction.Pitch:
                    // Guarded sub-engine call — mirrors the silent-default pattern of
                    // GetMicPitch/GetMicHittingParts (a malformed/replayed mic index is
                    // dropped, not thrown). Do NOT call the coordinator's own
                    // SetMicPitch(int,float) here: it throws on out-of-range.
                    if (mic >= 0 && mic < _micCount)
                    {
                        _subEngines[mic].SetMicPitch(gameInput.Axis);
                        _micSangThisTick[mic] = true; // unified needle sang-state (see below)
                    }
                    break;
                case VocalsAction.Hit when gameInput.Button:
                    HasHit = true; // coordinator-level; percussion scored here (standalone fix)
                    break;
                case VocalsAction.StarPower:
                    IsStarPowerInputActive = gameInput.Button;
                    break;
            }
        }

        protected override void UpdateHitLogic(double time)
        {
            if (NoteIndex >= Notes.Count)
            {
                HasSang = false;
                return;
            }

            // Bot self-activation. Mirrors YargVocalsEngine.UpdateBot: a bot toggles its
            // own StarPower input so UpdateStarPower deploys once it has half a bar. The
            // coordinator's UpdateBot is a no-op (sub-engines run their own), and the
            // sub-engines only toggle their own dead-data IsStarPowerInputActive — so
            // without this the authoritative coordinator never deployed SP for a bot.
            if (IsBot)
            {
                IsStarPowerInputActive = CanStarPowerActivate && !IsStarPowerInputActive;
            }

            // Drive each sub-engine forward for this tick. Each sub-engine runs its
            // full single-mic lifecycle (MutateStateWithInput → UpdateHitLogic →
            // phrase-end). Sub-engine outputs nobody reads are dead data.
            for (int i = 0; i < _micCount; i++)
            {
                _subEngines[i].Update(time);
            }

            // Mirror time variables from sub-engines (they all share the same SyncTrack,
            // so any one would do — use the first).
            CurrentTime = _subEngines[0].CurrentTime;
            CurrentTick = _subEngines[0].CurrentTick;
            // Mirror mic 0's pitch so VocalsPlayer can read it for the needle position.
            // SetMicPitch sets the sub-engine's PitchSang, but the player reads the
            // coordinator's PitchSang for the needle — without this it stays at 0.
            PitchSang = _subEngines[0].PitchSang;

            // Bots queue no Hit inputs and the coordinator's UpdateBot is a no-op, so a bot
            // would never set HasHit and never score percussion (real mics do, via
            // MutateStateWithInput). Arm HasHit here when a percussion note is due, mirroring
            // the solo bot path (YargVocalsEngine.UpdateBot's percussion branch).
            if (IsBot)
            {
                var botPercussion = GetNextPercussionNote(Notes[NoteIndex], CurrentTick, _resolvedPercussion.Contains);
                if (botPercussion is not null && CurrentTime >= botPercussion.Time)
                {
                    HasHit = true;
                }
            }

            // Score percussion taps at the band level. The coordinator aggregates
            // any mic's Hit into a single HasHit (MutateStateWithInput), so this
            // scores one band hit per tap with no double-count.
            // Also handles sing-to-activate (mirrors YargVocalsEngine.cs:188-218).
            CheckPercussionHit();

            // Read per-tick credit from each sub-engine's LastTickPartDeltas and
            // per-mic hitting-parts bitmask.
            Array.Clear(_lastTickMicDeltas, 0, _lastTickMicDeltas.Length);
            for (int i = 0; i < _micCount; i++)
            {
                var deltas = _subEngines[i].LastTickPartDeltas;
                double totalDelta = 0;
                for (int j = 0; j < deltas.Count && j < _allParts.Length; j++)
                {
                    totalDelta += deltas[j];
                }
                _lastTickMicDeltas[i] = totalDelta;

                // Read the sub-engine's per-mic hitting-parts bitmask (single-tick).
                _micCurrentlyHittingParts[i] = _subEngines[i].GetMicHittingParts();

                // OR-accumulate into the per-frame signal the visual layer reads, so a hit
                // on any tick this frame keeps the trail's on-note gate satisfied.
                _micHittingPartsThisFrame[i] |= _micCurrentlyHittingParts[i];
            }

            var phrase = Notes[NoteIndex];

            // Populate per-part tick totals for the current phrase. The coordinator's
            // phrase total sums across ALL harmony parts — not just Parts[0]/HARM1 — so
            // phrases that only have HARM2/HARM3 notes are not treated as empty.
            uint allPartsTotal = 0;
            for (int j = 0; j < _allParts.Length; j++)
            {
                _phraseTicksTotalPerPart[j] = GetTicksInPhraseForPart(_allParts[j]);
                allPartsTotal += _phraseTicksTotalPerPart[j];
            }

            _coordinatorPhraseTicksTotal ??= allPartsTotal;
            PhraseTicksTotal ??= _coordinatorPhraseTicksTotal;

            // Run the coordinator's per-tick ambiguity classifier
            AccumulateAmbiguityScoring();

            // Speculative refresh on the 100ms throttle for live HUD
            if (CurrentTime - _lastMeterRefreshTime >= METER_UPDATE_INTERVAL_SECONDS)
            {
                _lastMeterRefreshTime = CurrentTime;
                RunAllocatorIntoCanonicalMeters(commit: false);
            }

            // Check for end of phrase
            if (CurrentTick > phrase.TickEnd)
            {
                bool hasNotes = _coordinatorPhraseTicksTotal.Value != 0;
                bool isLastPhrase = NoteIndex == Notes.Count - 1;
                uint phraseTicksTotal = _coordinatorPhraseTicksTotal.Value;

                if (phraseTicksTotal == 0)
                {
                    HitNote(phrase);
                    // Fire OnPhraseHit so _phrasePercents stays aligned with
                    // _phraseGrades/_phrasePartResults (populated by the
                    // OnPartyVocalsPhrase handler below).
                    OnPhraseHit?.Invoke(1.0, true, isLastPhrase);
                    OnPartyVocalsPhrase?.Invoke(
                        PhraseGrade.Awesome, Array.Empty<PartyPartResult>(), isLastPhrase);
                }
                else
                {
                    ProcessPhraseEnd(phrase, phraseTicksTotal, isLastPhrase);
                }

                // Reset per-phrase state
                ResetPhraseState();

                UpdateCarriedNote(phrase);
            }
        }

        protected override void CheckForNoteHit()
        {
            // No-op: sub-engines handle their own hit detection.
        }

        protected override void UpdateBot(double songTime)
        {
            // No-op: each sub-engine runs its own UpdateBot via its own Update cycle.
        }

        protected override bool CanVocalNoteBeHit(VocalNote note, out float hitPercent)
        {
            hitPercent = 0f;
            throw new NotImplementedException(
                "CanVocalNoteBeHit is not called on the coordinator; sub-engines handle pitch matching.");
        }

        protected override bool CanNoteBeHit(VocalNote note) =>
            throw new NotImplementedException();

        // Vocals have no lanes (matches YargVocalsEngine).
        protected override bool ProximalLaneForgivesInput(int inputNote, VocalNote laneNote) =>
            throw new NotImplementedException();

        #endregion

        public delegate void PartyVocalsPhraseEvent(
            PhraseGrade grade,
            IReadOnlyList<PartyPartResult> parts,
            bool isLastPhrase);

        /// <summary>
        /// Fires at the end of each phrase alongside OnPhraseHit. Provides the
        /// final N-awesome grade and per-HARM canonical meter values. The coordinator
        /// always emits both OnPhraseHit (standard vocals event) and OnPartyVocalsPhrase
        /// (party-specific grade/parts) at phrase end. Declared here rather than on
        /// VocalsEngine so the prototype's phrase-end event stays off the upstream base engine.
        /// </summary>
        public PartyVocalsPhraseEvent? OnPartyVocalsPhrase;

        #region Phrase-End Logic

        private void ProcessPhraseEnd(VocalNote phrase, uint phraseTicksTotal, bool isLastPhrase)
        {
            // Final allocation using all accumulated credit
            RunAllocatorIntoCanonicalMeters(commit: true);

            int partCount = _allParts.Length;
            int awesomeCount = 0;
            double bestMeter = 0;
            for (int j = 0; j < partCount; j++)
            {
                if (_canonicalMeters[j] >= EngineParameters.PhraseHitPercent) awesomeCount++;
                if (_canonicalMeters[j] > bestMeter) bestMeter = _canonicalMeters[j];
            }

            var grade = awesomeCount switch
            {
                0 => PhraseGrade.Miss,
                1 => PhraseGrade.Awesome,
                2 => PhraseGrade.DoubleAwesome,
                _ => PhraseGrade.TripleAwesome,
            };
            bool hit = grade != PhraseGrade.Miss;

            // Per-part results for parts that actually have notes in this phrase (available parts),
            // ascending part order (lowest HARM number first = bottom of the summary bar).
            var parts = new List<PartyPartResult>(partCount);
            for (int j = 0; j < partCount; j++)
            {
                if (_phraseTicksTotalPerPart[j] > 0)
                {
                    parts.Add(new PartyPartResult(j, _canonicalMeters[j]));
                }
            }

            if (hit)
            {
                EngineStats.TicksHit += phraseTicksTotal;
                HitNote(phrase);
            }
            else
            {
                var ticksHit = (uint)Math.Round(PhraseTicksHit);
                EngineStats.TicksHit += ticksHit;
                EngineStats.TicksMissed += phraseTicksTotal - ticksHit;
                MissNote(phrase, bestMeter);
            }

            OnPhraseHit?.Invoke(bestMeter / EngineParameters.PhraseHitPercent, hit, isLastPhrase);
            OnPartyVocalsPhrase?.Invoke(grade, parts, isLastPhrase);
        }

        private void ResetPhraseState()
        {
            PhraseTicksHit = 0;
            PhraseTicksTotal = null;
            _coordinatorPhraseTicksTotal = null;

            Array.Clear(_micPartHits, 0, _micPartHits.Length);
            Array.Clear(_lastWindowSnapshot, 0, _lastWindowSnapshot.Length);
            Array.Clear(_cumulativeAssignedTicks, 0, _cumulativeAssignedTicks.Length);
            Array.Clear(_canonicalMeters, 0, _canonicalMeters.Length);
            Array.Clear(_phraseTicksTotalPerPart, 0, _phraseTicksTotalPerPart.Length);
            Array.Clear(_harmDirectTicks, 0, _harmDirectTicks.Length);
            Array.Clear(_ambiguityBuckets, 0, _ambiguityBuckets.Length);
            Array.Clear(_bucketPerMic, 0, _bucketPerMic.Length);
            Array.Clear(_micCurrentlyHittingParts, 0, _micCurrentlyHittingParts.Length);
            Array.Clear(_micHittingPartsThisFrame, 0, _micHittingPartsThisFrame.Length);
            Array.Clear(_micSangThisTick, 0, _micSangThisTick.Length);

            _lastMeterRefreshTime = CurrentTime;
        }

        #endregion

        #region Ambiguity Scoring

        private void AccumulateAmbiguityScoring()
        {
            int partCount = _phraseTicksTotalPerPart.Length;

            // Classify: for each mic, build the mask of HARMs it could be hitting
            for (int i = 0; i < _micCount; i++)
            {
                uint rawMask = _micCurrentlyHittingParts[i];
                uint mask = 0u;
                for (int j = 0; j < partCount; j++)
                {
                    if ((rawMask & (1u << j)) != 0u && _phraseTicksTotalPerPart[j] > 0u)
                        mask |= 1u << j;
                }
                _micHitMaskScratch[i] = mask;
            }

            // Direct credit: binary across mics
            for (int j = 0; j < partCount; j++)
            {
                double maxDelta = 0;
                for (int i = 0; i < _micCount; i++)
                {
                    uint m = _micHitMaskScratch[i];
                    if (PopCount(m) == 1 && (m & (1u << j)) != 0u)
                    {
                        double d = _lastTickMicDeltas[i];
                        if (d > maxDelta) maxDelta = d;
                    }
                }
                _harmDirectTicks[j] += maxDelta;
            }

            // Ambiguity bucket credit: additive across mics with per-mic-span cap.
            // Use per-part delta (not summed) so the per-mic-span cap correctly
            // limits each HARM to what one mic actually contributed per HARM.
            for (int i = 0; i < _micCount; i++)
            {
                uint m = _micHitMaskScratch[i];
                if (PopCount(m) >= 2)
                {
                    var deltas = _subEngines[i].LastTickPartDeltas;
                    double partDelta = 0;
                    int hitCount = 0;
                    for (int j = 0; j < partCount && j < deltas.Count; j++)
                    {
                        if ((m & (1u << j)) != 0u)
                        {
                            partDelta += deltas[j];
                            hitCount++;
                        }
                    }
                    if (hitCount > 0) partDelta /= hitCount;

                    _ambiguityBuckets[(int)m] += partDelta;
                    _bucketPerMic[i, (int)m] += partDelta;
                }
            }
        }

        #endregion

        #region Allocator

        private void RunAllocatorIntoCanonicalMeters(bool commit)
        {
            int partCount = _phraseTicksTotalPerPart.Length;

            Span<double> credited = stackalloc double[partCount];
            for (int j = 0; j < partCount; j++)
            {
                uint cap = _phraseTicksTotalPerPart[j];
                credited[j] = cap == 0 ? 0 : Math.Min(_harmDirectTicks[j], cap);
            }

            Span<double> bucketsCopy = stackalloc double[_ambiguityBuckets.Length];
            for (int i = 0; i < _ambiguityBuckets.Length; i++) bucketsCopy[i] = _ambiguityBuckets[i];

            Span<double> receivedFromBucket = stackalloc double[partCount];

            foreach (int S in _bucketOrder)
            {
                if (bucketsCopy[S] <= 0) continue;

                double perMicCap = 0;
                for (int i = 0; i < _micCount; i++)
                {
                    double v = _bucketPerMic[i, S];
                    if (v > perMicCap) perMicCap = v;
                }

                receivedFromBucket.Clear();

                while (bucketsCopy[S] > 0)
                {
                    int chosen = -1;
                    double chosenCredited = -1;
                    for (int j = 0; j < partCount; j++)
                    {
                        if ((S & (1 << j)) == 0) continue;
                        uint cap = _phraseTicksTotalPerPart[j];
                        if (cap == 0 || credited[j] >= cap) continue;
                        if (receivedFromBucket[j] >= perMicCap) continue;
                        if (credited[j] > chosenCredited)
                        {
                            chosenCredited = credited[j];
                            chosen = j;
                        }
                    }

                    if (chosen < 0) break;

                    double remainingCapacity = _phraseTicksTotalPerPart[chosen] - credited[chosen];
                    double remainingPerMicCap = perMicCap - receivedFromBucket[chosen];
                    double transfer = Math.Min(
                        Math.Min(bucketsCopy[S], remainingCapacity),
                        remainingPerMicCap);
                    if (transfer <= 0) break;
                    credited[chosen] += transfer;
                    bucketsCopy[S] -= transfer;
                    receivedFromBucket[chosen] += transfer;
                }
            }

            for (int j = 0; j < partCount; j++)
            {
                uint cap = _phraseTicksTotalPerPart[j];
                _canonicalMeters[j] = cap == 0 ? 0 : credited[j] / cap;
            }

            // Mirror best meter into PhraseTicksHit for HUD combo fill bar.
            // The fill bar represents accumulated progress within a phrase and must be
            // monotonically non-decreasing — the allocator's speculative refresh can
            // momentarily compute a lower "best" if timing causes a part's direct
            // ticks to be reclassified, so we clamp to the previous value.
            if (PhraseTicksTotal is { } total && total > 0)
            {
                double best = 0;
                for (int j = 0; j < partCount; j++)
                    if (_canonicalMeters[j] > best) best = _canonicalMeters[j];
                double newHit = best * total;
                if (newHit > PhraseTicksHit)
                {
                    PhraseTicksHit = newHit;
                }
            }
        }

        private static int[] ComputeBucketOrder(int partCount)
        {
            var masks = new List<int>();
            for (int m = 0; m < (1 << partCount); m++)
            {
                if (PopCount((uint)m) >= 2) masks.Add(m);
            }
            masks.Sort((a, b) =>
            {
                int pa = PopCount((uint)a);
                int pb = PopCount((uint)b);
                if (pa != pb) return pa - pb;
                return a - b;
            });
            return masks.ToArray();
        }

        private static int PopCount(uint m)
        {
            return (int)(((m >> 0) & 1u) + ((m >> 1) & 1u) + ((m >> 2) & 1u));
        }

        #endregion

        #region Helpers

        private uint GetTicksInPhraseForPart(VocalsPart part) =>
            GetTicksInPhraseForPart(part, Notes[NoteIndex]);

        private uint GetTicksInPhraseForPart(VocalsPart part, VocalNote masterPhrase)
        {
            uint masterStart = masterPhrase.Tick;
            uint masterEnd = masterPhrase.TickEnd;

            uint totalTime = 0;
            foreach (var partPhrase in part.NotePhrases)
            {
                var phraseNote = partPhrase.PhraseParentNote;
                if (phraseNote.Tick >= masterEnd || phraseNote.TickEnd <= masterStart) continue;

                foreach (var noteInPhrase in phraseNote.ChildNotes)
                {
                    if (noteInPhrase.IsPercussion) continue;
                    totalTime += phraseNote.GetTicksForNote(noteInPhrase);
                }
                break;
            }
            return totalTime;
        }

        #endregion

        #region Percussion Scoring

        private void CheckPercussionHit()
        {
            if (NoteIndex >= Notes.Count)
            {
                HasHit = false;
                return;
            }

            var phrase = Notes[NoteIndex];
            var percussion = GetNextPercussionNote(phrase, CurrentTick, _resolvedPercussion.Contains);
            if (percussion is not null)
            {
                // Gate on the full hit window (front/back tolerance), mirroring solo
                // YargVocalsEngine.CheckPercussionHit. The previous raw [Time, TimeEnd]
                // span gave ZERO early tolerance (CurrentTime >= Time) and, for a short
                // percussion note, almost no late tolerance — so real taps were dropped
                // even though they reached the engine.
                if (IsNoteInWindow(percussion, out var missed))
                {
                    if (HasHit)
                    {
                        // Consume locally (no note.SetHitState) so the shared note isn't
                        // mutated, then score exactly as before.
                        _resolvedPercussion.Add(percussion);
                        AddScore(percussion);
                        OnNoteHit?.Invoke(NoteIndex, percussion);
                    }
                }
                else if (missed)
                {
                    // Back-end miss: the window has passed without a tap. Resolve the note
                    // locally so it stops being "next due" (instead of lingering) and fire
                    // the miss event, mirroring solo's MissNote(percussion) minus the
                    // shared-note SetMissState.
                    _resolvedPercussion.Add(percussion);
                    OnNoteMissed?.Invoke(NoteIndex, percussion);
                }
            }
            else
            {
                // Mirror YargVocalsEngine.cs:210-214: singing (or any noise) can
                // result in a percussion hit call, so check sing-to-activate here.
                if (HasHit && CanStarPowerActivate && EngineParameters.SingToActivateStarPower)
                {
                    ActivateStarPower();
                }
            }

            HasHit = false;
        }

        #endregion
    }
}
