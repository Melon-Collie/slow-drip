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

    /// <summary>Remaining water, dry basis (kg water per kg dry solids).</summary>
    public double Moisture { get; init; }

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
