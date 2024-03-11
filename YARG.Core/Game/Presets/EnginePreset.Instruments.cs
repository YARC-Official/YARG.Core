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
            // Hit window is in semitones (max. difference between correct pitch and sung pitch).
            public double WindowSizeE = 1.7;
            public double WindowSizeM = 1.4;
            public double WindowSizeH = 1.1;
            public double WindowSizeX = 0.8;

            // These percentages may seem low, but accounting for delay,
            // plosives not being detected, etc., it's pretty good.
            public double HitPercentE = 0.325;
            public double HitPercentM = 0.400;
            public double HitPercentH = 0.450;
            public double HitPercentX = 0.575;

            public VocalsPreset Copy()
            {
                return new VocalsPreset
                {
                    WindowSizeE = WindowSizeE,
                    WindowSizeM = WindowSizeM,
                    WindowSizeH = WindowSizeH,
                    WindowSizeX = WindowSizeX,
                    HitPercentE = HitPercentE,
                    HitPercentM = HitPercentM,
                    HitPercentH = HitPercentH,
                    HitPercentX = HitPercentX,
                };
            }

            public VocalsEngineParameters Create(float[] starMultiplierThresholds, Difficulty difficulty, float updatesPerSecond)
            {
                // Hit window is in semitones (max. difference between correct pitch and sung pitch).
                double windowSize = difficulty switch
                {
                    Difficulty.Easy   => WindowSizeE,
                    Difficulty.Medium => WindowSizeM,
                    Difficulty.Hard   => WindowSizeH,
                    Difficulty.Expert => WindowSizeX,
                    _ => throw new InvalidOperationException("Unreachable")
                };

                double hitPercent = difficulty switch
                {
                    Difficulty.Easy   => HitPercentE,
                    Difficulty.Medium => HitPercentM,
                    Difficulty.Hard   => HitPercentH,
                    Difficulty.Expert => HitPercentX,
                    _ => throw new InvalidOperationException("Unreachable")
                };
                var hitWindow = new HitWindowSettings(windowSize, 0.03, 1, false);
                return new VocalsEngineParameters(
                    hitWindow, 
                    EnginePreset.DEFAULT_MAX_MULTIPLIER,
                    starMultiplierThresholds, 
                    hitPercent, 
                    true, 
                    updatesPerSecond);
            }
        }
    }
}