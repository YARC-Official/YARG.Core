using YARG.Core.Input;

namespace YARG.Core.Engine.Vocals
{
    /// Mic index is packed into the high bits of GameInput.Action for Party
    /// Vocals so a single flat input stream can carry which mic produced each
    /// input. GameInput is a union (Integer/Axis/Button alias), so the mic
    /// cannot ride in Integer alongside a pitch Axis — hence Action-packing.
    public static class PartyVocalsInput
    {
        private const int MIC_SHIFT = 8;
        private const int ACTION_MASK = 0xFF;

        public static int Pack(int micIndex, VocalsAction action) =>
            (micIndex << MIC_SHIFT) | ((int) action & ACTION_MASK);

        public static int UnpackMic(int packedAction) => packedAction >> MIC_SHIFT;
        public static VocalsAction UnpackAction(int packedAction) =>
            (VocalsAction) (packedAction & ACTION_MASK);
    }
}
