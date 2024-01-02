using System;
using System.IO;
using YARG.Core.Utility;

namespace YARG.Core.Engine
{
    public struct HitWindowSettings : IBinarySerializable
    {
        /// <summary>
        /// The scale factor of the hit window. This should be used to scale the window
        /// up/down during speed ups and slow downs.
        /// </summary>
        /// <remarks>
        /// This value is <b>NOT</b> serialized as it should be set when first creating the
        /// engine based on the song speed.
        /// </remarks>
        public double Scale { get; set; }

        /// <summary>
        /// The maximum window size. If the hit window is not dynamic, this value will be used.
        /// </summary>
        public double MaxWindow { get; private set; }
        /// <summary>
        /// The minimum window size. This value will only be used if the window is dynamic.
        /// </summary>
        public double MinWindow { get; private set; }

        /// <summary>
        /// Whether or not the hit window size can change over time.
        /// This is usually done by looking at the time in between notes.
        /// </summary>
        public bool IsDynamic { get; private set; }

        /// <summary>
        /// The front to back ratio of the hit window.
        /// </summary>
        public double FrontToBackRatio { get; private set; }

        private double _minMaxWindowRatio;

        public HitWindowSettings(double maxWindow, double minWindow, double frontToBackRatio, bool isDynamic)
        {
            // Swap max and min if necessary to ensure that max is always larger than min
            if (maxWindow < minWindow)
            {
                (maxWindow, minWindow) = (minWindow, maxWindow);
            }

            Scale = 1.0;
            MaxWindow = maxWindow;
            MinWindow = minWindow;
            FrontToBackRatio = frontToBackRatio;
            IsDynamic = isDynamic;

            _minMaxWindowRatio = MinWindow / MaxWindow;
        }

        /// <summary>
        /// Calculates the size of the front end of the hit window.
        /// The <see cref="Scale"/> is taken into account.
        /// </summary>
        /// <param name="fullWindow">
        /// The full hit window size. This should be passed in from <see cref="CalculateHitWindow"/>.
        /// </param>
        public double GetFrontEnd(double fullWindow)
        {
            return -(Math.Abs(fullWindow / 2) * FrontToBackRatio) * Scale;
        }

        /// <summary>
        /// Calculates the size of the back end of the hit window.
        /// The <see cref="Scale"/> is taken into account.
        /// </summary>
        /// <param name="fullWindow">
        /// The full hit window size. This should be passed in from <see cref="CalculateHitWindow"/>.
        /// </param>
        public double GetBackEnd(double fullWindow)
        {
            return Math.Abs(fullWindow / 2) * (2 - FrontToBackRatio) * Scale;
        }

        /// <summary>
        /// This method should be used to determine the full hit window size.
        /// This value can then be passed into the <see cref="GetFrontEnd"/>
        /// and <see cref="GetBackEnd"/> methods.
        /// </summary>
        /// <param name="averageTimeDistance">
        /// The average time distance between the notes at this time.
        /// </param>
        /// <returns>
        /// The size of the full hit window.
        /// </returns>
        public double CalculateHitWindow(double averageTimeDistance)
        {
            if (!IsDynamic)
            {
                return MaxWindow;
            }

            return Third_Yarg_Impl(averageTimeDistance);
        }

        private double Original_Yarg_Impl(double averageTimeDistance)
        {
            averageTimeDistance *= 1000;

            double sqrt = Math.Sqrt(averageTimeDistance + _minMaxWindowRatio);
            double tenth = 0.1 * averageTimeDistance;
            double realSize = tenth * sqrt + MinWindow * 1000;

            realSize /= 1000;

            return Math.Clamp(realSize, MinWindow, MaxWindow);
        }

        private double Second_Yarg_Impl(double averageTimeDistance)
        {
            averageTimeDistance *= 1000;

            double minOverFive = MinWindow / 5 * 1000;

            double sqrt = minOverFive * Math.Sqrt(averageTimeDistance * _minMaxWindowRatio);
            double eighthAverage = 0.125 * averageTimeDistance;
            double realSize = eighthAverage + sqrt + MinWindow * 1000;

            realSize /= 1000;

            return Math.Clamp(realSize, MinWindow, MaxWindow);
        }

        private double Third_Yarg_Impl(double averageTimeDistance)
        {
            averageTimeDistance *= 1000;

            double realSize = Curve(Math.Sqrt(MinWindow * 1000 / 40) * averageTimeDistance) + MinWindow * 1000;

            realSize /= 1000;

            return Math.Clamp(realSize, MinWindow, MaxWindow);

            static double Curve(double x)
            {
                return 0.2 * x + Math.Sqrt(17 * x);
            }
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(MaxWindow);
            writer.Write(MinWindow);
            writer.Write(IsDynamic);
            writer.Write(FrontToBackRatio);
        }

        public void Deserialize(BinaryReader reader, int version = 0)
        {
            MaxWindow = reader.ReadDouble();
            MinWindow = reader.ReadDouble();
            IsDynamic = reader.ReadBoolean();
            FrontToBackRatio = reader.ReadDouble();

            _minMaxWindowRatio = MinWindow / MaxWindow;
        }
    }
}