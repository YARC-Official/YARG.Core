using System;

namespace YARG.Core.Audio
{
    internal class PitchProcessor
    {
        private const int COURSE_OCTAVE_STEPS = 96;
        private const int SCAN_HIFREQ_SIZE = 31;
        private const float SCAN_HIFREQ_STEP = 1.005f;

        private readonly int _blockLen44; // 4/4 block length

        private readonly float _detectLevelThreshold;

        private readonly int _numCourseSteps;
        private readonly float[] _pCourseFreqOffset;
        private readonly float[] _pCourseFreq;
        private readonly float[] _scanHiOffset = new float[SCAN_HIFREQ_SIZE];
        private readonly float[] _peakBuf = new float[SCAN_HIFREQ_SIZE];
        private int _prevPitchIdx;
        private readonly float[] _detectCurve;

        public PitchProcessor(double SampleRate, float MinPitch, float MaxPitch, float DetectLevelThreshold)
        {
            _detectLevelThreshold = DetectLevelThreshold;

            _blockLen44 = (int) (SampleRate / MinPitch + 0.5);

            _numCourseSteps =
                (int) (Math.Log((double) MaxPitch / MinPitch) / Math.Log(2.0) * COURSE_OCTAVE_STEPS + 0.5) + 3;

            _pCourseFreqOffset = new float[_numCourseSteps + 10000];
            _pCourseFreq = new float[_numCourseSteps + 10000];

            _detectCurve = new float[_numCourseSteps];

            var freqStep = 1 / Math.Pow(2.0, 1.0 / COURSE_OCTAVE_STEPS);
            var curFreq = MaxPitch / freqStep;

            // frequency is stored from high to low
            for (var i = 0; i < _numCourseSteps; i++)
            {
                _pCourseFreq[i] = (float) curFreq;
                _pCourseFreqOffset[i] = (float) (SampleRate / curFreq);
                curFreq *= freqStep;
            }

            for (var i = 0; i < SCAN_HIFREQ_SIZE; i++)
                _scanHiOffset[i] = (float) Math.Pow(SCAN_HIFREQ_STEP, SCAN_HIFREQ_SIZE / 2 - i);
        }

        /// <summary>
        /// Determines the pitch of the given sample data.
        /// </summary>
        public float DetectPitch(float[] samplesLo, float[] samplesHi, int numSamples)
        {
            // Level is too low
            if (!LevelIsAbove(samplesLo, numSamples, _detectLevelThreshold) &&
                !LevelIsAbove(samplesHi, numSamples, _detectLevelThreshold))
                return 0;

            return DetectPitchLo(samplesLo, samplesHi);
        }

        /// <summary>
        /// Low resolution pitch detection
        /// </summary>
        private float DetectPitchLo(float[] samplesLo, float[] samplesHi)
        {
            Array.Clear(_detectCurve, 0, _detectCurve.Length);

            const int skipSize = 8, peakScanSize = 23, peakScanSizeHalf = peakScanSize / 2;

            const float peakThresh1 = 200.0f, peakThresh2 = 600.0f;
            var bufferSwitched = false;

            for (var idx = 0; idx < _numCourseSteps; idx += skipSize)
            {
                var blockLen = Math.Min(_blockLen44, (int) _pCourseFreqOffset[idx] * 2);
                float[] curSamples;

                // 258 is at 250 Hz, which is the switchover frequency for the two filters
                var loBuffer = idx >= 258;

                if (loBuffer)
                {
                    if (!bufferSwitched)
                    {
                        Array.Clear(_detectCurve, 258 - peakScanSizeHalf, peakScanSizeHalf + peakScanSizeHalf + 1);
                        bufferSwitched = true;
                    }

                    curSamples = samplesLo;
                }
                else
                {
                    curSamples = samplesHi;
                }

                var stepSizeLoRes = blockLen / 10;
                var stepSizeHiRes = Math.Max(1, Math.Min(5, idx * 5 / _numCourseSteps));

                var fValue = RatioAbsDiffLinear(curSamples, idx, blockLen, stepSizeLoRes, false);

                if (!(fValue > peakThresh1)) continue;

                // Do a closer search for the peak
                var peakIdx = -1;
                var peakVal = 0.0f;
                var prevVal = 0.0f;
                var dir = 4;      // start going forward
                var curPos = idx; // start at center of the scan range
                var begSearch = Math.Max(idx - peakScanSizeHalf, 0);
                var endSearch = Math.Min(idx + peakScanSizeHalf, _numCourseSteps - 1);

                while (curPos >= begSearch && curPos < endSearch)
                {
                    var curVal = RatioAbsDiffLinear(curSamples, curPos, blockLen, stepSizeHiRes, true);

                    if (peakVal < curVal)
                    {
                        peakVal = curVal;
                        peakIdx = curPos;
                    }

                    if (prevVal > curVal)
                    {
                        dir = -dir >> 1;

                        if (dir == 0)
                        {
                            if (peakVal > peakThresh2 && peakIdx >= 6 && peakIdx <= _numCourseSteps - 7)
                            {
                                var fValL = RatioAbsDiffLinear(curSamples, peakIdx - 5, blockLen, stepSizeHiRes, true);
                                var fValR = RatioAbsDiffLinear(curSamples, peakIdx + 5, blockLen, stepSizeHiRes, true);
                                var fPointy = peakVal / (fValL + fValR) * 2.0f;

                                var minPointy = _prevPitchIdx > 0 && Math.Abs(_prevPitchIdx - peakIdx) < 10
                                    ? 1.2f
                                    : 1.5f;

                                if (fPointy > minPointy)
                                {
                                    var pitchHi = DetectPitchHi(curSamples, peakIdx);

                                    if (pitchHi > 1.0f)
                                    {
                                        _prevPitchIdx = peakIdx;
                                        return pitchHi;
                                    }
                                }
                            }

                            break;
                        }
                    }

                    prevVal = curVal;
                    curPos += dir;
                }
            }

            _prevPitchIdx = 0;
            return 0;
        }

        /// <summary>
        /// High resolution pitch detection
        /// </summary>
        private float DetectPitchHi(float[] samples, int lowFreqIdx)
        {
            var peakIdx = -1;
            var prevVal = 0.0f;
            var dir = 4;                   // start going forward
            var curPos = SCAN_HIFREQ_SIZE >> 1; // start at center of the scan range

            Array.Clear(_peakBuf, 0, _peakBuf.Length);

            var offset = _pCourseFreqOffset[lowFreqIdx];

            while (curPos >= 0 && curPos < SCAN_HIFREQ_SIZE)
            {
                if (_peakBuf[curPos] == 0)
                    _peakBuf[curPos] = SumAbsDiffHermite(samples, offset * _scanHiOffset[curPos], _blockLen44, 1);

                if (peakIdx < 0 || _peakBuf[peakIdx] < _peakBuf[curPos]) peakIdx = curPos;

                if (prevVal > _peakBuf[curPos])
                {
                    dir = -dir >> 1;

                    if (dir == 0)
                    {
                        // found the peak
                        var minVal = Math.Min(_peakBuf[peakIdx - 1], _peakBuf[peakIdx + 1]);

                        minVal -= minVal * (1.0f / 32.0f);

                        var y1 = (float) Math.Log10(_peakBuf[peakIdx - 1] - minVal);
                        var y2 = (float) Math.Log10(_peakBuf[peakIdx] - minVal);
                        var y3 = (float) Math.Log10(_peakBuf[peakIdx + 1] - minVal);

                        var fIdx = peakIdx + (y3 - y1) / (2.0f * (2.0f * y2 - y1 - y3));

                        return (float) Math.Pow(SCAN_HIFREQ_STEP, fIdx - SCAN_HIFREQ_SIZE / 2.0) * _pCourseFreq[lowFreqIdx];
                    }
                }

                prevVal = _peakBuf[curPos];
                curPos += dir;
            }

            return 0;
        }

        /// <summary>
        /// Determines whether or not the level of the given buffer is above a certain threshold.
        /// </summary>
        private static bool LevelIsAbove(float[] buffer, int len, float level)
        {
            if (buffer == null || buffer.Length == 0) return false;

            var endIdx = Math.Min(buffer.Length, len);

            for (var idx = 0; idx < endIdx; idx++)
                if (Math.Abs(buffer[idx]) >= level)
                    return true;

            return false;
        }

        /// <summary>
        /// 4-point, 3rd-order Hermite interpolation (x-form)
        /// </summary>
        private static float InterpolateHermite(float fY0, float fY1, float fY2, float fY3, float frac)
        {
            var c1 = 0.5f * (fY2 - fY0);
            var c3 = 1.5f * (fY1 - fY2) + 0.5f * (fY3 - fY0);
            var c2 = fY0 - fY1 + c1 - c3;

            return ((c3 * frac + c2) * frac + c1) * frac + fY1;
        }

        /// <summary>
        /// Linear interpolation
        /// nFrac is based on 1.0 = 256
        /// </summary>
        private static float InterpolateLinear(float y0, float y1, float frac)
            => y0 * (1.0f - frac) + y1 * frac;

        /// <summary>
        /// Medium Low res SumAbsDiff
        /// </summary>
        private float RatioAbsDiffLinear(float[] samples, int freqIdx, int blockLen, int stepSize, bool hiRes)
        {
            if (hiRes && _detectCurve[freqIdx] > 0.0f) return _detectCurve[freqIdx];

            var offsetInt = (int) _pCourseFreqOffset[freqIdx];
            var offsetFrac = _pCourseFreqOffset[freqIdx] - offsetInt;
            var rect = 0.0f;
            var absDiff = 0.01f; // prevent divide by zero
            var count = 0;

            // Do a scan using linear interpolation and the specified step size
            for (var idx = 0; idx < blockLen; idx += stepSize, count++)
            {
                var sample = samples[idx];
                var interp = InterpolateLinear(samples[offsetInt + idx], samples[offsetInt + idx + 1], offsetFrac);
                absDiff += Math.Abs(sample - interp);
                rect += Math.Abs(sample) + Math.Abs(interp);
            }

            var finalVal = rect / absDiff * 100.0f;

            if (hiRes) _detectCurve[freqIdx] = finalVal;

            return finalVal;
        }

        /// <summary>
        /// Medium High res SumAbsDiff
        /// </summary>
        private static float SumAbsDiffHermite(float[] samples, float fOffset, int blockLen, int stepSize)
        {
            var offsetInt = (int) fOffset;
            var offsetFrac = fOffset - offsetInt;
            var value = 0.001f; // prevent divide by zero
            var count = 0;

            // do a scan using linear interpolation and the specified step size
            for (var idx = 0; idx < blockLen; idx += stepSize, count++)
            {
                var offsetIdx = offsetInt + idx;

                value += Math.Abs(samples[idx] - InterpolateHermite(samples[offsetIdx - 1], samples[offsetIdx],
                    samples[offsetIdx + 1], samples[offsetIdx + 2], offsetFrac));
            }

            return count / value;
        }
    }
}