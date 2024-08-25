using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using YARG.Core.Extensions;
using YARG.Core.Game;
using YARG.Core.Utility;

namespace YARG.Core.Replays
{
    /// <summary>
    /// A container that stores the presets used in a replay, and allows for easy access of
    /// said presets. The container has separate versioning from the replay itself.
    /// </summary>
    public class ReplayPresetContainer
    {
        private static readonly JsonSerializerSettings _jsonSettings = new()
        {
            Converters =
            {
                new JsonColorConverter()
            }
        };

        private const int CONTAINER_VERSION = 0;

        private readonly Dictionary<Guid, ColorProfile> _colorProfiles = new();
        private readonly Dictionary<Guid, CameraPreset> _cameraPresets = new();

        /// <returns>
        /// The color profile if it's in this container, otherwise, <c>null</c>.
        /// </returns>
        public ColorProfile? GetColorProfile(Guid guid)
        {
            return _colorProfiles.GetValueOrDefault(guid);
        }

        /// <summary>
        /// Stores the specified color profile into this container. If the color profile
        /// is a default one, nothing is stored.
        /// </summary>
        public void StoreColorProfile(ColorProfile colorProfile)
        {
            if (colorProfile.DefaultPreset)
            {
                return;
            }

            _colorProfiles[colorProfile.Id] = colorProfile;
        }

        /// <returns>
        /// The camera preset if it's in this container, otherwise, <c>null</c>.
        /// </returns>
        public CameraPreset? GetCameraPreset(Guid guid)
        {
            return _cameraPresets.GetValueOrDefault(guid);
        }

        /// <summary>
        /// Stores the specified camera preset into this container. If the camera preset
        /// is a default one, nothing is stored.
        /// </summary>
        public void StoreCameraPreset(CameraPreset cameraPreset)
        {
            if (cameraPreset.DefaultPreset)
            {
                return;
            }

            _cameraPresets[cameraPreset.Id] = cameraPreset;
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(CONTAINER_VERSION);

            SerializeDict(writer, _colorProfiles);
            SerializeDict(writer, _cameraPresets);
        }

        public void Deserialize(UnmanagedMemoryStream stream, int version = 0)
        {
            // This container has separate versioning
            version = stream.Read<int>(Endianness.Little);

            DeserializeDict(stream, _colorProfiles);
            DeserializeDict(stream, _cameraPresets);
        }

        private static void SerializeDict<T>(BinaryWriter writer, Dictionary<Guid, T> dict)
        {
            writer.Write(dict.Count);
            foreach (var (key, value) in dict)
            {
                // Write key
                writer.Write(key);

                // Write preset
                var json = JsonConvert.SerializeObject(value, _jsonSettings);
                writer.Write(json);
            }
        }

        private static void DeserializeDict<T>(UnmanagedMemoryStream stream, Dictionary<Guid, T> dict)
        {
            dict.Clear();
            int len = stream.Read<int>(Endianness.Little);
            for (int i = 0; i < len; i++)
            {
                // Read key
                var guid = stream.ReadGuid();

                // Read preset
                var json = stream.ReadString();
                var preset = JsonConvert.DeserializeObject<T>(json, _jsonSettings)!;

                dict.Add(guid, preset);
            }
        }
    }
}