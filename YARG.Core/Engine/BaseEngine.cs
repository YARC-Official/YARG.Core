﻿using System;
using System.Collections.Generic;
using YARG.Core.Chart;
using YARG.Core.Input;

namespace YARG.Core.Engine
{
    public abstract class BaseEngine<TNoteType, TInputType, TActionType, TEngineParams, TEngineStats> 
        where TNoteType : Note
        where TInputType : AbstractGameInput<TActionType>
        where TActionType : Enum
        where TEngineParams : BaseEngineParameters
        where TEngineStats : BaseStats, new()
    {
        protected const int POINTS_PER_NOTE = 50;
        protected const int POINTS_PER_BEAT = 25;
        
        protected const double STAR_POWER_PHRASE_AMOUNT = 0.25;
        
        protected readonly Queue<TInputType> InputQueue;
        
        protected readonly List<TNoteType> Notes;
        protected readonly TEngineParams EngineParameters;
        protected readonly TEngineStats EngineStats;
        
        protected BaseEngine(List<TNoteType> notes, TEngineParams engineParameters)
        {
            Notes = notes;
            EngineParameters = engineParameters;

            EngineStats = new TEngineStats();
            
            InputQueue = new Queue<TInputType>();
        }
        
        /// <summary>
        /// Queue an input to be processed by the engine.
        /// </summary>
        /// <param name="input">The input to queue into the engine.</param>
        public void QueueInput(TInputType input)
        {
            InputQueue.Enqueue(input);
        }

        /// <summary>
        /// Updates the engine and processes all inputs currently queued.
        /// </summary>
        public void UpdateEngine()
        {
            if (InputQueue.Count > 0)
            {
                ProcessInputs();
            }
        }
        
        /// <summary>
        /// Loops through the input queue and processes each input. Invokes engine logic for each input.
        /// </summary>
        protected abstract void ProcessInputs();
        
        /// <summary>
        /// Executes engine logic with respect to the given time.
        /// </summary>
        /// <param name="time">The time in which to simulate hit logic at.</param>
        /// <returns>True if a note was updated (hit or missed). False if no changes.</returns>
        protected abstract bool UpdateHitLogic(double time);
        
        /// <summary>
        /// Checks if the given note can be hit with the current input state.
        /// </summary>
        /// <param name="note">The Note to attempt to hit.</param>
        /// <returns>True if note can be hit. False otherwise.</returns>
        protected abstract bool CanNoteBeHit(TNoteType note);
        
        /// <summary>
        /// Resets the engine's state back to default and then processes the list of inputs up to the given time.
        /// </summary>
        /// <param name="time">Time to process up to.</param>
        /// <param name="inputs">List of inputs to execute against.</param>
        /// <returns>The input index that was processed up to.</returns>
        public abstract int ProcessUpToTime(double time, IList<TInputType> inputs);

        /// <summary>
        /// Processes the list of inputs from the given start time to the given end time. Does not reset the engine's state.
        /// </summary>
        /// <param name="startTime">Time to begin processing from.</param>
        /// <param name="endTime">Time to process up to.</param>
        /// <param name="inputs">List of inputs to execute against.</param>
        public abstract void ProcessFromTimeToTime(double startTime, double endTime, IList<TInputType> inputs);
    }
}