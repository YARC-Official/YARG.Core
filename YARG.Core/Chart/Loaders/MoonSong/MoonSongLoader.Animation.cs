﻿using System.Collections.Generic;
using MoonscraperChartEditor.Song;
using YARG.Core.Chart.Events;
using YARG.Core.Logging;

namespace YARG.Core.Chart
{
    internal partial class MoonSongLoader
    {
        private static readonly Dictionary<string, CharacterState.CharacterStateType> CharacterStateLookup = new()
        {
            { AnimationLookup.ANIMATION_STATE_IDLE,          CharacterState.CharacterStateType.Idle },
            { AnimationLookup.ANIMATION_STATE_IDLE_INTENSE,  CharacterState.CharacterStateType.IdleIntense },
            { AnimationLookup.ANIMATION_STATE_IDLE_REALTIME, CharacterState.CharacterStateType.IdleRealtime },
            { AnimationLookup.ANIMATION_STATE_PLAY,          CharacterState.CharacterStateType.Play },
            { AnimationLookup.ANIMATION_STATE_PLAY_SOLO,     CharacterState.CharacterStateType.PlaySolo },
            { AnimationLookup.ANIMATION_STATE_PLAY_INTENSE,  CharacterState.CharacterStateType.Intense },
            { AnimationLookup.ANIMATION_STATE_PLAY_MELLOW,   CharacterState.CharacterStateType.Mellow },
        };

        private AnimationTrack GetAnimationTrack(Instrument instrument)
        {
            var characterStates = new List<CharacterState>();
            var handMaps = new List<HandMap>();
            var strumMaps = new List<StrumMap>();
            var animationEvents = new List<AnimationEvent>();

            MoonChart chart;

            if (instrument.ToGameMode() == GameMode.Vocals)
            {
                chart = _moonSong.GetChart(MoonSong.MoonInstrument.Vocals, MoonSong.Difficulty.Expert);
            }
            else
            {
                chart = GetMoonChart(instrument, Difficulty.Expert);
            }

            // Process text events
            foreach (var textEvent in chart.events)
            {
                double time = _moonSong.TickToTime(textEvent.tick);
                string eventName = textEvent.text;

                // Character States
                if (CharacterStateLookup.TryGetValue(eventName, out var characterType))
                {
                    characterStates.Add(new CharacterState(characterType, time, textEvent.tick));
                    continue;
                }

                // Hand Maps
                if (AnimationLookup.LeftHandMapLookup.TryGetValue(eventName, out var handMapType))
                {
                    handMaps.Add(new HandMap(handMapType, time, textEvent.tick));
                    continue;
                }

                // Strum Maps
                if (AnimationLookup.RightHandMapLookup.TryGetValue(eventName, out var strumMapType))
                {
                    strumMaps.Add(new StrumMap(strumMapType, time, textEvent.tick));
                }
            }

            // Process animation notes
            foreach (var animNote in chart.animationNotes)
            {
                var lookup = chart.gameMode switch
                {
                    MoonChart.GameMode.Guitar => AnimationLookup.GuitarAnimationLookup,
                    MoonChart.GameMode.Drums  => AnimationLookup.DrumAnimationLookup,
                    _                         => null
                };

                if (lookup != null && lookup.TryGetValue(animNote.text, out var animType))
                {
                    animationEvents.Add(new AnimationEvent(animType,
                        _moonSong.TickToTime(animNote.tick), GetLengthInTime(animNote), animNote.tick,
                        animNote.length));
                }
            }

            return new AnimationTrack(characterStates, handMaps, strumMaps, animationEvents);
        }

        private AnimationTrack GetVocalsAnimationTrack()
        {
            var chart = _moonSong.GetChart(MoonSong.MoonInstrument.Vocals, MoonSong.Difficulty.Expert);

            return new AnimationTrack();
        }
    }
}