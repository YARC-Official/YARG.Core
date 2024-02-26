using System;
using System.Collections.Generic;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Engine.Drums;
using YARG.Core.Engine.Drums.Engines;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.Guitar.Engines;
using YARG.Core.Engine.Vocals;
using YARG.Core.Engine.Vocals.Engines;
using YARG.Core.Game;

namespace YARG.Core.Replays.Analyzer
{
    public class ReplayAnalyzer
    {
        private readonly SongChart _chart;
        private readonly Replay    _replay;

        private readonly double _fps;

        private readonly bool _doFrameUpdates;

        private readonly Random _random = new();

        public ReplayAnalyzer(SongChart chart, Replay replay, double fps)
        {
            _chart = chart;
            _replay = replay;
            _fps = fps;
            _doFrameUpdates = _fps > 0;
        }

        public static AnalysisResult[] AnalyzeReplay(SongChart chart, Replay replay, double fps = 0)
        {
            var analyzer = new ReplayAnalyzer(chart, replay, fps);
            return analyzer.Analyze();
        }

        private AnalysisResult[] Analyze()
        {
            var results = new AnalysisResult[_replay.Frames.Length];

            for (int i = 0; i < results.Length; i++)
            {
                var frame = _replay.Frames[i];
                var result = RunFrame(frame);

                results[i] = result;
            }

            return results;
        }

        private AnalysisResult RunFrame(ReplayFrame frame)
        {
            var engine = CreateEngine(frame.PlayerInfo.Profile, frame.EngineParameters);
            engine.Reset();

            double maxTime = _chart.GetEndTime() + 1;

            var frameTimes = new List<double>();

            if (_doFrameUpdates)
            {
                double currentTime = -2;

                if (frame.Inputs.Length > 0)
                {
                    // Pre-song
                    GenerateFrameTimes(frameTimes, currentTime, frame.Inputs[0].Time);

                    foreach (double time in frameTimes)
                    {
                        engine.UpdateEngineToTime(time);
                    }
                }

                int inputIndex = 0;

                while(inputIndex < frame.Inputs.Length)
                {
                    currentTime = frame.Inputs[inputIndex].Time;
                    var nextTime = inputIndex + 1 < frame.Inputs.Length ? frame.Inputs[inputIndex + 1].Time : maxTime;

                    frameTimes.Clear();
                    GenerateFrameTimes(frameTimes, currentTime, nextTime);
                    inputIndex++;

                    engine.QueueInput(ref frame.Inputs[inputIndex]);

                    foreach (double time in frameTimes)
                    {
                        engine.UpdateEngineToTime(time);
                    }
                }

                // End of song
                frameTimes.Clear();
                GenerateFrameTimes(frameTimes, currentTime, maxTime);

                foreach (double time in frameTimes)
                {
                    engine.UpdateEngineToTime(time);
                }
            }
            else
            {
                // Run each input through the engine
                foreach (var input in frame.Inputs)
                {
                    var inp = input;
                    engine.QueueInput(ref inp);
                }

                engine.UpdateEngineInputs();
            }

            bool passed = IsPassResult(frame.Stats, engine.BaseStats);

            return new AnalysisResult
            {
                Passed = passed,
                Stats = engine.BaseStats,
                NoteHitDifferences = new List<int>(),
                NoteMissDifferences = new List<int>(),
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
                        .Difficulties[profile.CurrentDifficulty];
                    profile.ApplyModifiers(notes);
                    foreach (var note in notes.Notes)
                    {
                        foreach (var subNote in note.ChordEnumerator())
                        {
                            subNote.ResetNoteState();
                        }
                    }

                    // Create engine
                    return new YargFiveFretEngine(
                        notes,
                        _chart.SyncTrack,
                        (GuitarEngineParameters) parameters);
                }
                case GameMode.FourLaneDrums:
                case GameMode.FiveLaneDrums:
                {
                    // Reset the notes
                    var notes = _chart.GetDrumsTrack(profile.CurrentInstrument).Difficulties[profile.CurrentDifficulty];
                    profile.ApplyModifiers(notes);
                    foreach (var note in notes.Notes)
                    {
                        foreach (var subNote in note.ChordEnumerator())
                        {
                            subNote.ResetNoteState();
                        }
                    }

                    // Create engine
                    return new YargDrumsEngine(
                        notes,
                        _chart.SyncTrack,
                        (DrumsEngineParameters) parameters);
                }
                case GameMode.Vocals:
                {
                    // Get the notes
                    var notes = _chart.GetVocalsTrack(profile.CurrentInstrument).Parts[0].CloneAsInstrumentDifficulty();

                    // No idea how vocals applies modifiers lol
                    //profile.ApplyModifiers(notes);

                    // Create engine
                    return new YargVocalsEngine(
                        notes,
                        _chart.SyncTrack,
                        (VocalsEngineParameters) parameters);
                }
                default:
                    throw new InvalidOperationException("Game mode not configured!");
            }
        }

        private void GenerateFrameTimes(ICollection<double> times, double from, double to)
        {
            YargTrace.Assert(to > from, "Invalid time range");

            double frameTime = 1.0 / _fps;

            for (double time = from; time < to; time += frameTime)
            {
                // Add up to 45% random adjustment to the frame time
                var randomAdjustment = _random.NextDouble() * 0.5;

                // Randomly make the adjustment negative
                if (_random.Next(2) == 0 && time > from)
                {
                    randomAdjustment = -randomAdjustment;
                }

                double adjustedTime = time + frameTime * randomAdjustment;
                times.Add(adjustedTime);
            }
        }

        private static bool IsPassResult(BaseStats original, BaseStats result)
        {
            return original.CommittedScore == result.CommittedScore &&
                original.NotesHit == result.NotesHit &&
                original.NotesMissed == result.NotesMissed &&
                original.Combo == result.Combo &&
                original.MaxCombo == result.MaxCombo &&
                original.SoloBonuses == result.SoloBonuses &&
                original.StarPowerPhrasesHit == result.StarPowerPhrasesHit;
        }
    }
}