namespace SlowDrip.Sim.Roasting;

/// <summary>
/// Canonical dial traces, one per curve shape design.md #9.4 names.
/// </summary>
/// <remarks>
/// These live in the sim rather than in the tuning tool because the tests assert
/// against them too. A trace and a tuned config are two halves of one claim — if
/// they drift apart, the tests stop meaning what they say.
/// </remarks>
public static class ReferenceRoasts
{
    /// <summary>
    /// RoR gliding steadily downward. First crack near 09:00, drop near 11:30 at
    /// roughly 213C with a 20% development ratio. The roast the others are wrong
    /// against.
    /// </summary>
    public static DialTrace Healthy { get; } = DialTrace.Of(
        new DialMove(0, 1.00),
        new DialMove(90, 0.66),
        new DialMove(240, 0.42),
        new DialMove(390, 0.34),
        new DialMove(510, 0.14));

    /// <summary>
    /// Heat pulled too early. RoR flatlines through drying and first crack arrives
    /// minutes late, by which point the roast is gone.
    /// </summary>
    public static DialTrace Baked { get; } = DialTrace.Of(
        new DialMove(0, 1.00),
        new DialMove(55, 0.26),
        new DialMove(330, 0.44),
        new DialMove(480, 0.52),
        new DialMove(660, 0.42));

    /// <summary>
    /// Run hot into first crack, then slam the gas down to catch it. RoR halves,
    /// and then the exotherm flicks it back above where it started — the harsh
    /// edge of design.md #9.4, arriving after the correction that was meant to
    /// prevent it.
    /// </summary>
    public static DialTrace CrashAndFlick { get; } = DialTrace.Of(
        new DialMove(0, 1.00),
        new DialMove(90, 0.66),
        new DialMove(240, 0.62),
        new DialMove(330, 0.24));

    /// <summary>Full burner held down. Fast, violent, past the point of steering.</summary>
    public static DialTrace Scorch { get; } = DialTrace.Constant(1.0);

    /// <summary>
    /// A denser, wetter high-grown lot. Roasted on <see cref="Healthy"/> it stalls
    /// short of first crack — last season's profile does not transfer.
    /// </summary>
    public static RoastCharge DenseLot { get; } = new()
    {
        DryMassKg = 1.0,
        Moisture = 0.125,
        DensityFactor = 1.10,
    };
}
