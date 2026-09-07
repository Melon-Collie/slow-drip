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
/// got there yet, so it lands somewhere else on a lot that heats differently.
/// Anything that has to act relative to first crack has to watch the curve, which
/// is what the player will be doing.
/// </para>
/// </remarks>
public static class ReferenceRoasts
{
    /// <summary>Gas setting the reference machine wants across first crack.</summary>
    /// <remarks>
    /// Published guidance says to set it "low enough to keep the RoR decreasing
    /// but not so low that the roast loses momentum". It has to sit below the
    /// opening's last step, or the reduction is not one.
    /// </remarks>
    public const double PreCrackGas = 0.44;

    /// <summary>
    /// The roast the others are wrong against: published protocol, played straight.
    /// Rate of rise gliding down, no stall, dropping near 213C at 20% development.
    /// </summary>
    public static DoctrinePilot Textbook() => new(preCrackGas: PreCrackGas);

    /// <summary>
    /// The same protocol with the pre-crack reduction taken too far down. The roast
    /// loses momentum, the rate of rise sags toward zero, and it never reaches drop
    /// temperature — the stall the guidance warns about.
    /// </summary>
    /// <remarks>
    /// The reduction's <i>depth</i> is what this machine punishes, not its timing.
    /// Sweeping the taught 45s lead from 120s to 10s moves the development ratio by
    /// about three points and never fails; sweeping the gas from 0.30 to 0.62 runs
    /// from a dead roast to one that arrives underdeveloped. See sim/README.md — the
    /// knife-edge on lead time that the previous model had was an artefact of an
    /// exotherm strong enough to make the roast metastable.
    /// <para>
    /// 0.26 is far enough down that the beans actually start losing heat, which is
    /// what <see cref="IRoastPilot.Abandon"/> watches for. Above about 0.31 a failing
    /// roast instead crawls: never net-negative, never reaching drop, running the
    /// clock out. That band is worth knowing about — with the energy balance corrected
    /// a dying roast no longer reliably announces itself by going negative, so the
    /// net-watts warning covers less of the failure space than it used to.
    /// </para>
    /// </remarks>
    public static DoctrinePilot CutTooDeep() => new(preCrackGas: 0.26);

    /// <summary>
    /// The same protocol with barely any reduction at all. Nothing crashes and
    /// nothing stalls; the roast simply carries too much heat into development and
    /// arrives at drop temperature before development has happened.
    /// </summary>
    public static DoctrinePilot CutTooShallow() => new(preCrackGas: 0.58);

    /// <summary>
    /// Heat pulled hard early in drying. The rate of rise flattens near zero while
    /// the water boils off, and everything after that is late — the same protocol
    /// across first crack cannot buy the time back.
    /// </summary>
    public static DoctrinePilot Baked() => new(
        DialTrace.Of(new DialMove(0, 0.85), new DialMove(55, 0.26), new DialMove(330, 0.52)),
        preCrackGas: PreCrackGas);

    /// <summary>Full burner held down. Fast, violent, past the point of steering.</summary>
    public static DialTrace Scorch { get; } = DialTrace.Constant(1.0);

    /// <summary>
    /// A hand-played roast that respects the window: two reductions through drying,
    /// the pre-crack cut well clear of first crack, and a tail step in development.
    /// Fragile by construction — see the remarks on this class.
    /// </summary>
    public static DialTrace HandPlayed { get; } = DialTrace.Of(
        new DialMove(0, 0.85),
        new DialMove(90, 0.66),
        new DialMove(240, 0.48),
        new DialMove(500, 0.44),
        new DialMove(700, 0.36));

    /// <summary>
    /// A lot sorted to one screen size. First crack arrives as a tight volley the
    /// player can time a gas reduction against.
    /// </summary>
    public static RoastCharge WellSorted { get; } = new() { CrackTempSpread = 2.0 };

    /// <summary>
    /// A lot that went to the roaster unsorted. The same six thousand pops are
    /// smeared over four minutes instead of two, they start half a minute early, and
    /// they never get loud enough to be an obvious cue — so the early stragglers read
    /// as first crack, the gas comes down too soon, and development runs long while
    /// the bulk of the batch is still cracking.
    /// </summary>
    /// <remarks>
    /// This is #9.2 paying off twice. Sorting is framed there as costing yield to
    /// protect the score; here it also buys the one piece of information the
    /// roaster most needs, and no part of the model was told to make that happen.
    /// <para>
    /// What it costs is control of the development ratio, not the roast itself: a
    /// ragged lot lands near 30% where a sorted one lands near 22%. Only a genuinely
    /// extreme spread (13C and up) runs the clock out. The previous model killed the
    /// roast outright at this spread, which was the over-strong exotherm leaving no
    /// margin anywhere rather than a fact about screen size.
    /// </para>
    /// </remarks>
    public static RoastCharge MixedScreen { get; } = new() { CrackTempSpread = 6.0 };

    /// <summary>
    /// A denser, wetter high-grown lot. The hand-played trace misses on it; the
    /// pilot, which watches the curve, adapts.
    /// </summary>
    public static RoastCharge DenseLot { get; } = new()
    {
        DryMassKg = 1.0,
        Moisture = 0.135,
        DensityFactor = 1.15,
    };
}
