using System;

namespace YARG.Core.Chart
{
    public class LipsyncEvent : ChartEvent, ICloneable<LipsyncEvent>
    {
        // TODO: Make this more generic, rather than basing directly on RB
        public enum LipsyncType
        {
            // Actual visemes
            Bump_hi,
            Bump_lo,
            Cage_hi,
            Cage_lo,
            Church_hi,
            Church_lo,
            Earth_hi,
            Earth_lo,
            Eat_hi,
            Eat_lo,
            Fave_hi,
            Fave_lo,
            If_hi,
            If_lo,
            Neutral_hi,
            Neutral_lo,
            New_hi,
            New_lo,
            Oat_hi,
            Oat_lo,
            Ox_hi,
            Ox_lo,
            Roar_hi,
            Roar_lo,
            Size_hi,
            Size_lo,
            Though_hi,
            Though_lo,
            Told_hi,
            Told_lo,
            Wet_hi,
            Wet_lo,
            // Other facial animation stuff
            Blink,
            Brow_aggressive,
            Brow_down,
            Brow_dramatic,
            Brow_openmouthed,
            Brow_pouty,
            Brow_up,
            Squint,
            Wide_eyed,
            exp_spazz_tongueout_side_01,
            exp_spazz_tongueout_front_01,
            exp_spazz_snear_mellow_01,
            exp_spazz_snear_intense_01,
            exp_spazz_eyesclosed_01,
            exp_rocker_teethgrit_pained_01,
            exp_rocker_teethgrit_happy_01,
            exp_rocker_soloface_01,
            exp_rocker_smile_mellow_01,
            exp_rocker_smile_intense_01,
            exp_rocker_slackjawed_01,
            exp_rocker_shout_quick_01,
            exp_rocker_shout_eyesopen_01,
            exp_rocker_shout_eyesclosed_01,
            exp_rocker_bassface_cool_01,
            exp_rocker_bassface_aggressive_01,
            exp_dramatic_pouty_01,
            exp_dramatic_mouthopen_01,
            exp_dramatic_happy_eyesopen_01,
            exp_dramatic_happy_eyesclosed_01,
            exp_banger_teethgrit_01,
            exp_banger_slackjawed_01,
            exp_banger_roar_01,
            exp_banger_oohface_01
        }

        public LipsyncType Type { get; }
        public float Value { get; }

        public LipsyncEvent(LipsyncType type, float value, double time, uint tick) : base(time, 0, tick, 0)
        {
            Type = type;
            Value = value;
        }

        public LipsyncEvent(LipsyncEvent other) : base(other)
        {
            Type = other.Type;
        }

        public LipsyncEvent Clone()
        {
            return new LipsyncEvent(this);
        }
    }
}