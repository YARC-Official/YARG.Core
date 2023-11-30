using System;
using System.IO;
using YARG.Core.Utility;

namespace YARG.Core.Engine
{
    public struct HitWindowSettings : IBinarySerializable
    {
        public double MaxWindow { get; private set; }
        public double MinWindow { get; private set; }

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

            MaxWindow = maxWindow;
            MinWindow = minWindow;
            FrontToBackRatio = frontToBackRatio;
            IsDynamic = isDynamic;

            _minMaxWindowRatio = MinWindow / MaxWindow;
        }

        public double GetFrontEnd(double fullWindow)
        {
            return -(Math.Abs(fullWindow / 2) * FrontToBackRatio);
        }

        public double GetBackEnd(double fullWindow)
        {
            return Math.Abs(fullWindow / 2) * (2 - FrontToBackRatio);
        }

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