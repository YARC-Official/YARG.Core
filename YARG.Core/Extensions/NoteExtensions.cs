using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YARG.Core.Chart;
using static YARG.Core.Engine.Keys.FiveLaneKeysEngine;

namespace YARG.Core.Extensions
{
    public static class NoteExtensions
    {
        public static FiveFretGuitarFret ToFret(this FiveLaneKeysAction action)
        {
            return action switch
            {
                FiveLaneKeysAction.GreenKey => FiveFretGuitarFret.Green,
                FiveLaneKeysAction.RedKey => FiveFretGuitarFret.Red,
                FiveLaneKeysAction.YellowKey => FiveFretGuitarFret.Yellow,
                FiveLaneKeysAction.BlueKey => FiveFretGuitarFret.Blue,
                FiveLaneKeysAction.OrangeKey => FiveFretGuitarFret.Orange,
                FiveLaneKeysAction.OpenNote => FiveFretGuitarFret.Open,
                FiveLaneKeysAction.Wildcard => FiveFretGuitarFret.Wildcard,
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }
}
