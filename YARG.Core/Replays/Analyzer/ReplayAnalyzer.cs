using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Engine.Drums;
using YARG.Core.Engine.Drums.Engines;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.Guitar.Engines;
using YARG.Core.Engine.ProKeys.Engines;
using YARG.Core.Engine.ProKeys;
using YARG.Core.Engine.Vocals;
using YARG.Core.Engine.Vocals.Engines;
using YARG.Core.Game;
using YARG.Core.Logging;
using Cysharp.Text;

namespace YARG.Core.Replays.Analyzer
{
    public class ReplayAnalyzer
    {
        private readonly SongChart _chart;

        private readonly ReplayInfo _replayInfo;
        private readonly ReplayData _replayData;

        private readonly double _fps;
        private readonly bool   _doFrameUpdates;

        private readonly int _frameNum;

        private readonly Random _random = new();

        public ReplayAnalyzer(SongChart chart, ReplayInfo replayInfo, ReplayData replayData, double fps, int frameNum)
        {
            _chart = chart;

            _replayInfo = replayInfo;
            _replayData = replayData;

            _frameNum = frameNum;

            _fps = fps;
            _doFrameUpdates = _fps > 0 || (replayData.FrameTimes.Length != 0 && _frameNum != -1);
        }

        public static AnalysisResult[] AnalyzeReplay(SongChart chart, ReplayInfo info, ReplayData data, double fps = 0, int frameNum = -1)
        {
            var analyzer = new ReplayAnalyzer(chart, info, data, fps, frameNum);
            return analyzer.Analyze();
        }

        public static string PrintStatDifferences(BaseStats originalStats, BaseStats resultStats)
        {
            var sb = new StringBuilder();

            void AppendStatDifference<T>(string name, T frameStat, T resultStat)
                where T : IEquatable<T>
            {
                if (frameStat.Equals(resultStat))
                    sb.AppendLine($"- {name + ":",-31} {frameStat,-12} (identical)");
                else
                    sb.AppendLine($"- {name + ":",-31} {frameStat,-10} -> {resultStat}");
            }

            sb.AppendLine("Base stats:");
            AppendStatDifference("CommittedScore", originalStats.CommittedScore, resultStats.CommittedScore);
            AppendStatDifference("PendingScore", originalStats.PendingScore, resultStats.PendingScore);
            AppendStatDifference("TotalScore", originalStats.TotalScore, resultStats.TotalScore);
            AppendStatDifference("StarScore", originalStats.StarScore, resultStats.StarScore);
            AppendStatDifference("Combo", originalStats.Combo, resultStats.Combo);
            AppendStatDifference("MaxCombo", originalStats.MaxCombo, resultStats.MaxCombo);
            AppendStatDifference("ScoreMultiplier", originalStats.ScoreMultiplier, resultStats.ScoreMultiplier);
            AppendStatDifference("NotesHit", originalStats.NotesHit, resultStats.NotesHit);
            AppendStatDifference("TotalNotes", originalStats.TotalNotes, resultStats.TotalNotes);
            AppendStatDifference("NotesMissed", originalStats.NotesMissed, resultStats.NotesMissed);
            AppendStatDifference("Percent", originalStats.Percent, resultStats.Percent);
            AppendStatDifference("StarPowerTickAmount", originalStats.StarPowerTickAmount,
                resultStats.StarPowerTickAmount);
            AppendStatDifference("TotalStarPowerTicks", originalStats.TotalStarPowerTicks,
                resultStats.TotalStarPowerTicks);
            AppendStatDifference("TimeInStarPower", originalStats.TimeInStarPower, resultStats.TimeInStarPower);
            AppendStatDifference("IsStarPowerActive", originalStats.IsStarPowerActive, resultStats.IsStarPowerActive);
            AppendStatDifference("StarPowerPhrasesHit", originalStats.StarPowerPhrasesHit,
                resultStats.StarPowerPhrasesHit);
            AppendStatDifference("TotalStarPowerPhrases", originalStats.TotalStarPowerPhrases,
                resultStats.TotalStarPowerPhrases);
            AppendStatDifference("StarPowerPhrasesMissed", originalStats.StarPowerPhrasesMissed,
                resultStats.StarPowerPhrasesMissed);
            AppendStatDifference("SoloBonuses", originalStats.SoloBonuses, resultStats.SoloBonuses);
            AppendStatDifference("StarPowerScore", originalStats.StarPowerScore, resultStats.StarPowerScore);
            // PrintStatDifference("Stars",                  originalStats.Stars,                  resultStats.Stars);

            sb.AppendLine();
            switch (originalStats, resultStats)
            {
                case (GuitarStats originalGuitar, GuitarStats resultGuitar):
                {
                    sb.AppendLine("Guitar stats:");
                    AppendStatDifference("Overstrums", originalGuitar.Overstrums, resultGuitar.Overstrums);
                    AppendStatDifference("HoposStrummed", originalGuitar.HoposStrummed, resultGuitar.HoposStrummed);
                    AppendStatDifference("GhostInputs", originalGuitar.GhostInputs, resultGuitar.GhostInputs);
                    AppendStatDifference("StarPowerWhammyTicks", originalGuitar.StarPowerWhammyTicks,
                        resultGuitar.StarPowerWhammyTicks);
                    AppendStatDifference("SustainScore", originalGuitar.SustainScore, resultGuitar.SustainScore);
                    break;
                }
                case (DrumsStats originalDrums, DrumsStats resultDrums):
                {
                    sb.AppendLine("Drums stats:");
                    AppendStatDifference("Overhits", originalDrums.Overhits, resultDrums.Overhits);
                    break;
                }
                case (VocalsStats originalVocals, VocalsStats resultVocals):
                {
                    sb.AppendLine("Vocals stats:");
                    AppendStatDifference("TicksHit", originalVocals.TicksHit, resultVocals.TicksHit);
                    AppendStatDifference("TicksMissed", originalVocals.TicksMissed, resultVocals.TicksMissed);
                    AppendStatDifference("TotalTicks", originalVocals.TotalTicks, resultVocals.TotalTicks);
                    break;
                }
                case (ProKeysStats originalKeys, ProKeysStats resultKeys):
                {
                    sb.AppendLine("Pro Keys stats:");
                    AppendStatDifference("Overhits", originalKeys.Overhits, resultKeys.Overhits);
                    break;
                }
                default:
                {
                    if (originalStats.GetType() != resultStats.GetType())
                        sb.AppendLine(
                            $"Stats types do not match! Original: {originalStats.GetType()}, result: {resultStats.GetType()}");
                    else
                        sb.AppendLine($"Unhandled stats type {originalStats.GetType()}!");
                    break;
                }
            }

            return sb.ToString();
        }

        private AnalysisResult[] Analyze()
        {
            var results = new AnalysisResult[_replayData.Frames.Length];

            for (int i = 0; i < results.Length; i++)
            {
                var frame = _replayData.Frames[i];
                var result = RunFrame(frame);

                results[i] = result;
            }

            return results;
        }

        private AnalysisResult RunFrame(ReplayFrame frame)
        {
            var engine = CreateEngine(frame.Profile, frame.EngineParameters);
            engine.SetSpeed(frame.EngineParameters.SongSpeed);
            engine.Reset();

            double maxTime = _replayInfo.ReplayLength;
            if (frame.Inputs.Length > 0)
            {
                double last = frame.Inputs[^1].Time;
                if (last > maxTime)
                {
                    maxTime = last;
                }
            }

            if (!_doFrameUpdates)
            {
                // If we're not doing frame updates, just queue all of the inputs at once
                foreach (var input in frame.Inputs)
                {
                    var inp = input;
                    engine.QueueInput(ref inp);
                }

                // Run the engine updates
                engine.Update(maxTime);
            }
            else
            {
                // If we're doing frame updates, the inputs and frame times must be
                // "interweaved" so nothing gets queued in the future
                int currentInput = 0;
                if (_replayData.FrameTimes.Length != 0)
                {
                    for (int frameIndex = 0; frameIndex < _replayData.FrameTimes.Length; frameIndex++)
                    {
                        double time = _replayData.FrameTimes[frameIndex];

                        if (frameIndex == _frameNum)
                        {
                            Debugger.Break();
                        }

                        for (; currentInput < frame.Inputs.Length; currentInput++)
                        {
                            var input = frame.Inputs[currentInput];
                            if (input.Time > time)
                            {
                                break;
                            }

                            engine.QueueInput(ref input);
                        }

                        engine.Update(time);
                    }
                }
                else
                {
                    foreach (var time in GenerateFrameTimes(-2, maxTime))
                    {
                        for (; currentInput < frame.Inputs.Length; currentInput++)
                        {
                            var input = frame.Inputs[currentInput];
                            if (input.Time > time)
                            {
                                break;
                            }

                            engine.QueueInput(ref input);
                        }

                        engine.Update(time);
                    }
                }
            }

            bool passed = IsPassResult(frame.Stats, engine.BaseStats, out string log);

            return new AnalysisResult
            {
                Passed = passed,
                Frame = frame,
                OriginalStats = frame.Stats,
                ResultStats = engine.BaseStats,
                StatLog = log,
            };
        }

        private BaseEngine CreateEngine(YargProfile profile, BaseEngineParameters parameters)
        {
            switch (profile.GameMode)
            {
                case GameMode.FiveFretGuitar:
                {
                    // Reset the notes
                    var notes = _chart.GetFiveFretTrack(profile.CurrentInstrument)
                        .GetDifficulty(profile.CurrentDifficulty).Clone();
                    profile.ApplyModifiers(notes);
                    foreach (var note in notes.Notes)
                    {
                        foreach (var subNote in note.AllNotes)
                        {
                            subNote.ResetNoteState();
                        }
                    }

                    // Create engine
                    return new YargFiveFretEngine(
                        notes,
                        _chart.SyncTrack,
                        (GuitarEngineParameters) parameters,
                        profile.IsBot);
                }
                case GameMode.FourLaneDrums:
                case GameMode.FiveLaneDrums:
                {
                    // Reset the notes
                    var notes = _chart.GetDrumsTrack(profile.CurrentInstrument)
                        .GetDifficulty(profile.CurrentDifficulty).Clone();
                    profile.ApplyModifiers(notes);
                    foreach (var note in notes.Notes)
                    {
                        foreach (var subNote in note.AllNotes)
                        {
                            subNote.ResetNoteState();
                        }
                    }

                    // Create engine
                    return new YargDrumsEngine(
                        notes,
                        _chart.SyncTrack,
                        (DrumsEngineParameters) parameters,
                        profile.IsBot);
                }
                case GameMode.ProKeys:
                {
                    // Reset the notes
                    var notes = _chart.ProKeys.GetDifficulty(profile.CurrentDifficulty).Clone();
                    profile.ApplyModifiers(notes);
                    foreach (var note in notes.Notes)
                    {
                        foreach (var subNote in note.AllNotes)
                        {
                            subNote.ResetNoteState();
                        }
                    }

                    // Create engine
                    return new YargProKeysEngine(
                        notes,
                        _chart.SyncTrack,
                        (ProKeysEngineParameters) parameters,
                        profile.IsBot);
                }
                case GameMode.Vocals:
                {
                    // Get the notes
                    var notes = _chart.GetVocalsTrack(profile.CurrentInstrument)
                        .Parts[profile.HarmonyIndex].CloneAsInstrumentDifficulty();

                    // No idea how vocals applies modifiers lol
                    //profile.ApplyModifiers(notes);

                    // Create engine
                    return new YargVocalsEngine(
                        notes,
                        _chart.SyncTrack,
                        (VocalsEngineParameters) parameters,
                        profile.IsBot);
                }
                default:
                    throw new InvalidOperationException("Game mode not configured!");
            }
        }

        private List<double> GenerateFrameTimes(double from, double to)
        {
            YargLogger.Assert(to > from, "Invalid time range");

            double frameTime = 1.0 / _fps;

            var times = new List<double>();
            for (double time = from; time < to; time += frameTime)
            {
                // Add up to 10% random adjustment to the frame time
                var randomAdjustment = _random.NextDouble() * 0.1;

                // Randomly make the adjustment negative
                if (_random.Next(2) == 0 && time > from)
                {
                    randomAdjustment = -randomAdjustment;
                }

                double adjustedTime = time + frameTime * randomAdjustment;

                if (adjustedTime > to)
                {
                    adjustedTime = to;
                }

                times.Add(adjustedTime);
            }

            // Add the end time just in case
            times.Add(to);

            return times;
        }

        // ReSharper disable CompareOfFloatsByEqualityOperator

        private static bool IsPassResult(BaseStats original, BaseStats result, out string log)
        {
            // For easier maintenance/reading, manually check log level and
            // use a string builder instead of LogFormat methods

            var builder = new Utf16ValueStringBuilder(true);

            // This helper is copied to the instrument-specific methods
            // because passing a `using` variable by reference is disallowed,
            // and working around that is annoying
            void FormatStat<T>(string stat, T originalValue, T resultValue)
                where T : IEquatable<T>
            {
                string format = originalValue.Equals(resultValue)
                    ? "- {0}: {1} == {2}\n"
                    : "- {0}: {1} != {2}\n";
                builder.AppendFormat(format, stat, originalValue, resultValue);
            }

            // Commented stats aren't serialized

            builder.AppendLine("Scoring:");
            FormatStat("Committed score", original.CommittedScore, result.CommittedScore);
            FormatStat("Pending score", original.PendingScore, result.PendingScore);
            FormatStat("Score from notes", original.NoteScore, result.NoteScore);
            FormatStat("Score from sustains", original.SustainScore, result.SustainScore);
            FormatStat("Score from multipliers", original.MultiplierScore, result.MultiplierScore);
            FormatStat("Score from solos", original.SoloBonuses, result.SoloBonuses);
            FormatStat("Score from SP", original.StarPowerScore, result.StarPowerScore);
            // FormatStat("Stars", original.Stars, result.Stars);

            builder.AppendLine();

            builder.AppendLine("Performance:");
            FormatStat("Notes hit", original.NotesHit, result.NotesHit);
            FormatStat("Notes missed", original.NotesMissed, result.NotesMissed);
            FormatStat("Combo", original.Combo, result.Combo);
            FormatStat("Max combo", original.MaxCombo, result.MaxCombo);
            FormatStat("Multiplier", original.ScoreMultiplier, result.ScoreMultiplier);
            FormatStat("Hit percent", original.Percent, result.Percent);

            builder.AppendLine();

            builder.AppendLine("Star Power:");
            FormatStat("Phrases hit", original.StarPowerPhrasesHit, result.StarPowerPhrasesHit);
            FormatStat("Phrases missed", original.StarPowerPhrasesMissed, result.StarPowerPhrasesMissed);
            FormatStat("Total ticks earned", original.TotalStarPowerTicks, result.TotalStarPowerTicks);
            FormatStat("Remaining ticks", original.StarPowerTickAmount, result.StarPowerTickAmount);
            FormatStat("Ticks from whammy", original.StarPowerWhammyTicks, result.StarPowerWhammyTicks);
            FormatStat("Time in SP", original.TimeInStarPower, result.TimeInStarPower);
            FormatStat("Activation count", original.StarPowerActivationCount, result.StarPowerActivationCount);
            // FormatStat("Total bars filled", original.TotalStarPowerBarsFilled, result.TotalStarPowerBarsFilled);
            FormatStat("Ended with SP active", original.IsStarPowerActive, result.IsStarPowerActive);

            builder.AppendLine();

            bool scoringPass =
                original.CommittedScore == result.CommittedScore &&
                original.PendingScore == result.PendingScore &&
                original.NoteScore == result.NoteScore &&
                original.SustainScore == result.SustainScore &&
                original.MultiplierScore == result.MultiplierScore &&
                original.SoloBonuses == result.SoloBonuses &&
                original.StarPowerScore == result.StarPowerScore; // &&
            // original.Stars == result.Stars;

            bool performancePass =
                original.NotesHit == result.NotesHit &&
                original.SustainScore == result.SustainScore &&
                original.NotesMissed == result.NotesMissed &&
                original.Combo == result.Combo &&
                original.MaxCombo == result.MaxCombo &&
                original.ScoreMultiplier == result.ScoreMultiplier &&
                original.Percent == result.Percent;

            bool spPass =
                original.StarPowerPhrasesHit == result.StarPowerPhrasesHit &&
                original.StarPowerPhrasesMissed == result.StarPowerPhrasesMissed &&
                original.IsStarPowerActive == result.IsStarPowerActive &&
                original.StarPowerTickAmount == result.StarPowerTickAmount &&
                original.TotalStarPowerTicks == result.TotalStarPowerTicks &&
                original.StarPowerWhammyTicks == result.StarPowerWhammyTicks &&
                original.TimeInStarPower == result.TimeInStarPower &&
                original.StarPowerActivationCount == result.StarPowerActivationCount &&
                //original.TotalStarPowerBarsFilled == result.TotalStarPowerBarsFilled &&
                original.IsStarPowerActive == result.IsStarPowerActive;

            bool generalPass = scoringPass && performancePass && spPass;

            bool instrumentPass;
            switch (original, result)
            {
                case (GuitarStats guitar1, GuitarStats guitar2):
                    instrumentPass = IsInstrumentPassResult(guitar1, guitar2, ref builder);
                    break;

                case (DrumsStats drums1, DrumsStats drums2):
                    instrumentPass = IsInstrumentPassResult(drums1, drums2, ref builder);
                    break;

                case (VocalsStats vox1, VocalsStats vox2):
                    instrumentPass = IsInstrumentPassResult(vox1, vox2, ref builder);
                    break;

                // case (ProGuitarStats pg1, ProGuitarStats pg2):
                //     instrumentPass = IsInstrumentPassResult(pg1, pg2, ref builder);
                //     break;

                case (ProKeysStats pk1, ProKeysStats pk2):
                    instrumentPass = IsInstrumentPassResult(pk1, pk2, ref builder);
                    break;

                default:
                    YargLogger.Assert(original.GetType() == result.GetType());
                    YargLogger.LogFormatDebug("Instrument-specific validation not yet implemented for {0}",
                        original.GetType());
                    instrumentPass = true;
                    break;
            }

            log = builder.ToString();
            builder.Dispose();

            return generalPass && instrumentPass;
        }

        private static bool IsInstrumentPassResult(GuitarStats original, GuitarStats result,
            ref Utf16ValueStringBuilder builder)
        {
            if (YargLogger.MinimumLogLevel <= LogLevel.Debug)
            {
                void FormatStat<T>(string stat, T originalValue, T resultValue, ref Utf16ValueStringBuilder builder)
                    where T : IEquatable<T>
                {
                    string format = originalValue.Equals(resultValue)
                        ? "- {0}: {1} == {2}\n"
                        : "- {0}: {1} != {2}\n";
                    builder.AppendFormat(format, stat, originalValue, resultValue);
                }

                builder.AppendLine("Guitar:");
                FormatStat("Overstrums", original.Overstrums, result.Overstrums, ref builder);
                FormatStat("Ghost inputs", original.GhostInputs, result.GhostInputs, ref builder);
                FormatStat("HOPOs strummed", original.HoposStrummed, result.HoposStrummed, ref builder);
            }

            return original.Overstrums == result.Overstrums &&
                original.GhostInputs == result.GhostInputs &&
                original.HoposStrummed == result.HoposStrummed;
        }

        private static bool IsInstrumentPassResult(DrumsStats original, DrumsStats result, ref Utf16ValueStringBuilder builder)
        {
            void FormatStat<T>(string stat, T originalValue, T resultValue, ref Utf16ValueStringBuilder builder)
                where T : IEquatable<T>
            {
                string format = originalValue.Equals(resultValue)
                    ? "- {0}: {1} == {2}\n"
                    : "- {0}: {1} != {2}\n";
                builder.AppendFormat(format, stat, originalValue, resultValue);
            }

            builder.AppendLine("Drums:");
            FormatStat("Overhits", original.Overhits, result.Overhits, ref builder);
            FormatStat("Ghosts hit correctly", original.GhostsHit, result.GhostsHit, ref builder);
            FormatStat("Ghosts hit incorrectly",
                original.TotalGhosts - original.GhostsHit,
                result.TotalGhosts - result.GhostsHit, ref builder);
            FormatStat("Accents hit correctly", original.AccentsHit, result.AccentsHit, ref builder);
            FormatStat("Accents hit incorrectly",
                original.TotalAccents - original.AccentsHit,
                result.TotalAccents - result.AccentsHit, ref builder);
            FormatStat("Score from dynamics", original.DynamicsBonus, result.DynamicsBonus, ref builder);

            return original.Overhits == result.Overhits &&
                original.GhostsHit == result.GhostsHit &&
                original.TotalGhosts == result.TotalGhosts &&
                original.AccentsHit == result.AccentsHit &&
                original.TotalAccents == result.TotalAccents &&
                original.DynamicsBonus == result.DynamicsBonus;
        }

        private static bool IsInstrumentPassResult(VocalsStats original, VocalsStats result, ref Utf16ValueStringBuilder builder)
        {
            void FormatStat<T>(string stat, T originalValue, T resultValue, ref Utf16ValueStringBuilder builder)
                where T : IEquatable<T>
            {
                string format = originalValue.Equals(resultValue)
                    ? "- {0}: {1} == {2}\n"
                    : "- {0}: {1} != {2}\n";
                builder.AppendFormat(format, stat, originalValue, resultValue);
            }

            builder.AppendLine("Vocals:");
            FormatStat("Note ticks hit", original.TicksHit, result.TicksHit, ref builder);
            FormatStat("Note ticks missed", original.TicksMissed, result.TicksMissed, ref builder);

            return original.TicksHit == result.TicksHit &&
                original.TicksMissed == result.TicksMissed;
        }

        // private static bool IsInstrumentPassResult(ProGuitarStats original, ProGuitarStats result)
        // {
        //     if (YargLogger.MinimumLogLevel <= LogLevel.Debug)
        //     {
        //         using var builder = new Utf16ValueStringBuilder(true);

        //         void FormatStat<T>(string stat, T originalValue, T resultValue)
        //             where T : IEquatable<T>
        //         {
        //             string format = originalValue.Equals(resultValue)
        //                 ? "- {0}: {1} == {2}\n"
        //                 : "- {0}: {1} != {2}\n";
        //             builder.AppendFormat(format, stat, originalValue, resultValue);
        //         }

        //         builder.AppendLine("Pro Guitar:");
        //         FormatStat("Stat", original.Stat, result.Stat);
        //         YargLogger.LogDebug(builder.ToString());
        //         builder.Clear();
        //     }

        //     return original.Stat == result.Stat;
        // }

        private static bool IsInstrumentPassResult(ProKeysStats original, ProKeysStats result, ref Utf16ValueStringBuilder builder)
        {
            void FormatStat<T>(string stat, T originalValue, T resultValue, ref Utf16ValueStringBuilder builder)
                where T : IEquatable<T>
            {
                string format = originalValue.Equals(resultValue)
                    ? "- {0}: {1} == {2}\n"
                    : "- {0}: {1} != {2}\n";
                builder.AppendFormat(format, stat, originalValue, resultValue);
            }

            builder.AppendLine("Guitar:");
            FormatStat("Overhits", original.Overhits, result.Overhits, ref builder);
            FormatStat("Fat fingers ignored", original.FatFingersIgnored, result.FatFingersIgnored, ref builder);

            return original.Overhits == result.Overhits &&
                original.FatFingersIgnored == result.FatFingersIgnored;
        }

        // ReSharper restore CompareOfFloatsByEqualityOperator
    }
}