using Newtonsoft.Json;
using System;

namespace YARG.Core.Game
{
    public partial class YargProfile
    {
        [Obsolete("Superseded by highway reordering.")]
        public bool SplitProTomsAndCymbals;

        [Obsolete("Superseded by highway reordering.")]
        public bool SwapSnareAndHiHat;

        [Obsolete("Superseded by highway reordering.")]
        public bool SwapCrashAndRide;


#pragma warning disable 612, 618 // Ignore obsolete warnings since this is the place where we grandfather them in
        public void GrandfatherIn()
        {
            UpgradeDrumCustomization();

            // Always do this last
            Version = PROFILE_VERSION;
        }

        private void UpgradeDrumCustomization()
        {
            if (Version < 8 && SplitProTomsAndCymbals)
            {
                ProDrumsHighwayOrdering = new DrumsHighwayItem[]
                    {
                        SwapSnareAndHiHat ?  DrumsHighwayItem.YellowCymbal : DrumsHighwayItem.Red,
                        SwapSnareAndHiHat ?  DrumsHighwayItem.Red : DrumsHighwayItem.YellowCymbal,
                        DrumsHighwayItem.YellowDrum,
                        SwapCrashAndRide ? DrumsHighwayItem.GreenCymbal : DrumsHighwayItem.BlueCymbal,
                        DrumsHighwayItem.BlueDrum,
                        SwapCrashAndRide ? DrumsHighwayItem.BlueCymbal : DrumsHighwayItem.GreenCymbal,
                        DrumsHighwayItem.GreenDrum,
                    };
            }

            if (Version < 8 && SwapSnareAndHiHat)
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

            SplitProTomsAndCymbals = false;
            SwapSnareAndHiHat = false;
            SwapCrashAndRide = false;
        }
#pragma warning restore 612, 618
    }
}
