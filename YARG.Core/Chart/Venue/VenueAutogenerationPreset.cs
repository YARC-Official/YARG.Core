using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using MoonscraperChartEditor.Song;
using MoonscraperChartEditor.Song.IO;

using MoonVenueEvent = MoonscraperChartEditor.Song.VenueEvent;

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
                if (!defaultSectionRead)
                {
                    YargTrace.DebugWarning("Missing default section in preset: " + path);
                }
            }
            catch (Exception ex)
            {
                YargTrace.LogException(ex, $"Error while loading auto-gen preset {path}");
            }
        }

        public void GenerateLightingEvents(ref SongChart chart) {
            uint lastTick = chart.GetLastTick();
            uint resolution = chart.Resolution;
            LightingType latestLighting = LightingType.Intro;
            PostProcessingType latestPostProc = PostProcessingType.Default;
            // Add initial state
            chart.VenueTrack.Lighting.Add(new LightingEvent(latestLighting, 0, 0));
            chart.VenueTrack.PostProcessing.Add(new PostProcessingEvent(latestPostProc, 0, 0));
            foreach (Section section in chart.Sections)
            {
                // Find which section preset to use...
                AutogenerationSectionPreset sectionPreset = DefaultSectionPreset;
                bool matched = false;
                foreach (AutogenerationSectionPreset preset in SectionPresets)
                {
                    var nameToMatch = section.Name.ToLower().Trim().Replace("-","").Replace(" ","_");
                    foreach (string practiceSecion in preset.PracticeSections)
                    {
                        var regexString = "^" + Regex.Escape(practiceSecion).Replace("\\*", ".*") + "$"; 
                        if (Regex.IsMatch(nameToMatch, regexString))
                        {
                            sectionPreset = preset;
                            matched = true;
                            YargTrace.DebugInfo("Section " + section.Name + " matched practice section " + practiceSecion);
                            break;
                        }
                    }
                    if (matched)
                    {
                        break;
                    }
                }
                if (!matched)
                {
                    YargTrace.DebugInfo("No match found for section " + section.Name + "; using default autogen section");
                }
                // Actually generate lighting
                LightingType currentLighting = latestLighting;
                foreach (LightingType lighting in sectionPreset.AllowedLightPresets)
                {
                    if (lighting != latestLighting)
                    {
                        currentLighting = lighting;
                        break;
                    }
                }
                if (currentLighting != latestLighting) // Only generate new events if lighting's changed
                {
                    if (sectionPreset.LightPresetBlendIn > 0)
                    {
                        uint blendTick = section.Tick - (sectionPreset.LightPresetBlendIn * resolution);
                        if (blendTick > 0)
                        {
                            chart.VenueTrack.Lighting.Add(new LightingEvent(latestLighting, chart.SyncTrack.TickToTime(blendTick), blendTick));
                        }
                    }
                    chart.VenueTrack.Lighting.Add(new LightingEvent(currentLighting, section.Time, section.Tick));
                    latestLighting = currentLighting;
                }
                else if (LightingIsManual(currentLighting))
                {
                    chart.VenueTrack.Lighting.Add(new LightingEvent(LightingType.Keyframe_Next, section.Time, section.Tick));
                }
                // Generate next keyframes
                if (LightingIsManual(currentLighting))
                {
                    uint nextTick = section.Tick + (resolution * sectionPreset.KeyframeRate);
                    while (nextTick < lastTick && nextTick < section.TickEnd)
                    {
                        chart.VenueTrack.Lighting.Add(new LightingEvent(LightingType.Keyframe_Next, chart.SyncTrack.TickToTime(nextTick), nextTick));
                        nextTick += (resolution * sectionPreset.KeyframeRate);
                    }
                }
                // Generate post-procs
                PostProcessingType currentPostProc = latestPostProc;
                foreach (PostProcessingType postProc in sectionPreset.AllowedPostProcs)
                {
                    if (postProc != latestPostProc)
                    {
                        currentPostProc = postProc;
                        break;
                    }
                }
                if (currentPostProc != latestPostProc) // Only generate new events if post-proc's changed
                {
                    if (sectionPreset.PostProcBlendIn > 0)
                    {
                        uint blendTick = section.Tick - (sectionPreset.PostProcBlendIn * resolution);
                        if (blendTick > 0)
                        {
                            chart.VenueTrack.PostProcessing.Add(new PostProcessingEvent(latestPostProc, chart.SyncTrack.TickToTime(blendTick), blendTick));
                        }
                    }
                    chart.VenueTrack.PostProcessing.Add(new PostProcessingEvent(currentPostProc, section.Time, section.Tick));
                    latestPostProc = currentPostProc;
                }
            }
            // Reorder lighting track (Next keyframes and blend-in events might be unordered)
            chart.VenueTrack.Lighting.Sort((x,y) => x.Tick.CompareTo(y.Tick));
        }

        public void GenerateCameraCutEvents(ref SongChart chart) {
            // TODO: camera cut generator function
        }

        private bool LightingIsManual(LightingType lighting) {
            return lighting == LightingType.Default ||
                   lighting == LightingType.Dischord ||
                   lighting == LightingType.Chorus ||
                   lighting == LightingType.Cool_Manual ||
                   lighting == LightingType.Stomp ||
                   lighting == LightingType.Verse ||
                   lighting == LightingType.Warm_Manual;
        }

        private AutogenerationSectionPreset JObjectToSectionPreset(JObject o) {
            AutogenerationSectionPreset sectionPreset = new AutogenerationSectionPreset();
            foreach (var parameter in o)
            {
                switch (parameter.Key.ToLower().Trim())
                {
                    case "practice_sections":
                        List<string> practiceSections = new List<string>();
                        foreach (string section in (JArray)parameter.Value) {
                            practiceSections.Add(section);
                        }
                        sectionPreset.PracticeSections = practiceSections;
                        break;
                    case "allowed_lightpresets":
                        List<LightingType> allowedLightPresets = new List<LightingType>();
                        foreach (string key in (JArray)parameter.Value) {
                            var keyTrim = key.Trim();
                            if (MidIOHelper.VENUE_LIGHTING_CONVERSION_LOOKUP.TryGetValue(keyTrim, out var eventData))
                            {
                                allowedLightPresets.Add(MoonSongLoader.LightingLookup[eventData]);
                            }
                            else
                            {
                                YargTrace.DebugWarning("Invalid light preset: " + key);
                            }
                        }
                        sectionPreset.AllowedLightPresets = allowedLightPresets;
                        break;
                    case "allowed_postprocs":
                        List<PostProcessingType> allowedPostProcs = new List<PostProcessingType>();
                        foreach (string key in (JArray)parameter.Value) {
                            var keyTrim = key.Trim();
                            if (MidIOHelper.VENUE_TEXT_CONVERSION_LOOKUP.TryGetValue(keyTrim, out var eventData) && eventData.type == MoonVenueEvent.Type.PostProcessing)
                            {
                                allowedPostProcs.Add(MoonSongLoader.PostProcessLookup[eventData.text]);
                            }
                            else
                            {
                                YargTrace.DebugWarning("Invalid post-proc: " + key);
                            }
                        }
                        sectionPreset.AllowedPostProcs = allowedPostProcs;
                        break;
                    case "keyframe_rate":
                        sectionPreset.KeyframeRate = (uint)parameter.Value;
                        break;
                    case "lightpreset_blendin":
                        sectionPreset.LightPresetBlendIn = (uint)parameter.Value;
                        break;
                    case "postproc_blendin":
                        sectionPreset.PostProcBlendIn = (uint)parameter.Value;
                        break;
                    case "dircut_at_start":
                        // TODO: add when we have characters / directed camera cuts
                        break;
                    case "bonusfx_at_start":
                        sectionPreset.BonusFxAtStart = (bool)parameter.Value;
                        break;
                    case "camera_pacing":
                        sectionPreset.CameraPacingOverride = StringToCameraPacing((string)parameter.Value);
                        break;
                    default:
                        YargTrace.DebugWarning("Unknown section preset parameter: " + parameter.Key);
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
        public uint KeyframeRate;
        public uint LightPresetBlendIn;
        public uint PostProcBlendIn;
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