using System.Collections.Generic;
using YARG.Core.Chart.Events;
using CharacterStateType = YARG.Core.Chart.Events.CharacterState.CharacterStateType;
using HandMapType = YARG.Core.Chart.Events.HandMap.HandMapType;
using StrumMapType = YARG.Core.Chart.Events.StrumMap.StrumMapType;

namespace YARG.Core.Chart
{
    public class AnimationLookup
    {
        public enum Type
        {
            LeftHand,
            Drum,
            CharacterState,
            HandMap,
            StrumMap
        }

        // Animation states
        public const string ANIMATION_STATE_IDLE          = "idle";
        public const string ANIMATION_STATE_IDLE_REALTIME = "idle_realtime";
        public const string ANIMATION_STATE_IDLE_INTENSE  = "idle_intense";
        public const string ANIMATION_STATE_PLAY          = "play";
        public const string ANIMATION_STATE_PLAY_SOLO     = "play_solo";
        public const string ANIMATION_STATE_PLAY_INTENSE  = "intense";
        public const string ANIMATION_STATE_PLAY_MELLOW   = "mellow";

        // Hand maps
        public const string HAND_MAP_DEFAULT = "HandMap_Default";
        public const string HAND_MAP_NOCHORDS = "HandMap_NoChords";
        public const string HAND_MAP_ALLCHORDS = "HandMap_AllChords";
        public const string HAND_MAP_ALLBEND = "HandMap_AllBend";
        public const string HAND_MAP_SOLO = "HandMap_Solo";
        public const string HAND_MAP_DROPD = "HandMap_DropD";
        public const string HAND_MAP_DROPD2 = "HandMap_DropD2";
        public const string HAND_MAP_CHORD_C = "HandMap_Chord_C";
        public const string HAND_MAP_CHORD_D = "HandMap_Chord_D";
        public const string HAND_MAP_CHORD_A = "HandMap_Chord_A";

        // Strum maps
        public const string STRUM_MAP_DEFAULT = "StrumMap_Default";
        public const string STRUM_MAP_PICK = "StrumMap_Pick";
        public const string STRUM_MAP_SLAP = "StrumMap_SlapBass";

        // Left hand animations
        public const string LH_POSITION_1 = "LeftHandPosition1";
        public const string LH_POSITION_2 = "LeftHandPosition2";
        public const string LH_POSITION_3 = "LeftHandPosition3";
        public const string LH_POSITION_4 = "LeftHandPosition4";
        public const string LH_POSITION_5 = "LeftHandPosition5";
        public const string LH_POSITION_6 = "LeftHandPosition6";
        public const string LH_POSITION_7 = "LeftHandPosition7";
        public const string LH_POSITION_8 = "LeftHandPosition8";
        public const string LH_POSITION_9 = "LeftHandPosition9";
        public const string LH_POSITION_10 = "LeftHandPosition10";
        public const string LH_POSITION_11 = "LeftHandPosition11";
        public const string LH_POSITION_12 = "LeftHandPosition12";
        public const string LH_POSITION_13 = "LeftHandPosition13";
        public const string LH_POSITION_14 = "LeftHandPosition14";
        public const string LH_POSITION_15 = "LeftHandPosition15";
        public const string LH_POSITION_16 = "LeftHandPosition16";
        public const string LH_POSITION_17 = "LeftHandPosition17";
        public const string LH_POSITION_18 = "LeftHandPosition18";
        public const string LH_POSITION_19 = "LeftHandPosition19";
        public const string LH_POSITION_20 = "LeftHandPosition20";

        // Drum animations
        public const string DRUM_KICK           = "Kick";
        public const string DRUM_HIHAT_OPEN     = "HiHatOpen";
        public const string DRUM_SNARE_LH_HARD  = "SnareLHHard";
        public const string DRUM_SNARE_RH_HARD  = "SnareRHHard";
        public const string DRUM_SNARE_LH_SOFT  = "SnareLHSoft";
        public const string DRUM_SNARE_RH_SOFT  = "SnareRHSoft";
        public const string DRUM_HIHAT_LH       = "HiHatLeft";
        public const string DRUM_HIHAT_RH       = "HiHatRight";
        public const string DRUM_PERCUSSION_RH  = "PercussionRight";
        public const string DRUM_CRASH1_LH_HARD = "Crash1LeftHard";
        public const string DRUM_CRASH1_LH_SOFT = "Crash1LeftSoft";
        public const string DRUM_CRASH1_RH_HARD = "Crash1RightHard";
        public const string DRUM_CRASH1_RH_SOFT = "Crash1RightSoft";
        public const string DRUM_CRASH2_RH_HARD = "Crash2RightHard";
        public const string DRUM_CRASH2_RH_SOFT = "Crash2RightSoft";
        public const string DRUM_CRASH2_LH_HARD = "Crash2LeftHard";
        public const string DRUM_CRASH2_LH_SOFT = "Crash2LeftSoft";
        public const string DRUM_CRASH1_CHOKE   = "Crash1Choke";
        public const string DRUM_CRASH2_CHOKE   = "Crash2Choke";
        public const string DRUM_RIDE_LH        = "RideLeft";
        public const string DRUM_RIDE_RH        = "RideRight";
        public const string DRUM_TOM1_LH        = "Tom1Left";
        public const string DRUM_TOM1_RH        = "Tom1Right";
        public const string DRUM_TOM2_LH        = "Tom2Left";
        public const string DRUM_TOM2_RH        = "Tom2Right";
        public const string DRUM_FLOOR_TOM_LH   = "FloorTomLeft";
        public const string DRUM_FLOOR_TOM_RH   = "FloorTomRight";

        public static readonly Dictionary<string, string> CHARACTER_STATE_LOOKUP = new()
        {
            { "idle", ANIMATION_STATE_IDLE },
            { "idle_realtime", ANIMATION_STATE_IDLE_REALTIME },
            { "idle_intense", ANIMATION_STATE_IDLE_INTENSE },
            { "play", ANIMATION_STATE_PLAY },
            { "play_solo", ANIMATION_STATE_PLAY_SOLO },
            { "intense", ANIMATION_STATE_PLAY_INTENSE },
            { "mellow", ANIMATION_STATE_PLAY_MELLOW },
        };

        public static readonly Dictionary<string, string> LEFT_HAND_MAP_LOOKUP = new()
        {
            { "HandMap_Default", HAND_MAP_DEFAULT },
            { "HandMap_NoChords", HAND_MAP_NOCHORDS },
            { "HandMap_AllChords", HAND_MAP_ALLCHORDS },
            { "HandMap_AllBend", HAND_MAP_ALLBEND },
            { "HandMap_Solo", HAND_MAP_SOLO },
            { "HandMap_DropD", HAND_MAP_DROPD },
            { "HandMap_DropD2", HAND_MAP_DROPD2 },
            { "HandMap_Chord_C", HAND_MAP_CHORD_C },
            { "HandMap_Chord_D", HAND_MAP_CHORD_D },
            { "HandMap_Chord_A", HAND_MAP_CHORD_A },
        };

        public static readonly Dictionary<string, string> RIGHT_HAND_MAP_LOOKUP = new()
        {
            { "StrumMap_Default", STRUM_MAP_DEFAULT },
            { "StrumMap_Pick", STRUM_MAP_PICK },
            { "StrumMap_SlapBass", STRUM_MAP_SLAP },
        };

        public static readonly Dictionary<string, HandMapType> LeftHandMapLookup = new()
        {
            { HAND_MAP_DEFAULT, HandMapType.Default },
            { HAND_MAP_NOCHORDS, HandMapType.NoChords },
            { HAND_MAP_ALLCHORDS, HandMapType.AllChords },
            { HAND_MAP_ALLBEND, HandMapType.AllBend },
            { HAND_MAP_SOLO, HandMapType.Solo },
            { HAND_MAP_DROPD, HandMapType.DropD },
            { HAND_MAP_DROPD2, HandMapType.DropD2 },
            { HAND_MAP_CHORD_C, HandMapType.ChordC },
            { HAND_MAP_CHORD_D, HandMapType.ChordD },
            { HAND_MAP_CHORD_A, HandMapType.ChordA },
        };

        public static readonly Dictionary<string, StrumMapType> RightHandMapLookup = new()
        {
            { STRUM_MAP_DEFAULT, StrumMapType.Default },
            { STRUM_MAP_PICK, StrumMapType.Pick },
            { STRUM_MAP_SLAP, StrumMapType.SlapBass },
        };
    }
}