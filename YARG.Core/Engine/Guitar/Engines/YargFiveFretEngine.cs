using System;
using System.Collections.Generic;
using YARG.Core.Chart;
using YARG.Core.Input;

namespace YARG.Core.Engine.Guitar.Engines
{
    public class YargFiveFretEngine : GuitarEngine
    {

        public YargFiveFretEngine(List<GuitarNote> notes, GuitarEngineParameters engineParameters) : base(notes, engineParameters)
        {

        }

        protected override void ProcessInputs()
        {
            while (InputQueue.TryDequeue(out var input))
            {
                CurrentInput = input;
                UpdateHitLogic(input.Time);
            }
        }

        protected override bool UpdateHitLogic(double time)
        {
            double delta = time - LastUpdateTime;

            UpdateTimers(delta);

            // remove later
            return true;
        }

        protected void UpdateTimers(double delta)
        {
            State.StrummedThisUpdate = false;

            // If engine was invoked with an input update and the input is a fret
            if (IsInputUpdate && IsFretInput(CurrentInput) || !IsInputUpdate)
            {
                if (!(State.StrumLeniencyTimer > 0) || State.StrummedThisUpdate)
                {
                    return;
                }

                // Hopo leniency active and strum leniency active so hopo was strummed
                if (State.HopoLeniencyTimer > 0)
                {
                    State.HopoLeniencyTimer = 0;
                    State.StrumLeniencyTimer = 0;
                }
                else
                {
                    State.StrumLeniencyTimer -= delta;
                    if (State.StrumLeniencyTimer <= 0)
                    {
                        if (State.WasHopoStrummed)
                        {
                            State.StrumLeniencyTimer = 0;
                        }
                        else
                        {
                            // Overstrum in here
                        }

                        State.WasHopoStrummed = false;
                    }
                }

                if (State.HopoLeniencyTimer > 0)
                {
                    State.HopoLeniencyTimer -= delta;
                }
            }
        }

        protected override bool CanNoteBeHit(GuitarNote note)
        {
            throw new System.NotImplementedException();
        }

        protected bool IsFretInput(GameInput<GuitarAction> input)
        {
            return input.Action switch
            {
                GuitarAction.Green or
                GuitarAction.Red or
                GuitarAction.Yellow or
                GuitarAction.Blue or
                GuitarAction.Orange => true,
                _ => false,
            };
        }

        protected bool IsStrumInput(GameInput<GuitarAction> input)
        {
            return input.Action switch
            {
                GuitarAction.StrumUp or
                GuitarAction.StrumDown => true,
                _ => false,
            };
        }
    }
}