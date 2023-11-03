using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace YARG.Core.Chart
{
    /// <summary>
    /// A venue auto-generation preset; to be used incase a given chart does not include manually charted lighting/camera cues.
    /// </summary>
    public class VenueAutogenerationPreset
    {
        public CameraPacing CameraPacing;
        public AutogenerationSectionPreset DefaultSectionPreset;
        public List<AutogenerationSectionPreset> SectionPresets;
        
        public VenueAutogenerationPreset(string path)
        {
            CameraPacing = CameraPacing.Medium;
            DefaultSectionPreset = new AutogenerationSectionPreset();
            DefaultSectionPreset.AllowedLightPresets.Add(LightingType.Default);
            DefaultSectionPreset.AllowedPostProcs.Add(PostProcessingType.Default);
            if (File.Exists(path))
            {
                ReadPresetFromFile(path);
            }
            else
            {
                YargTrace.DebugWarning("Auto-generation preset file not found: " + path);
            }
        }

        private void ReadPresetFromFile(string path)
        {
            // TODO: read preset from file (JSON) fuction
        }

        public void GenerateLightingEvents(ref SongChart chart) {
            // TODO: lighting generator function
        }

        public void GenerateCameraCutEvents(ref SongChart chart) {
            // TODO: camera cut generator function
        }
        
       
        
    }

    public class AutogenerationSectionPreset
    {
        public string SectionName; // probably useless
        public List<string> PracticeSections; // i.e. "*verse*" which applies to "Verse 1", "Verse 2", etc.
        public List<LightingType> AllowedLightPresets;
        public List<PostProcessingType> AllowedPostProcs;
        public int KeyframeRate;
        public int LightPresetBlendIn;
        public int PostProcBlendIn;
        // public DirectedCameraCutType DirectedCutAtStart; // TODO: add when we have characters / directed camera cuts
        public bool BonusFxAtStart;
        public CameraPacing? CameraPacingOverride;

        public AutogenerationSectionPreset()
        {
            // Default values
            SectionName = "";
            PracticeSections = new List<string>();
            AllowedLightPresets = new List<LightingType>();
            AllowedPostProcs = new List<PostProcessingType>();
            KeyframeRate = 2;
            LightPresetBlendIn = 0;
            PostProcBlendIn = 0;
            BonusFxAtStart = false;
            CameraPacingOverride = null;
        }
    }

    /// <summary>
    /// Possible camera pacing values.
    /// </summary>
    public enum CameraPacing
    {
        Minimal,
        Slow,
        Medium,
        Fast,
        Crazy
    }
}