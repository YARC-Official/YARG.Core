using System;
using System.Collections.Generic;
using System.Linq;
using YARG.Core.Game;

namespace YARG.Core.Chart
{
    public static class PartyVocalsChartSelection
    {
        public static Instrument Resolve(SongChart chart, PartyVocalsChartPreference preference)
        {
            var preferred = preference switch
            {
                PartyVocalsChartPreference.Harmony => Instrument.Harmony,
                PartyVocalsChartPreference.Solo => Instrument.Vocals,
                _ => throw new ArgumentOutOfRangeException(nameof(preference))
            };
            var fallback = preferred == Instrument.Harmony ? Instrument.Vocals : Instrument.Harmony;
            if (HasContent(chart.GetVocalsTrack(preferred))) return preferred;
            if (HasContent(chart.GetVocalsTrack(fallback))) return fallback;
            throw new InvalidOperationException("No playable vocal chart is available.");
        }

        public static bool HasContent(VocalsTrack track) =>
            track.Parts.Any(part => part.NotePhrases.Any(phrase => !phrase.IsEmpty));

        public static List<VocalsPart> CreateModifiedParts(SongChart chart, Instrument source, YargProfile profile)
        {
            if (source != Instrument.Vocals && source != Instrument.Harmony)
                throw new ArgumentOutOfRangeException(nameof(source));
            var parts = new List<VocalsPart>();
            foreach (var part in chart.GetVocalsTrack(source).Parts)
            {
                var clone = part.Clone();
                profile.ApplyVocalModifiers(clone);
                parts.Add(clone);
            }
            if (parts.Count == 0) throw new InvalidOperationException("The selected vocal chart has no parts.");
            return parts;
        }
    }
}