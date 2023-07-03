namespace YARG.Core
{
    public class YargProfile
    {

        public string Name;

        public GameMode GameMode => Instrument.ToGameMode();

        public Instrument Instrument;
        public Difficulty Difficulty;

        public float NoteSpeed;
        public float HighwayLength;

        public bool LeftyFlip;

        public bool IsBot;

        public YargProfile()
        {
            Name = "Default";
            Instrument = Instrument.FiveFretGuitar;
            Difficulty = Difficulty.Expert;
            NoteSpeed = 6;
            HighwayLength = 1;
            LeftyFlip = false;
        }
    }
}