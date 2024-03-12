using System;
using YARG.Core.Logging;

// Temporary console app for quick and dirty testing
// Changes to this generally shouldn't be committed, but common test procedures are fine to keep around

YargLogger.AddLogListener(new ConsoleYargLogListener(new StandardYargLogFormatter()));

Console.WriteLine();
Console.WriteLine("Press any key to continue...");
YargLogger.KillLogger();
Console.ReadKey(intercept: true);