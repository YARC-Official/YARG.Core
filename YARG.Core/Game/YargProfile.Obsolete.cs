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
                        SwapSnareAndHiHat.Value ?  DrumsHighwayItem.FourLaneYellowCymbal : DrumsHighwayItem.FourLaneRed,
                        SwapSnareAndHiHat.Value ?  DrumsHighwayItem.FourLaneRed : DrumsHighwayItem.FourLaneYellowCymbal,
                        DrumsHighwayItem.FourLaneYellowDrum,
                        SwapCrashAndRide.Value ? DrumsHighwayItem.FourLaneGreenCymbal : DrumsHighwayItem.FourLaneBlueCymbal,
                        DrumsHighwayItem.FourLaneBlueDrum,
                        SwapCrashAndRide.Value ? DrumsHighwayItem.FourLaneBlueCymbal : DrumsHighwayItem.FourLaneGreenCymbal,
                        DrumsHighwayItem.FourLaneGreenDrum,
                    };
            }

            if (SwapSnareAndHiHat is not null && SwapSnareAndHiHat.Value)
            {
                FiveLaneDrumsHighwayOrdering = new DrumsHighwayItem[]
                    {
                        DrumsHighwayItem.FiveLaneYellow,
                        DrumsHighwayItem.FiveLaneRed,
                        DrumsHighwayItem.FiveLaneBlue,
                        DrumsHighwayItem.FiveLaneOrange,
                        DrumsHighwayItem.FiveLaneGreen
                    };
            }

            SplitProTomsAndCymbals = null;
            SwapSnareAndHiHat = null;
            SwapCrashAndRide = null;
        }
#pragma warning restore 612, 618
    }
}
