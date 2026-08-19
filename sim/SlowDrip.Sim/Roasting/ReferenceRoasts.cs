namespace SlowDrip.Sim.Roasting;

/// <summary>
/// Canonical roasts, one per claim worth holding the model to.
/// </summary>
/// <remarks>
/// <para>
/// These live in the sim rather than in the tuning tool because the tests assert
/// against them too. A roast and a tuned config are two halves of one claim — if
/// they drift apart, the tests stop meaning what they say.
/// </para>
/// <para>
/// Most of them are pilots rather than recordings, and that is itself a finding.
/// A recorded trace cuts the gas at a fixed second whether or not the roast has
/// got there yet, so it is brittle: on this machine, moving one mid-roast dial
/// step by two percent is the difference between a finished roast and one that
/// stalls at 120C. Anything that has to act relative to first crack has to watch
/// the curve, which is what the player will be doing.
/// </para>
/// </remarks>
public static class ReferenceRoasts
{
    /// <summary>Gas setting the reference machine wants across first crack.</summary>
    /// <remarks>
    /// Published guidance says to set it "low enough to keep the RoR decreasing
    /// but not so low that the roast loses momentum". On this machine that is a
    /// narrow band: 0.30 finishes 12C too dark, 0.18 stalls into a negative rate
    /// of rise. 0.26 is the middle of it.
    /// </remarks>
    public const double PreCrackGas = 0.26;

    /// <summary>
    /// The roast the others are wrong against: published protocol, played straight.
    /// Rate of rise gliding down, no stall, dropping near 213C at 20% development.
    /// </summary>
    public static DoctrinePilot Textbook() => new(preCrackGas: PreCrackGas);

    /// <summary>
    /// The same protocol with the pre-crack reduction made far too early. The roast
    /// loses momentum before it gets to first crack and the rate of rise goes
    /// negative — the stall the guidance warns about.
    /// </summary>
    public static DoctrinePilot CutTooEarly() => new(preCrackGas: PreCrackGas, leadSeconds: 90.0);

    /// <summary>
    /// The same protocol with the reduction left too late. By the time the gas
    /// comes down the exotherm is running and the roast bolts past the drop
    /// temperature.
    /// </summary>
    public static DoctrinePilot CutTooLate() => new(preCrackGas: PreCrackGas, leadSeconds: 10.0);

    /// <summary>
    /// Heat pulled hard early in drying. The rate of rise flattens near zero while
    /// the water boils off, and everything after that is late — the same protocol
    /// across first crack cannot buy the time back.
    /// </summary>
    public static DoctrinePilot Baked() => new(
        DialTrace.Of(new DialMove(0, 1.00), new DialMove(55, 0.28), new DialMove(330, 0.50)),
        preCrackGas: PreCrackGas);

    /// <summary>Full burner held down. Fast, violent, past the point of steering.</summary>
    public static DialTrace Scorch { get; } = DialTrace.Constant(1.0);

    /// <summary>
    /// A hand-played roast that respects the window: two reductions through drying,
    /// the pre-crack cut well clear of first crack, and a tail step in development.
    /// Fragile by construction — see the remarks on this class.
    /// </summary>
    public static DialTrace HandPlayed { get; } = DialTrace.Of(
        new DialMove(0, 1.00),
        new DialMove(90, 0.64),
        new DialMove(240, 0.42),
        new DialMove(470, 0.27),
        new DialMove(620, 0.17));

    /// <summary>
    /// A denser, wetter high-grown lot. The hand-played trace misses on it; the
    /// pilot, which watches the curve, adapts.
    /// </summary>
    public static RoastCharge DenseLot { get; } = new()
    {
        DryMassKg = 1.0,
        Moisture = 0.125,
        DensityFactor = 1.10,
    };
}
