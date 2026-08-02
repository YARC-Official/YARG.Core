using System;
using System.Collections.Generic;
using System.Text;

namespace YARG.Core.Game
{
    public abstract class Device
    {
        private readonly int VERSION = 0;

        public int Version;

        public Guid Id;
        public string Name;

        public long InputCalibrationMilliseconds;
        public double InputCalibrationSeconds
        {
            get => InputCalibrationMilliseconds / 1000.0;
            set => InputCalibrationMilliseconds = (long) (value * 1000);
        }
    }

    public class Controller : Device
    {
        public string Layout;
        public string Hash;
    }

    public class Microphone: Device
    {

    }
}
