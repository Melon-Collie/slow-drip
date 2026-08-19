using System.Globalization;
using SlowDrip.Sim.Roasting;
using SlowDrip.RoastLab;

// roastlab — run roasts headlessly, at whatever speed the machine manages.
//
//   roastlab                     summarise every reference scenario
//   roastlab <scenario>          summarise one
//   roastlab <scenario> --csv    dump the curve to stdout for plotting

var name = args.FirstOrDefault(a => !a.StartsWith("--", StringComparison.Ordinal));
var wantCsv = args.Contains("--csv");

if (name is null)
{
    foreach (var scenario in Scenario.All) Summarise(scenario);
    return 0;
}

Scenario chosen;
try
{
    chosen = Scenario.Named(name);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

if (wantCsv)
{
    Console.Write(RunOf(chosen).ToCsv());
    return 0;
}

Summarise(chosen);
return 0;

static RoastLog RunOf(Scenario s) => RoastRunner.Run(
    s.Trace,
    charge: s.Charge,
    maxSeconds: 900.0,
    dropWhen: RoastRunner.DropAtDevelopmentRatio(0.20));

static void Summarise(Scenario s)
{
    var log = RunOf(s);
    var tp = log.TurningPoint;

    Console.WriteLine($"== {s.Name}");
    Console.WriteLine($"   {s.Intent}");
    Console.WriteLine(tp is { } t
        ? $"   turning point   {Clock(t.Time)} at {t.BeanProbe:F1}C"
        : "   turning point   none — probe never bottomed out");
    Console.WriteLine(log.ReachedFirstCrack
        ? $"   first crack     {Clock(log.FirstCrackTime)}"
        : "   first crack     never reached");
    Console.WriteLine($"   drop            {Clock(log.DropTime)} at {log.DropTemp:F1}C");
    Console.WriteLine(log.DevelopmentTimeRatio >= 0.0
        ? $"   dev time ratio  {log.DevelopmentTimeRatio:P1}"
        : "   dev time ratio  n/a");
    Console.WriteLine($"   peak RoR        {log.PeakRateOfRise:F1} C/min");
    Console.WriteLine($"   RoR profile     {RorSparkline(log)}");
    Console.WriteLine();
}

// Coarse ASCII plot of RoR over the roast — enough to see a glide, a flatline,
// or a crash without leaving the terminal.
static string RorSparkline(RoastLog log)
{
    const string Ramp = " .:-=+*#%";
    var afterTurn = log.Samples.Where(s => s.Time >= 60.0).ToArray();
    if (afterTurn.Length == 0) return "(too short)";

    var max = Math.Max(1.0, afterTurn.Max(s => s.RateOfRise));
    var columns = 48;
    var chars = new char[columns];
    for (var i = 0; i < columns; i++)
    {
        var sample = afterTurn[(int)((long)i * (afterTurn.Length - 1) / (columns - 1))];
        var level = Math.Clamp(sample.RateOfRise / max, 0.0, 1.0);
        chars[i] = Ramp[(int)Math.Round(level * (Ramp.Length - 1))];
    }

    return new string(chars);
}

static string Clock(double seconds) =>
    seconds < 0.0
        ? "--:--"
        : string.Create(CultureInfo.InvariantCulture, $"{(int)(seconds / 60):00}:{(int)(seconds % 60):00}");
