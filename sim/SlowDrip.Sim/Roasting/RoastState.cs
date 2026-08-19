namespace SlowDrip.Sim.Roasting;

/// <summary>
/// An immutable snapshot of the roaster. Presentation reads this and never holds it
/// (design.md #15).
/// </summary>
public readonly record struct RoastState
{
    /// <summary>Seconds since charge.</summary>
    public double Time { get; init; }

    /// <summary>Dial position, 0..1.</summary>
    public double Burner { get; init; }

    /// <summary>Drum and air temperature (degC).</summary>
    public double EnvTemp { get; init; }

    /// <summary>
    /// True bean temperature (degC). Not observable in game — the player reads
    /// <see cref="BeanProbe"/>. Exposed for tests and tuning.
    /// </summary>
    public double BeanTemp { get; init; }

    /// <summary>Bean probe reading (degC). This is the number the player sees.</summary>
    public double BeanProbe { get; init; }

    /// <summary>Smoothed rate of rise of the probe (degC per minute).</summary>
    public double RateOfRise { get; init; }

    /// <summary>Water near the bean surface, free to evaporate (dry basis).</summary>
    public double SurfaceMoisture { get; init; }

    /// <summary>
    /// Water still locked in the bean core (dry basis). Whatever remains here at
    /// first crack is what crashes the rate of rise.
    /// </summary>
    public double CoreMoisture { get; init; }

    /// <summary>Total remaining water, dry basis (kg water per kg dry solids).</summary>
    public double Moisture => SurfaceMoisture + CoreMoisture;

    /// <summary>Unreacted share of the roast reactions, 1 at charge and falling.</summary>
    public double ReactantRemaining { get; init; }

    /// <summary>Current self-heating output of the bean mass (W).</summary>
    public double ExothermWatts { get; init; }

    /// <summary>
    /// Net power into the bean mass (W): what the drum is giving, plus what the
    /// beans are generating, minus what evaporation is taking. Negative means the
    /// roast is losing.
    /// </summary>
    /// <remarks>
    /// This is the number that decides whether a roast lives, and it is state
    /// rather than prophecy — design.md #9.3's rule is "expose state, hide
    /// outcome", and this is the state side of that line. It says the roast is
    /// losing heat right now; it does not say how the coffee will taste, or even
    /// that the roast is doomed. A player who adds gas can put it back positive.
    /// </remarks>
    public double NetBeanWatts { get; init; }

    /// <summary>
    /// How much hotter the drum is than the beans (degC). Below zero the drum is
    /// pulling heat back out of them.
    /// </summary>
    /// <remarks>
    /// A real roaster reads this off the environmental temperature gauge, which is
    /// why it is fair to show: it is an instrument they actually have, not a hint
    /// the game invented.
    /// </remarks>
    public double DrumHeadroom => EnvTemp - BeanTemp;

    /// <summary>Share of the batch that has ruptured, 0..1.</summary>
    public double CrackedFraction { get; init; }

    /// <summary>
    /// Beans rupturing per second. This is the audio signal of design.md #9.4 —
    /// presentation scatters pops at this rate rather than playing a cue.
    /// </summary>
    public double PopsPerSecond { get; init; }

    /// <summary>True once first crack has begun.</summary>
    public bool FirstCrack { get; init; }

    /// <summary>Seconds since charge when first crack began, or -1.</summary>
    public double FirstCrackTime { get; init; }

    /// <summary>Derived stage of the roast.</summary>
    public RoastPhase Phase { get; init; }

    /// <summary>
    /// Time from first crack to now as a fraction of total elapsed time, or -1
    /// before first crack. The one number design.md #9.4 asks to surface.
    /// </summary>
    public double DevelopmentTimeRatio =>
        FirstCrack && Time > 0.0 ? (Time - FirstCrackTime) / Time : -1.0;
}
