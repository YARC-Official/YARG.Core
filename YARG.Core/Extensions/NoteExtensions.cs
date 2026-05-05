using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YARG.Core.Chart;
using YARG.Core.Input;
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

        public static DrumsAction ToAction(this FourLaneDrumPad pad)
        {
            return pad switch {
                FourLaneDrumPad.RedDrum => DrumsAction.RedDrum,
                FourLaneDrumPad.YellowDrum => DrumsAction.YellowDrum,
                FourLaneDrumPad.BlueDrum => DrumsAction.BlueDrum,
                FourLaneDrumPad.GreenDrum => DrumsAction.GreenDrum,
                FourLaneDrumPad.YellowCymbal => DrumsAction.YellowCymbal,
                FourLaneDrumPad.BlueCymbal => DrumsAction.BlueCymbal,
                FourLaneDrumPad.GreenCymbal => DrumsAction.GreenCymbal,
                FourLaneDrumPad.Kick => DrumsAction.Kick,
                FourLaneDrumPad.Wildcard => DrumsAction.WildcardPad,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public static DrumsAction ToAction(this FiveLaneDrumPad pad)
        {
            return pad switch
            {
                FiveLaneDrumPad.Red => DrumsAction.RedDrum,
                FiveLaneDrumPad.Yellow => DrumsAction.YellowCymbal,
                FiveLaneDrumPad.Blue => DrumsAction.BlueDrum,
                FiveLaneDrumPad.Orange => DrumsAction.OrangeCymbal,
                FiveLaneDrumPad.Green => DrumsAction.GreenDrum,
                FiveLaneDrumPad.Kick => DrumsAction.Kick,
                FiveLaneDrumPad.Wildcard => DrumsAction.WildcardPad,
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }
}
