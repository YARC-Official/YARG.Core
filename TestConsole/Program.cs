using System;
using YARG.Core;

// Temporary console app for quick and dirty testing
// Changes to this generally shouldn't be committed, but common test procedures are fine to keep around

YargTrace.AddListener(new YargDebugTraceListener());

Console.WriteLine();
Console.WriteLine("Press any key to continue...");
Console.ReadKey(intercept: true);