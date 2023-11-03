using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
            SectionPresets = new List<AutogenerationSectionPreset>();
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
            // TODO: ACTUALLY TEST IT!! GOTTA SLEEP
            try
            {
                JObject o = JObject.Parse(File.ReadAllText(path));
                string cameraPacing = (string)o.SelectToken("camera_pacing");
                CameraPacing = StringToCameraPacing(cameraPacing);
                bool defaultSectionRead = false;

                foreach (var sectionPreset in (JObject)o.SelectToken("section_presets")) {
                    AutogenerationSectionPreset value = JObjectToSectionPreset((JObject)sectionPreset.Value);
                    value.SectionName = sectionPreset.Key;
                    if (sectionPreset.Key.ToLower().Trim() == "default")
                    {
                        DefaultSectionPreset = value;
                        if (defaultSectionRead)
                        {
                            YargTrace.DebugWarning("Multiple default sections found in preset: " + path);
                        }
                        defaultSectionRead = true;
                    }
                    else
                    {
                        SectionPresets.Add(value);
                    }
                }
            }
            catch (Exception ex)
            {
                YargTrace.LogException(ex, $"Error while loading auto-gen preset {path}");
            }
        }

        public void GenerateLightingEvents(ref SongChart chart) {
            // TODO: lighting generator function
        }

        public void GenerateCameraCutEvents(ref SongChart chart) {
            // TODO: camera cut generator function
        }

        private AutogenerationSectionPreset JObjectToSectionPreset(JObject o) {
            AutogenerationSectionPreset sectionPreset = new AutogenerationSectionPreset();
            foreach (var parameter in o)
            {
                switch (parameter.Key.ToLower().Trim())
                {
                    case "allowed_lightpresets":
                        List<LightingType> allowedLightPresets = new List<LightingType>();
                        foreach (string key in (JArray)parameter.Value) {
                            key = key.Trim();
                            if (VENUE_LIGHTING_CONVERSION_LOOKUP.TryGetValue(key, out eventData))
                            {
                                allowedLightPresets.Add(eventData);
                            }
                            else
                            {
                                YargTrace.DebugWarning("Invalid light preset: " + key);
                            }
                        }
                        sectionPreset.AllowedLightPresets = allowedLightPresets;
                        break;
                    case "allowed_postprocs":
                        List<LightingType> allowedPostProcs = new List<LightingType>();
                        foreach (string key in (JArray)parameter.Value) {
                            key = key.Trim();
                            if (VENUE_TEXT_CONVERSION_LOOKUP.TryGetValue(key, out eventData) && eventData.type == VenueEvent.Type.PostProcessing)
                            {
                                allowedPostProcs.Add(eventData.text);
                            }
                            else
                            {
                                YargTrace.DebugWarning("Invalid post-proc: " + key);
                            }
                        }
                        sectionPreset.AllowedPostProcs = allowedPostProcs;
                        break;
                    case "keyframe_rate":
                        sectionPreset.KeyframeRate = (int)parameter.Value;
                        break;
                    case "lightpreset_blendin":
                        sectionPreset.LightPresetBlendIn = (int)parameter.Value;
                        break;
                    case "postproc_blendin":
                        sectionPreset.PostProcBlendIn = (int)parameter.Value;
                        break;
                    /*case "dircut_at_start":
                        // TODO: add when we have characters / directed camera cuts
                        break;*/
                    case "bonusfx_at_start":
                        sectionPreset.BonusFxAtStart = (bool)parameter.Value;
                        break;
                    case "camera_pacing":
                        sectionPreset.CameraPacing = StringToCameraPacing((string)parameter.Value);
                        break;
                }
            }
            return sectionPreset;
        }
        
        private CameraPacing StringToCameraPacing(string cameraPacing)
        {
            switch (cameraPacing.ToLower().Trim()) {
                case "minimal": 
                    return CameraPacing.Minimal;
                case "slow":
                    return CameraPacing.Slow;
                case "medium":
                    return CameraPacing.Medium;
                case "fast":
                    return CameraPacing.Fast;
                case "crazy":
                    return CameraPacing.Crazy;
                default:
                    YargTrace.DebugWarning("Invalid camera pacing in auto-gen preset: " + cameraPacing);
                    return CameraPacing.Medium;
            }
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