using SlowDrip.Sim.Roasting;

namespace SlowDrip.RoastLab;

/// <summary>A named dial trace plus the lot it is roasting.</summary>
public sealed record Scenario(string Name, string Intent, DialTrace Trace, RoastCharge Charge)
{
    public static Scenario Named(string name) =>
        All.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"No scenario '{name}'. Known: {string.Join(", ", All.Select(s => s.Name))}");

    /// <summary>
    /// The reference curves from <see cref="ReferenceRoasts"/>, each paired with the
    /// claim in design.md #9.4 it exists to make visible.
    /// </summary>
    public static IReadOnlyList<Scenario> All { get; } = new[]
    {
        new Scenario(
            "healthy",
            "RoR gliding steadily downward. The roast the others are wrong against.",
            ReferenceRoasts.Healthy,
            RoastCharge.Default),

        new Scenario(
            "baked",
            "Heat pulled too early. RoR flatlines through drying, and first crack\n   arrives minutes late with the roast already gone.",
            ReferenceRoasts.Baked,
            RoastCharge.Default),

        new Scenario(
            "crash-and-flick",
            "Run hot into first crack, then slam the gas down. RoR halves, then the\n   exotherm flicks it back above where it started.",
            ReferenceRoasts.CrashAndFlick,
            RoastCharge.Default),

        new Scenario(
            "scorch",
            "Full burner held down. Fast, violent, and past the point of steering.",
            ReferenceRoasts.Scorch,
            RoastCharge.Default),

        new Scenario(
            "dense-lot",
            "The healthy trace on a denser, wetter high-grown lot. It stalls short of\n   first crack: last season's profile does not transfer.",
            ReferenceRoasts.Healthy,
            ReferenceRoasts.DenseLot),
    };
}
