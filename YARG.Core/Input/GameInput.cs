namespace YARG.Core.Input
{
    public struct GameInput
    {
        private readonly int _rawActionValue;

        public ActionType Type { get; }
        public double     Time { get; set; }

        public GameInput(int rawActionValue, double time, ActionType type)
        {
            _rawActionValue = rawActionValue;
            Time = time;
            Type = type;
        }

        public GuitarAction GetGuitarAction()
        {
            return (GuitarAction) _rawActionValue;
        }

        public DrumAction GetDrumAction()
        {
            return (DrumAction) _rawActionValue;
        }

        public MenuAction GetMenuAction()
        {
            return (MenuAction) _rawActionValue;
        }
    }
}