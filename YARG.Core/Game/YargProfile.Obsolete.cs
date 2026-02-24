using System;

namespace YARG.Core.Game
{
    public partial class YargProfile
    {
        [Obsolete("Superseded by highway reordering.")]
        public bool? SplitProTomsAndCymbals;

        [Obsolete("Superseded by highway reordering.")]
        public bool? SwapSnareAndHiHat;

        [Obsolete("Superseded by highway reordering.")]
        public bool? SwapCrashAndRide;


#pragma warning disable 612, 618 // Ignore obsolete warnings since this is the place where we grandfather them in
        public void GrandfatherIn()
        {
            if (SplitProTomsAndCymbals is not null && SplitProTomsAndCymbals.Value)
            {
                ProDrumsHighwayOrdering = new DrumsHighwayItem[]
                    {
                        SwapSnareAndHiHat.Value ?  DrumsHighwayItem.YellowCymbal : DrumsHighwayItem.Red,
                        SwapSnareAndHiHat.Value ?  DrumsHighwayItem.Red : DrumsHighwayItem.YellowCymbal,
                        DrumsHighwayItem.YellowDrum,
                        SwapCrashAndRide.Value ? DrumsHighwayItem.GreenCymbal : DrumsHighwayItem.BlueCymbal,
                        DrumsHighwayItem.BlueDrum,
                        SwapCrashAndRide.Value ? DrumsHighwayItem.BlueCymbal : DrumsHighwayItem.GreenCymbal,
                        DrumsHighwayItem.GreenDrum,
                    };
            }

            if (SwapSnareAndHiHat is not null && SwapSnareAndHiHat.Value)
            {
                FiveLaneDrumsHighwayOrdering = new DrumsHighwayItem[]
                    {
                        DrumsHighwayItem.Yellow,
                        DrumsHighwayItem.Red,
                        DrumsHighwayItem.Blue,
                        DrumsHighwayItem.Orange,
                        DrumsHighwayItem.Green
                    };
            }

            SplitProTomsAndCymbals = null;
            SwapSnareAndHiHat = null;
            SwapCrashAndRide = null;
        }
#pragma warning restore 612, 618
    }
}
