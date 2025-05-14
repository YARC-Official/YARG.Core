namespace ReplayCli;

public class Program
{
    private static int Main(string[] args)
    {
        var defaultColor = Console.ForegroundColor;
        try
        {
            var cli = new Cli();
            if (!cli.ParseArguments(args))
            {
                return -1;
            }

            if (!cli.Run())
            {
                return -1;
            }

            return 0;
        }
        finally
        {
            Console.ForegroundColor = defaultColor;
        }
    }
}
