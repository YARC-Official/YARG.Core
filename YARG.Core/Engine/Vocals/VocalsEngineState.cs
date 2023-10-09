﻿namespace YARG.Core.Engine.Vocals
{
    public class VocalsEngineState : BaseEngineState
    {
        /// <summary>
        /// The float value for the last pitch sang (as a MIDI note).
        /// </summary>
        public float PitchSang;

        /// <summary>
        /// The amount of vocal ticks in the current phrase. Is decimal.<br/>
        /// A vocal tick is the amount of vocal updates per second.
        /// </summary>
        public double? PhraseTicksTotal;

        /// <summary>
        /// The amount of vocals ticks hit in the current phrase. Is not decimal.<br/>
        /// A vocal tick is the amount of vocal updates per second.
        /// </summary>
        public uint PhraseTicksHit;

        /// <summary>
        /// The last time there was a pitch update. <b>THIS IS FOR VISUAL PURPOSES ONLY.</b>
        /// </summary>
        public double VisualLastSingTime;

        /// <summary>
        /// The last time a note was hit. <b>THIS IS FOR VISUAL PURPOSES ONLY.</b>
        /// </summary>
        public double VisualLastHitTime;

        public override void Reset()
        {
            base.Reset();

            PitchSang = 0f;

            PhraseTicksTotal = null;
            PhraseTicksHit = 0;

            VisualLastSingTime = double.NegativeInfinity;
            VisualLastHitTime = double.NegativeInfinity;
        }
    }
}