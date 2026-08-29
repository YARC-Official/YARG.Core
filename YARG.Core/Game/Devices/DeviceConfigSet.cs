using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace YARG.Core.Game
{
    public abstract class DeviceConfigSet
    {
        private readonly int VERSION = 0;

        public int Version;

        public Guid Id;
        public string Name;

        /// <summary>
        /// The last time this profile was used.
        /// </summary>
        public DateTime LastUsed;

        public long InputCalibrationMilliseconds;
        public double InputCalibrationSeconds
        {
            get => InputCalibrationMilliseconds / 1000.0;
            set => InputCalibrationMilliseconds = (long) (value * 1000);
        }

        public void ClaimDevice()
        {
            LastUsed = DateTime.Now;
        }

        // For replay serialization
        public void Serialize(BinaryWriter writer)
        {
            // No properties currently matter for replay serialization. If we start allowing devices to store
            // default color profiles and/or highway orderings, those will be relevant to replays and should
            // be serialized.
        }
    }

    public class ControllerConfigSet : DeviceConfigSet
    {
        public string Layout;
        public string Hash;
    }

    public class MicrophoneConfigSet : DeviceConfigSet
    {

    }
}
