using System;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace YARG.Core.Game
{
    public abstract class BasePreset
    {
        public string Name;
        public Guid Id;

        /// <summary>
        /// Determines whether or not the preset should be modifiable in the settings.
        /// </summary>
        [JsonIgnore]
        public bool DefaultPreset;

        protected BasePreset(string name, bool defaultPreset)
        {
            Name = name;
            DefaultPreset = defaultPreset;

            if (defaultPreset)
            {
                // Make sure default presets are consistent based on names.
                // This ensures that their GUIDs will be consistent (because they are constructed in code every time).
                using var md5 = MD5.Create();
                byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(name));
                Id = new Guid(hash);
            }
            else
            {
                Id = Guid.NewGuid();
            }
        }

        public abstract BasePreset CopyWithNewName(string name);
    }
}