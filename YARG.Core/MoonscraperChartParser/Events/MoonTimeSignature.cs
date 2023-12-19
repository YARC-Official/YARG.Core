﻿// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System;

namespace MoonscraperChartEditor.Song
{
    [Serializable]
    internal class MoonTimeSignature : MoonObject
    {
        public uint numerator;
        public uint denominator;

        public MoonTimeSignature(uint _position = 0, uint _numerator = 4, uint _denominator = 4)
            : base(ID.TimeSignature, _position)
        {
            numerator = _numerator;
            denominator = _denominator;
        }

        public struct BeatInfo
        {
            public uint tickOffset;
            public uint tickGap;
            public int repetitions;
            public uint repetitionCycleOffset;
        }

        public struct MeasureInfo
        {
            public BeatInfo measureLine;
            public BeatInfo beatLine;
            public BeatInfo quarterBeatLine;
        }

        public MeasureInfo GetMeasureInfo(MoonSong song)
        {
            var measureInfo = new MeasureInfo();
            float resolution = song.resolution;

            {
                measureInfo.measureLine.tickOffset = 0;
                measureInfo.measureLine.repetitions = 1;
                measureInfo.measureLine.tickGap = (uint)(resolution * 4.0f / denominator * numerator);
                measureInfo.measureLine.repetitionCycleOffset = 0;
            }

            {
                measureInfo.beatLine.tickGap = measureInfo.measureLine.tickGap / numerator;
                measureInfo.beatLine.tickOffset = measureInfo.beatLine.tickGap;
                measureInfo.beatLine.repetitions = (int)numerator - 1;
                measureInfo.beatLine.repetitionCycleOffset = measureInfo.beatLine.tickOffset;
            }

            {
                measureInfo.quarterBeatLine.tickGap = measureInfo.beatLine.tickGap;
                measureInfo.quarterBeatLine.tickOffset = measureInfo.beatLine.tickGap / 2;
                measureInfo.quarterBeatLine.repetitions = (int)numerator;
                measureInfo.quarterBeatLine.repetitionCycleOffset = 0;
            }

            return measureInfo;
        }

        public override bool ValueEquals(MoonObject obj)
        {
            bool baseEq = base.ValueEquals(obj);
            if (!baseEq || obj is not MoonTimeSignature ts)
                return baseEq;

            return numerator == ts.numerator || denominator == ts.denominator;
        }

        protected override MoonObject CloneImpl() => Clone();

        public new MoonTimeSignature Clone()
        {
            return new MoonTimeSignature(tick, numerator, denominator);
        }

        public override string ToString()
        {
            return $"Time signature {numerator}/{denominator} at tick {tick}";
        }
    }
}
