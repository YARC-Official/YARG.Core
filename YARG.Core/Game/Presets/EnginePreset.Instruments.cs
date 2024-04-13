using System;
using YARG.Core.Engine;
using YARG.Core.Engine.Drums;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.Vocals;

namespace YARG.Core.Game
{
    public partial class EnginePreset
    {
        public const double DEFAULT_WHAMMY_BUFFER = 0.25;

        public const int DEFAULT_MAX_MULTIPLIER = 4;
        public const int BASS_MAX_MULTIPLIER    = 6;

        /// <summary>
        /// A preset for a hit window. This should
        /// be used within each engine preset class.
        /// </summary>
        public struct HitWindowPreset
        {
            public double MaxWindow;
            public double MinWindow;

            public bool IsDynamic;

            public double FrontToBackRatio;

            public HitWindowSettings Create()
            {
                return new HitWindowSettings(MaxWindow, MinWindow, FrontToBackRatio, IsDynamic);
            }
        }

        /// <summary>
        /// The engine preset for five fret guitar.
        /// </summary>
        public class FiveFretGuitarPreset
        {
            public bool AntiGhosting     = true;
            public bool InfiniteFrontEnd = false;

            public double HopoLeniency = 0.08;

            public double StrumLeniency      = 0.05;
            public double StrumLeniencySmall = 0.025;

            public HitWindowPreset HitWindow = new()
            {
                MaxWindow = 0.14,
                MinWindow = 0.14,
                IsDynamic = false,
                FrontToBackRatio = 1.0
            };

            public FiveFretGuitarPreset Copy()
            {
                return new FiveFretGuitarPreset
                {
                    AntiGhosting = AntiGhosting,
                    InfiniteFrontEnd = InfiniteFrontEnd,
                    HopoLeniency = HopoLeniency,
                    StrumLeniency = StrumLeniency,
                    StrumLeniencySmall = StrumLeniencySmall,
                    HitWindow = HitWindow,
                };
            }

            public GuitarEngineParameters Create(float[] starMultiplierThresholds, bool isBass)
            {
                var hitWindow = HitWindow.Create();
                return new GuitarEngineParameters(
                    hitWindow,
                    isBass ? BASS_MAX_MULTIPLIER : DEFAULT_MAX_MULTIPLIER,
                    starMultiplierThresholds,
                    HopoLeniency,
                    StrumLeniency,
                    StrumLeniencySmall,
                    DEFAULT_WHAMMY_BUFFER,
                    InfiniteFrontEnd,
                    AntiGhosting);
            }
        }

        /// <summary>
        /// The engine preset for four and five lane drums. These two game modes
        /// use the same engine, so there's no point in splitting them up.
        /// </summary>
        public class DrumsPreset
        {
            public HitWindowPreset HitWindow = new()
            {
                MaxWindow = 0.14,
                MinWindow = 0.14,
                IsDynamic = false,
                FrontToBackRatio = 1.0
            };

            public DrumsPreset Copy()
            {
                return new DrumsPreset
                {
                    HitWindow = HitWindow
                };
            }

            public DrumsEngineParameters Create(float[] starMultiplierThresholds, DrumsEngineParameters.DrumMode mode)
            {
                var hitWindow = HitWindow.Create();
                return new DrumsEngineParameters(
                    hitWindow,
                    DEFAULT_MAX_MULTIPLIER,
                    starMultiplierThresholds,
                    mode);
            }
        }


        /// <summary>
        /// The engine preset for vocals/harmonies.
        /// </summary>
        public class VocalsPreset
        {
            // Pitch window is in semitones (max. difference between correct pitch and sung pitch).
            public float PitchWindowE = 1.7f;
            public float PitchWindowM = 1.4f;
            public float PitchWindowH = 1.1f;
            public float PitchWindowX = 0.8f;

            /// <summary>
            /// The perfect pitch window is equal to the pitch window times the perfect pitch percent,
            /// for all difficulties.
            /// </summary>
            public float PerfectPitchPercent = 0.6f;

            // These percentages may seem low, but accounting for delay,
            // plosives not being detected, etc., it's pretty good.
            public float HitPercentE = 0.325f;
            public float HitPercentM = 0.400f;
            public float HitPercentH = 0.450f;
            public float HitPercentX = 0.575f;

            public VocalsPreset Copy()
            {
                return new VocalsPreset
                {
                    PitchWindowE = PitchWindowE,
                    PitchWindowM = PitchWindowM,
                    PitchWindowH = PitchWindowH,
                    PitchWindowX = PitchWindowX,
                    PerfectPitchPercent = PerfectPitchPercent,
                    HitPercentE = HitPercentE,
                    HitPercentM = HitPercentM,
                    HitPercentH = HitPercentH,
                    HitPercentX = HitPercentX,
                };
            }

            public VocalsEngineParameters Create(float[] starMultiplierThresholds, Difficulty difficulty,
                float updatesPerSecond)
            {
                // Hit window is in semitones (max. difference between correct pitch and sung pitch).
                var (pitchWindow, hitPercent) = difficulty switch
                {
                    Difficulty.Easy   => (PitchWindowE, HitPercentE),
                    Difficulty.Medium => (PitchWindowM, HitPercentM),
                    Difficulty.Hard   => (PitchWindowH, HitPercentH),
                    Difficulty.Expert => (PitchWindowX, HitPercentX),
                    _ => throw new InvalidOperationException("Unreachable")
                };

                // TODO: This is for percussion
                var hitWindow = new HitWindowSettings(pitchWindow, 0.03, 1, false);

                return new VocalsEngineParameters(
                    hitWindow,
                    DEFAULT_MAX_MULTIPLIER,
                    starMultiplierThresholds,
                    pitchWindow,
                    pitchWindow * PerfectPitchPercent,
                    hitPercent,
                    updatesPerSecond,
                    true);
            }
        }
    }
}