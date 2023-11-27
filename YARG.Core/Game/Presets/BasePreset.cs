using System;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace YARG.Core.Game
{
    public abstract class BasePreset
    {
        public string Name;
        public Guid   Id;

        /// <summary>
        /// The type of the preset in string form. This is only
        /// used for checking the type when importing a preset.
        /// </summary>
        public string? Type;
        
        /// <summary>
        /// Determines whether or not the preset should be modifiable in the settings.
        /// </summary>
        [JsonIgnore]
        public bool DefaultPreset;

        protected BasePreset(string name, bool defaultPreset)
        {
            Name = name;
            DefaultPreset = defaultPreset;

            Id = defaultPreset 
                ? GetGuidForBasePreset(name) 
                : Guid.NewGuid();
        }

        public abstract BasePreset CopyWithNewName(string name);

        public static Guid GetGuidForBasePreset(string name)
        {
            // Make sure default presets are consistent based on names.
            // This ensures that their GUIDs will be consistent (because they are constructed in code every time).
            using var md5 = MD5.Create();
            byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(name));
            return new Guid(hash);
        }
    }
}