namespace SlowDrip.Sim.Roasting;

/// <summary>
/// The green coffee going into the drum.
/// </summary>
/// <remarks>
/// These fields are why a saved profile does not transfer cleanly between lots
/// (design.md #9.4). Thermal mass falls out of <see cref="DryMassKg"/> and
/// <see cref="Moisture"/>; density scales how readily the mass takes heat. No
/// special case is needed to make last season's curve wrong this season — the
/// same dial trace simply lands somewhere else.
/// </remarks>
public sealed record RoastCharge
{
    /// <summary>Dry solids in the drum (kg). Water is tracked separately as moisture.</summary>
    public double DryMassKg { get; init; } = 1.0;

    /// <summary>Water content on a dry basis (kg water per kg dry solids). Fresh crop runs wetter.</summary>
    public double Moisture { get; init; } = 0.115;

    /// <summary>
    /// Bean density relative to a nominal lot. High-grown beans are denser and
    /// take heat more slowly for the same mass.
    /// </summary>
    public double DensityFactor { get; init; } = 1.0;

    /// <summary>
    /// Share of the water that starts locked in the bean core rather than near
    /// the surface.
    /// </summary>
    /// <remarks>
    /// Whatever is still in the core at first crack vents in a rush and crashes
    /// the rate of rise. Denser, high-grown beans hold their core water more
    /// stubbornly, which is why they punish a hurried drying phase harder.
    /// </remarks>
    public double CoreMoistureFraction { get; init; } = 0.5;

    /// <summary>
    /// Beans per kilogram of dry solids. Screen size, in effect — Robusta and
    /// peaberry run small and numerous, Maragogype the other way.
    /// </summary>
    public double BeansPerKg { get; init; } = 6000.0;

    /// <summary>Temperature the median bean ruptures at (degC).</summary>
    /// <remarks>Denser, higher-grown beans hold their pressure longer.</remarks>
    public double CrackTempMean { get; init; } = 196.0;

    /// <summary>
    /// Spread of rupture temperatures across the batch (degC, one standard
    /// deviation).
    /// </summary>
    /// <remarks>
    /// The uniformity of the lot, and the single number that decides what first
    /// crack sounds like. A well-sorted single screen size cracks in a tight
    /// volley the player can time against; a mixed lot smears the same number of
    /// pops over twice as long and gives a much worse cue. This is where #9.2's
    /// sorting table pays off a second time — not as defect removal, as
    /// information at the roaster.
    /// </remarks>
    public double CrackTempSpread { get; init; } = 3.5;

    /// <summary>Beans in the drum.</summary>
    public int BeanCount => Math.Max(1, (int)Math.Round(DryMassKg * BeansPerKg));

    /// <summary>A 1kg lot of nominal density and moisture.</summary>
    public static RoastCharge Default { get; } = new();

    /// <summary>Total mass in the drum at charge, water included (kg).</summary>
    public double TotalMassKg => DryMassKg * (1.0 + Moisture);
}
