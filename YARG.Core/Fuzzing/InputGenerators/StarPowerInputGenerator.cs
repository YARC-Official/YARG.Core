using System;
using System.Collections.Generic;
using System.Linq;
using YARG.Core.Chart;
using YARG.Core.Fuzzing.Interfaces;
using YARG.Core.Input;
using YARG.Core.Logging;

namespace YARG.Core.Fuzzing.InputGenerators
{
    /// <summary>
    /// Generates input sequences focused on star power phrase testing.
    /// </summary>
    public class StarPowerInputGenerator
    {
        private readonly Random _random;
        private readonly int? _seed;

        /// <summary>
        /// Initializes a new instance of StarPowerInputGenerator.
        /// </summary>
        /// <param name="seed">Random seed for reproducible generation</param>
        public StarPowerInputGenerator(int? seed = null)
        {
            _seed = seed;
            _random = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        /// <summary>
        /// Generates input sequences that focus on star power phrase mechanics.
        /// </summary>
        /// <param name="chart">Song chart to analyze for star power phrases</param>
        /// <param name="instrument">Target instrument</param>
        /// <param name="difficulty">Target difficulty</param>
        /// <returns>Array of game inputs focused on star power testing</returns>
        public GameInput[] GenerateStarPowerFocusedInputs(SongChart chart, Instrument instrument, Difficulty difficulty)
        {
            var startTime = chart.GetStartTime();
            var endTime = Math.Min(chart.GetEndTime(), startTime + 60.0); // Test first 60 seconds (legacy behavior)
            return GenerateStarPowerFocusedInputs(chart, instrument, difficulty, startTime, endTime);
        }

        /// <summary>
        /// Generates input sequences that focus on star power phrase mechanics within specified time bounds.
        /// </summary>
        /// <param name="chart">Song chart to analyze for star power phrases</param>
        /// <param name="instrument">Target instrument</param>
        /// <param name="difficulty">Target difficulty</param>
        /// <param name="startTime">Start time for input generation</param>
        /// <param name="endTime">End time for input generation</param>
        /// <returns>Array of game inputs focused on star power testing</returns>
        public GameInput[] GenerateStarPowerFocusedInputs(SongChart chart, Instrument instrument, Difficulty difficulty, double startTime, double endTime)
        {
            if (chart == null) throw new ArgumentNullException(nameof(chart));

            var inputs = new List<GameInput>();

            try
            {
                // Get the appropriate track for the instrument
                var difficultyTrack = GetInstrumentDifficultyTrack(chart, instrument, difficulty);
                if (difficultyTrack == null)
                {
                    YargLogger.LogWarning($"No track found for {instrument} {difficulty}, generating basic inputs");
                    return GenerateBasicStarPowerInputs(startTime, endTime, instrument);
                }

                YargLogger.LogInfo($"Generating chart-based inputs for {instrument} {difficulty} from {startTime:F2}s to {endTime:F2}s");

                // Generate inputs based on actual chart notes
                inputs.AddRange(GenerateChartBasedInputs(difficultyTrack, instrument, startTime, endTime));

                // Add star power phrase specific inputs
                inputs.AddRange(GenerateStarPowerPhraseInputs(difficultyTrack, instrument, startTime, endTime));

                // Sort inputs by time
                inputs.Sort((a, b) => a.Time.CompareTo(b.Time));

                YargLogger.LogInfo($"Generated {inputs.Count} chart-based inputs for {instrument} {difficulty}");
            }
            catch (Exception ex)
            {
                YargLogger.LogWarning($"Failed to generate chart-based inputs: {ex.Message}, falling back to basic inputs");
                return GenerateBasicStarPowerInputs(startTime, endTime, instrument);
            }

            return inputs.ToArray();
        }

        /// <summary>
        /// Generates basic star power test inputs when no specific chart data is available.
        /// </summary>
        private GameInput[] GenerateBasicStarPowerInputs(double startTime, double endTime, Instrument instrument)
        {
            var inputs = new List<GameInput>();

            // Generate inputs that would typically build star power
            for (double time = startTime; time < endTime; time += 0.5)
            {
                // Add note hits
                inputs.Add(CreateNoteInput(time, instrument));
                
                // Occasionally add star power activation
                if (_random.NextDouble() < 0.1) // 10% chance
                {
                    inputs.Add(CreateStarPowerActivationInput(time, instrument));
                }
            }

            return inputs.ToArray();
        }

        /// <summary>
        /// Creates a note input for the specified instrument.
        /// </summary>
        private GameInput CreateNoteInput(double time, Instrument instrument)
        {
            return instrument switch
            {
                Instrument.FiveFretGuitar or Instrument.FiveFretBass =>
                    GameInput.Create(time, GuitarAction.GreenFret, true),
                Instrument.FourLaneDrums =>
                    GameInput.Create(time, DrumsAction.RedDrum, 1.0f),
                Instrument.ProKeys =>
                    GameInput.Create(time, ProKeysAction.Key1, true),
                Instrument.Vocals =>
                    GameInput.Create(time, VocalsAction.Pitch, 440.0f),
                _ => GameInput.Create(time, GuitarAction.GreenFret, true)
            };
        }

        /// <summary>
        /// Creates a star power activation input for the specified instrument.
        /// </summary>
        private GameInput CreateStarPowerActivationInput(double time, Instrument instrument)
        {
            return instrument switch
            {
                Instrument.FiveFretGuitar or Instrument.FiveFretBass =>
                    GameInput.Create(time, GuitarAction.StarPower, true),
                Instrument.ProKeys =>
                    GameInput.Create(time, ProKeysAction.StarPower, true),
                Instrument.Vocals =>
                    GameInput.Create(time, VocalsAction.StarPower, true),
                _ => GameInput.Create(time, GuitarAction.StarPower, true)
            };
        }

        /// <summary>
        /// Gets the appropriate instrument difficulty track from the chart.
        /// </summary>
        private object? GetInstrumentDifficultyTrack(SongChart chart, Instrument instrument, Difficulty difficulty)
        {
            try
            {
                return instrument switch
                {
                    Instrument.FiveFretGuitar => chart.FiveFretGuitar?.TryGetDifficulty(difficulty, out var guitarTrack) == true ? guitarTrack : null,
                    Instrument.FiveFretBass => chart.FiveFretBass?.TryGetDifficulty(difficulty, out var bassTrack) == true ? bassTrack : null,
                    Instrument.FourLaneDrums => chart.FourLaneDrums?.TryGetDifficulty(difficulty, out var drumsTrack) == true ? drumsTrack : null,
                    Instrument.FiveLaneDrums => chart.FiveLaneDrums?.TryGetDifficulty(difficulty, out var drums5Track) == true ? drums5Track : null,
                    Instrument.ProKeys => chart.ProKeys?.TryGetDifficulty(difficulty, out var proKeysTrack) == true ? proKeysTrack : null,
                    Instrument.Vocals => chart.Vocals?.Parts?.FirstOrDefault(), // Vocals don't have difficulties in the same way
                    _ => null
                };
            }
            catch (Exception ex)
            {
                YargLogger.LogWarning($"Error getting difficulty track for {instrument} {difficulty}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Generates inputs based on actual chart notes with timing jitter.
        /// </summary>
        private GameInput[] GenerateChartBasedInputs(object difficultyTrack, Instrument instrument, double startTime, double endTime)
        {
            var inputs = new List<GameInput>();

            try
            {
                switch (difficultyTrack)
                {
                    case InstrumentDifficulty<GuitarNote> guitarDiff:
                        inputs.AddRange(GenerateGuitarInputs(guitarDiff, instrument, startTime, endTime));
                        break;
                    case InstrumentDifficulty<DrumNote> drumsDiff:
                        inputs.AddRange(GenerateDrumsInputs(drumsDiff, instrument, startTime, endTime));
                        break;
                    case InstrumentDifficulty<ProKeysNote> proKeysDiff:
                        inputs.AddRange(GenerateProKeysInputs(proKeysDiff, instrument, startTime, endTime));
                        break;
                    case VocalsPart vocalsPart:
                        inputs.AddRange(GenerateVocalsInputs(vocalsPart, instrument, startTime, endTime));
                        break;
                    default:
                        YargLogger.LogWarning($"Unknown difficulty track type: {difficultyTrack?.GetType()}");
                        break;
                }
            }
            catch (Exception ex)
            {
                YargLogger.LogWarning($"Error generating chart-based inputs: {ex.Message}");
            }

            return inputs.ToArray();
        }

        /// <summary>
        /// Generates guitar inputs based on chart notes.
        /// </summary>
        private GameInput[] GenerateGuitarInputs(InstrumentDifficulty<GuitarNote> difficulty, Instrument instrument, double startTime, double endTime)
        {
            var inputs = new List<GameInput>();
            const double maxJitter = 0.02; // 20ms timing jitter
            const double hitRate = 0.85; // 85% hit rate for realistic gameplay

            foreach (var note in difficulty.Notes.Where(n => n.Time >= startTime && n.Time <= endTime))
            {
                // Skip some notes to simulate realistic gameplay (not 100% accuracy)
                if (_random.NextDouble() > hitRate) continue;

                // Add timing jitter
                var jitteredTime = note.Time + (_random.NextDouble() - 0.5) * maxJitter;
                jitteredTime = Math.Max(startTime, Math.Min(endTime, jitteredTime));

                // Generate fret press for the note
                var fretAction = GetGuitarFretAction(note.Fret);
                inputs.Add(GameInput.Create(jitteredTime, fretAction, true));

                // Add strum input slightly after fret press
                var strumTime = jitteredTime + 0.001 + (_random.NextDouble() - 0.5) * 0.005; // 1-6ms after fret
                inputs.Add(GameInput.Create(strumTime, GuitarAction.StrumDown, true));

                // Handle chord notes (child notes)
                foreach (var childNote in note.ChildNotes)
                {
                    var childFretAction = GetGuitarFretAction(childNote.Fret);
                    inputs.Add(GameInput.Create(jitteredTime, childFretAction, true));
                }

                // Generate whammy inputs for sustain notes
                if (note.IsSustain && note.TimeLength > 0.1) // Only for sustains longer than 100ms
                {
                    inputs.AddRange(GenerateWhammyInputsForSustain(note.Time, note.Time + note.TimeLength, startTime, endTime));
                }

                // Release fret after note
                var releaseTime = jitteredTime + Math.Max(0.05, note.TimeLength * 0.1); // Hold for at least 50ms or 10% of sustain
                inputs.Add(GameInput.Create(releaseTime, fretAction, false));
                
                foreach (var childNote in note.ChildNotes)
                {
                    var childFretAction = GetGuitarFretAction(childNote.Fret);
                    inputs.Add(GameInput.Create(releaseTime, childFretAction, false));
                }
            }

            return inputs.ToArray();
        }

        /// <summary>
        /// Generates drums inputs based on chart notes.
        /// </summary>
        private GameInput[] GenerateDrumsInputs(InstrumentDifficulty<DrumNote> difficulty, Instrument instrument, double startTime, double endTime)
        {
            var inputs = new List<GameInput>();
            const double maxJitter = 0.015; // 15ms timing jitter for drums
            const double hitRate = 0.90; // 90% hit rate for drums

            foreach (var note in difficulty.Notes.Where(n => n.Time >= startTime && n.Time <= endTime))
            {
                if (_random.NextDouble() > hitRate) continue;

                var jitteredTime = note.Time + (_random.NextDouble() - 0.5) * maxJitter;
                jitteredTime = Math.Max(startTime, Math.Min(endTime, jitteredTime));

                // Generate drum hit based on pad
                var drumAction = GetDrumAction(note.Pad);
                var velocity = 0.7f + (float)_random.NextDouble() * 0.3f; // Random velocity 0.7-1.0
                inputs.Add(GameInput.Create(jitteredTime, drumAction, velocity));

                // Handle chord notes (simultaneous hits)
                foreach (var childNote in note.ChildNotes)
                {
                    var childDrumAction = GetDrumAction(childNote.Pad);
                    inputs.Add(GameInput.Create(jitteredTime, childDrumAction, velocity));
                }
            }

            return inputs.ToArray();
        }

        /// <summary>
        /// Generates pro keys inputs based on chart notes.
        /// </summary>
        private GameInput[] GenerateProKeysInputs(InstrumentDifficulty<ProKeysNote> difficulty, Instrument instrument, double startTime, double endTime)
        {
            var inputs = new List<GameInput>();
            const double maxJitter = 0.02; // 20ms timing jitter
            const double hitRate = 0.80; // 80% hit rate for pro keys

            foreach (var note in difficulty.Notes.Where(n => n.Time >= startTime && n.Time <= endTime))
            {
                if (_random.NextDouble() > hitRate) continue;

                var jitteredTime = note.Time + (_random.NextDouble() - 0.5) * maxJitter;
                jitteredTime = Math.Max(startTime, Math.Min(endTime, jitteredTime));

                // Generate key press
                var keyAction = GetProKeysAction(note.Key);
                inputs.Add(GameInput.Create(jitteredTime, keyAction, true));

                // Handle chord notes
                foreach (var childNote in note.ChildNotes)
                {
                    var childKeyAction = GetProKeysAction(childNote.Key);
                    inputs.Add(GameInput.Create(jitteredTime, childKeyAction, true));
                }

                // Release key after sustain
                var releaseTime = jitteredTime + Math.Max(0.05, note.TimeLength * 0.8); // Hold for sustain duration
                inputs.Add(GameInput.Create(releaseTime, keyAction, false));
                
                foreach (var childNote in note.ChildNotes)
                {
                    var childKeyAction = GetProKeysAction(childNote.Key);
                    inputs.Add(GameInput.Create(releaseTime, childKeyAction, false));
                }
            }

            return inputs.ToArray();
        }

        /// <summary>
        /// Generates vocals inputs based on chart notes.
        /// </summary>
        private GameInput[] GenerateVocalsInputs(VocalsPart vocalsPart, Instrument instrument, double startTime, double endTime)
        {
            var inputs = new List<GameInput>();
            const double hitRate = 0.75; // 75% hit rate for vocals

            foreach (var phrase in vocalsPart.NotePhrases.Where(p => p.Time >= startTime && p.Time <= endTime))
            {
                if (_random.NextDouble() > hitRate) continue;

                // Generate inputs for vocal notes within the phrase
                foreach (var note in phrase.PhraseParentNote.ChildNotes)
                {
                    if (note.Time < startTime || note.Time > endTime) continue;
                    if (_random.NextDouble() > hitRate) continue;

                    if (note.IsPercussion)
                    {
                        // Generate percussion hit
                        inputs.Add(GameInput.Create(note.Time, VocalsAction.Hit, 1.0f));
                    }
                    else if (!note.IsNonPitched)
                    {
                        // Generate pitch input for pitched notes
                        var frequency = 440.0f * (float)Math.Pow(2.0, (note.Pitch - 69) / 12.0); // Convert MIDI pitch to frequency
                        inputs.Add(GameInput.Create(note.Time, VocalsAction.Pitch, frequency));

                        // Continue pitch for the duration of the note
                        if (note.TimeLength > 0.1)
                        {
                            var noteEndTime = note.Time + note.TimeLength;
                            inputs.Add(GameInput.Create(noteEndTime, VocalsAction.Pitch, 0.0f)); // Stop pitch
                        }
                    }
                }
            }

            return inputs.ToArray();
        }

        /// <summary>
        /// Generates star power phrase specific inputs.
        /// </summary>
        private GameInput[] GenerateStarPowerPhraseInputs(object difficultyTrack, Instrument instrument, double startTime, double endTime)
        {
            var inputs = new List<GameInput>();

            try
            {
                // Get star power phrases from the difficulty track
                var phrases = GetStarPowerPhrases(difficultyTrack);
                
                foreach (var phrase in phrases.Where(p => p.Time >= startTime && p.Time <= endTime))
                {
                    // Add star power activation at random points during or after the phrase
                    if (_random.NextDouble() < 0.3) // 30% chance to activate star power
                    {
                        var activationTime = phrase.Time + phrase.TimeLength + _random.NextDouble() * 2.0; // 0-2 seconds after phrase
                        if (activationTime <= endTime)
                        {
                            inputs.Add(CreateStarPowerActivationInput(activationTime, instrument));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                YargLogger.LogWarning($"Error generating star power phrase inputs: {ex.Message}");
            }

            return inputs.ToArray();
        }

        /// <summary>
        /// Gets star power phrases from a difficulty track.
        /// </summary>
        private Phrase[] GetStarPowerPhrases(object difficultyTrack)
        {
            try
            {
                return difficultyTrack switch
                {
                    InstrumentDifficulty<GuitarNote> guitarDiff => guitarDiff.Phrases.Where(p => p.Type == PhraseType.StarPower).ToArray(),
                    InstrumentDifficulty<DrumNote> drumsDiff => drumsDiff.Phrases.Where(p => p.Type == PhraseType.StarPower).ToArray(),
                    InstrumentDifficulty<ProKeysNote> proKeysDiff => proKeysDiff.Phrases.Where(p => p.Type == PhraseType.StarPower).ToArray(),
                    VocalsPart vocalsPart => vocalsPart.OtherPhrases.Where(p => p.Type == PhraseType.StarPower).ToArray(),
                    _ => Array.Empty<Phrase>()
                };
            }
            catch
            {
                return Array.Empty<Phrase>();
            }
        }

        /// <summary>
        /// Generates whammy inputs for a sustain note.
        /// </summary>
        private GameInput[] GenerateWhammyInputsForSustain(double sustainStart, double sustainEnd, double testStart, double testEnd)
        {
            var inputs = new List<GameInput>();
            
            // Clamp sustain times to test bounds
            sustainStart = Math.Max(sustainStart, testStart);
            sustainEnd = Math.Min(sustainEnd, testEnd);
            
            if (sustainEnd <= sustainStart) return inputs.ToArray();

            // Generate whammy pattern during sustain
            var whammyStart = sustainStart + 0.05; // Start whammy 50ms into sustain
            var whammyEnd = sustainEnd - 0.05; // End whammy 50ms before sustain ends
            
            if (whammyEnd <= whammyStart) return inputs.ToArray();

            // Random whammy pattern
            var whammyPattern = _random.Next(0, 3);
            switch (whammyPattern)
            {
                case 0: // Continuous whammy
                    inputs.Add(GameInput.Create(whammyStart, GuitarAction.Whammy, 0.8f + (float)_random.NextDouble() * 0.2f));
                    inputs.Add(GameInput.Create(whammyEnd, GuitarAction.Whammy, 0.0f));
                    break;
                    
                case 1: // Intermittent whammy
                    for (double time = whammyStart; time < whammyEnd; time += 0.1 + _random.NextDouble() * 0.2)
                    {
                        var onTime = Math.Min(time, whammyEnd);
                        var offTime = Math.Min(time + 0.05 + _random.NextDouble() * 0.1, whammyEnd);
                        
                        inputs.Add(GameInput.Create(onTime, GuitarAction.Whammy, 0.6f + (float)_random.NextDouble() * 0.4f));
                        if (offTime < whammyEnd)
                            inputs.Add(GameInput.Create(offTime, GuitarAction.Whammy, 0.0f));
                    }
                    break;
                    
                case 2: // Variable intensity whammy
                    for (double time = whammyStart; time < whammyEnd; time += 0.05)
                    {
                        var intensity = 0.3f + (float)_random.NextDouble() * 0.7f;
                        inputs.Add(GameInput.Create(Math.Min(time, whammyEnd), GuitarAction.Whammy, intensity));
                    }
                    inputs.Add(GameInput.Create(whammyEnd, GuitarAction.Whammy, 0.0f));
                    break;
            }

            return inputs.ToArray();
        }

        /// <summary>
        /// Gets the appropriate guitar fret action for a fret number.
        /// </summary>
        private GuitarAction GetGuitarFretAction(int fret)
        {
            return fret switch
            {
                1 => GuitarAction.GreenFret,
                2 => GuitarAction.RedFret,
                3 => GuitarAction.YellowFret,
                4 => GuitarAction.BlueFret,
                5 => GuitarAction.OrangeFret,
                6 => GuitarAction.OrangeFret, // Six fret support - map 6th fret to orange for now
                7 => GuitarAction.GreenFret, // Open note - map to green
                _ => GuitarAction.GreenFret // Default
            };
        }

        /// <summary>
        /// Gets the appropriate drum action for a pad number.
        /// </summary>
        private DrumsAction GetDrumAction(int pad)
        {
            return pad switch
            {
                0 => DrumsAction.Kick,
                1 => DrumsAction.RedDrum,
                2 => DrumsAction.YellowDrum,
                3 => DrumsAction.BlueDrum,
                4 => DrumsAction.GreenDrum,
                5 => DrumsAction.GreenDrum, // 5-lane support
                _ => DrumsAction.RedDrum // Default
            };
        }

        /// <summary>
        /// Gets the appropriate pro keys action for a key number.
        /// </summary>
        private ProKeysAction GetProKeysAction(int key)
        {
            // Map key numbers to ProKeysAction enum values
            if (key >= 1 && key <= 25)
            {
                return (ProKeysAction)(key - 1); // Key1 = 0, Key2 = 1, etc.
            }
            return ProKeysAction.Key1; // Default
        }

        /// <summary>
        /// Gets the random seed used by this generator.
        /// </summary>
        public int? Seed => _seed;
    }
}