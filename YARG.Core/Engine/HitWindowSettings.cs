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

            averageTimeDistance *= 1000;

            double sqrt = Math.Sqrt(averageTimeDistance + _minMaxWindowRatio);
            double eighth = 0.125 * averageTimeDistance;
            double realSize = eighth * sqrt + MinWindow * 1000;

            realSize /= 1000;

            return Math.Max(Math.Min(realSize, MaxWindow), MinWindow);
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