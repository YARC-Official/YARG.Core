using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace YARG.Core.Audio
{
    /// <summary>
    /// Available IIR filter types.
    /// </summary>
    internal enum IIRFilterType
    {
        LowPass,
        HighPass
    }

    /// <summary>
    /// An infinite impulse response filter (old-style analog filter).
    /// </summary>
    internal class IIRFilter
    {
        private const int HISTORY_MASK = 31, HISTORY_SIZE = 32;
        private const int ORDER_MIN = 1, ORDER_MAX = 16;

        private int _order;
        private IIRFilterType _filterType;

        private float _freqLow, _freqHigh, _freqMax, _sampleRate;
        private double[] _real, _imag, _z, _aCoeff, _bCoeff;
        private double[] _inHistory = new double[HISTORY_SIZE], _outHistory = new double[HISTORY_SIZE];
        private int _histIdx;
        private bool _invertDenormal;

        public float FreqLow
        {
            get => _freqLow;
            set
            {
                if (value <= 0)
                    throw new ArgumentOutOfRangeException(nameof(value));
                _freqLow = value;
                Design();
            }
        }

        public float FreqHigh
        {
            get => _freqHigh;
            set
            {
                if (value <= 0)
                    throw new ArgumentOutOfRangeException(nameof(value));
                _freqHigh = value;
                Design();
            }
        }

        public IIRFilter(IIRFilterType type, int order, float freqLimit, float sampleRate)
        {
            if (order < ORDER_MIN || order > ORDER_MAX)
                throw new ArgumentOutOfRangeException(nameof(order));
            if (freqLimit <= 0)
                throw new ArgumentOutOfRangeException(nameof(freqLimit));
            if (sampleRate <= 0)
                throw new ArgumentOutOfRangeException(nameof(sampleRate));

            _filterType = type;

            _order = Math.Clamp(Math.Abs(order), ORDER_MIN, ORDER_MAX);

            switch (_filterType)
            {
                case IIRFilterType.LowPass:
                    _freqHigh = freqLimit;
                    break;

                case IIRFilterType.HighPass:
                    _freqLow = freqLimit;
                    break;
            }

            _sampleRate = sampleRate;
            _freqMax = 0.5f * _sampleRate;

            Design();
        }

        private static bool IsOdd(int n) => (n & 1) == 1;

        private static double Sqr(double value) => value * value;

        /// <summary>
        /// Determines the poles and zeros of the filter based on the bilinear transform method.
        /// </summary>
        [MemberNotNull(nameof(_real))]
        [MemberNotNull(nameof(_imag))]
        [MemberNotNull(nameof(_z))]
        private void LocatePolesAndZeros()
        {
            _real = new double[_order + 1];
            _imag = new double[_order + 1];
            _z = new double[_order + 1];

            // Butterworth, Chebyshev parameters
            int n = _order;

            int ir = n % 2;
            int n1 = n + ir;
            int n2 = (3 * n + ir) / 2 - 1;
            double f1 = _filterType switch
            {
                IIRFilterType.LowPass => _freqHigh,
                IIRFilterType.HighPass => (double) (_freqMax - _freqLow),
                _ => 0.0,
            };
            double tanw1 = Math.Tan(0.5 * Math.PI * f1 / _freqMax);
            double tansqw1 = Sqr(tanw1);

            // Real and Imaginary parts of low-pass poles
            double r;

            for (var k = n1; k <= n2; k++)
            {
                var t = 0.5 * (2 * k + 1 - ir) * Math.PI / n;

                var b3 = 1.0 - 2.0 * tanw1 * Math.Cos(t) + tansqw1;
                r = (1.0 - tansqw1) / b3;
                var i = 2.0 * tanw1 * Math.Sin(t) / b3;

                var m = 2 * (n2 - k) + 1;
                _real[m + ir] = r;
                _imag[m + ir] = Math.Abs(i);
                _real[m + ir + 1] = r;
                _imag[m + ir + 1] = -Math.Abs(i);
            }

            if (IsOdd(n))
            {
                r = (1.0 - tansqw1) / (1.0 + 2.0 * tanw1 + tansqw1);

                _real[1] = r;
                _imag[1] = 0.0;
            }

            switch (_filterType)
            {
                case IIRFilterType.LowPass:
                    for (var m = 1; m <= n; m++) _z[m] = -1.0;
                    break;

                case IIRFilterType.HighPass:
                    // Low-pass to high-pass transformation
                    for (var m = 1; m <= n; m++)
                    {
                        _real[m] = -_real[m];
                        _z[m] = 1.0;
                    }

                    break;
            }
        }

        /// <summary>
        /// Pre-calculates all values necessary for filtering.
        /// </summary>
        // LocatePolesAndZeros
        [MemberNotNull(nameof(_real))]
        [MemberNotNull(nameof(_imag))]
        [MemberNotNull(nameof(_z))]
        // Design
        [MemberNotNull(nameof(_aCoeff))]
        [MemberNotNull(nameof(_bCoeff))]
        private void Design()
        {
            _aCoeff = new double[_order + 1];
            _bCoeff = new double[_order + 1];

            var newA = new double[_order + 1];
            var newB = new double[_order + 1];

            // Find filter poles and zeros
            LocatePolesAndZeros();

            // Compute filter coefficients from pole/zero values
            _aCoeff[0] = 1.0;
            _bCoeff[0] = 1.0;

            for (var i = 1; i <= _order; i++) _aCoeff[i] = _bCoeff[i] = 0.0;

            var k = 0;
            var n = _order;
            var pairs = n / 2;

            if (IsOdd(_order))
            {
                // First subfilter is first order
                _aCoeff[1] = -_z[1];
                _bCoeff[1] = -_real[1];
                k = 1;
            }

            for (var p = 1; p <= pairs; p++)
            {
                var m = 2 * p - 1 + k;
                var alpha1 = -(_z[m] + _z[m + 1]);
                var alpha2 = _z[m] * _z[m + 1];
                var beta1 = -2.0 * _real[m];
                var beta2 = Sqr(_real[m]) + Sqr(_imag[m]);

                newA[1] = _aCoeff[1] + alpha1 * _aCoeff[0];
                newB[1] = _bCoeff[1] + beta1 * _bCoeff[0];

                for (var i = 2; i <= n; i++)
                {
                    newA[i] = _aCoeff[i] + alpha1 * _aCoeff[i - 1] + alpha2 * _aCoeff[i - 2];
                    newB[i] = _bCoeff[i] + beta1 * _bCoeff[i - 1] + beta2 * _bCoeff[i - 2];
                }

                for (var i = 1; i <= n; i++)
                {
                    _aCoeff[i] = newA[i];
                    _bCoeff[i] = newB[i];
                }
            }

            // Ensure the filter is normalized
            Span<float> temp = stackalloc float[1000];
            FilterGain(temp);
        }

        /// <summary>
        /// Resets the filter's history buffers.
        /// </summary>
        public void Reset()
        {
            Array.Clear(_inHistory, 0, _inHistory.Length);
            Array.Clear(_outHistory, 0, _outHistory.Length);
            _histIdx = 0;
        }

        /// <summary>
        /// Applies the filter to the given buffer.
        /// </summary>
        public void FilterBuffer(ReadOnlySpan<float> inBuffer, int inBufferOffset, Span<float> outBuffer,
            int outBufferOffset, int size)
        {
            const double kDenormal = 0.000000000000001;
            var denormal = _invertDenormal ? -kDenormal : kDenormal;
            _invertDenormal = !_invertDenormal;

            for (var sampleIdx = 0; sampleIdx < size; sampleIdx++)
            {
                _inHistory[_histIdx] = inBuffer[inBufferOffset + sampleIdx] + denormal;

                var sum = _aCoeff.Select((t, idx) => t * _inHistory[(_histIdx - idx) & HISTORY_MASK]).Sum();

                for (var idx = 1; idx < _bCoeff.Length; idx++)
                    sum -= _bCoeff[idx] * _outHistory[(_histIdx - idx) & HISTORY_MASK];

                _outHistory[_histIdx] = sum;
                _histIdx = (_histIdx + 1) & HISTORY_MASK;
                outBuffer[outBufferOffset + sampleIdx] = (float) sum;
            }
        }

        /// <summary>
        /// Determines the gain at the given buffer's size number of frequency points.
        /// </summary>
        public void FilterGain(Span<float> gainBuffer)
        {
            // Filter gain at uniform frequency intervals
            int freqPoints = gainBuffer.Length;
            var gMax = -100f;
            var sc = 10 / (float) Math.Log(10);
            var t = Math.PI / (freqPoints - 1);

            for (var i = 0; i < freqPoints; i++)
            {
                var theta = i * t;

                if (i == 0) theta = Math.PI * 0.0001;

                if (i == freqPoints - 1) theta = Math.PI * 0.9999;

                double sac = 0, sas = 0, sbc = 0, sbs = 0;

                for (var k = 0; k <= _order; k++)
                {
                    var c = Math.Cos(k * theta);
                    var s = Math.Sin(k * theta);
                    sac += c * _aCoeff[k];
                    sas += s * _aCoeff[k];
                    sbc += c * _bCoeff[k];
                    sbs += s * _bCoeff[k];
                }

                gainBuffer[i] = sc * (float) Math.Log((Sqr(sac) + Sqr(sas)) / (Sqr(sbc) + Sqr(sbs)));
                gMax = Math.Max(gMax, gainBuffer[i]);
            }

            // Normalize to 0 dB maximum gain
            for (var i = 0; i < freqPoints; i++) gainBuffer[i] -= gMax;

            // Normalize numerator (a) coefficients
            var normFactor = (float) Math.Pow(10.0f, -0.05f * gMax);

            for (var i = 0; i <= _order; i++) _aCoeff[i] *= normFactor;
        }
    }
}