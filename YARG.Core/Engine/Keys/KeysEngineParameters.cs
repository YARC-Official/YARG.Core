using System.IO;
using YARG.Core.Extensions;
using YARG.Core.IO;

namespace YARG.Core.Engine.Keys
{
    public class KeysEngineParameters : BaseEngineParameters
    {
        public readonly double ChordStaggerWindow;

        public readonly double FatFingerWindow;

        public readonly bool NoStarPowerOverlap;

        /// <summary>
        /// Allows five-lane key releases to hit matching notes. Pro Keys intentionally ignores this option.
        /// </summary>
        public readonly bool ReleaseKeysCanHitNotes;

        public KeysEngineParameters(HitWindowSettings hitWindow, int maxMultiplier, double spWhammyBuffer,
            double sustainDropLeniency, float[] starMultiplierThresholds, float[] soloBonusStarMultiplierThresholds, double chordStaggerWindow, double fatFingerWindow,
            bool noStarPowerOverlap, bool enableLanes, bool releaseKeysCanHitNotes = false)
            : base(hitWindow, maxMultiplier, spWhammyBuffer, sustainDropLeniency, starMultiplierThresholds, soloBonusStarMultiplierThresholds, enableLanes)
        {
            ChordStaggerWindow = chordStaggerWindow;
            FatFingerWindow = fatFingerWindow;
            NoStarPowerOverlap = noStarPowerOverlap;
            ReleaseKeysCanHitNotes = releaseKeysCanHitNotes;
        }

        public KeysEngineParameters(ref FixedArrayStream stream, int version)
            : base(ref stream, version)
        {
            ChordStaggerWindow = stream.Read<double>(Endianness.Little);
            FatFingerWindow = stream.Read<double>(Endianness.Little);
            if (version >= 9)
            {
                NoStarPowerOverlap = stream.ReadBoolean();
            }

            if (version >= 19)
            {
                ReleaseKeysCanHitNotes = stream.ReadBoolean();
            }
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);

            writer.Write(ChordStaggerWindow);
            writer.Write(FatFingerWindow);
            writer.Write(NoStarPowerOverlap);
            writer.Write(ReleaseKeysCanHitNotes);
        }

        public override string ToString()
        {
            return
                $"{base.ToString()}\n" +
                $"Chord stagger window: {ChordStaggerWindow}\n" +
                $"Fat finger window: {FatFingerWindow}\n" +
                $"No star power overlap: {NoStarPowerOverlap}\n" +
                $"Release keys can hit notes: {ReleaseKeysCanHitNotes}";
        }
    }
}