namespace YARG.Core.Game
{
    public partial class ColorProfile
    {

        public static ColorProfile Default = new("Default");

        public string Name;

        public ColorProfile(string name)
        {
            Name = name;
        }
    }
}