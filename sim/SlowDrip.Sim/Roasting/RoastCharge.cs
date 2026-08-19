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

    /// <summary>A 1kg lot of nominal density and moisture.</summary>
    public static RoastCharge Default { get; } = new();

    /// <summary>Total mass in the drum at charge, water included (kg).</summary>
    public double TotalMassKg => DryMassKg * (1.0 + Moisture);
}
