namespace SlowDrip.Sim.Roasting;

/// <summary>
/// Machine and physics constants for the drum roaster.
/// </summary>
/// <remarks>
/// Every field here is a tuning knob, not a law. The structure is the part to
/// hold onto: two coupled thermal bodies, an evaporation sink, an exothermic
/// source, and a lagged sensor. The ~20s dead time of design.md #9.4 is not
/// stored anywhere in this type — it emerges from the composition.
/// </remarks>
public sealed record RoasterConfig
{
    /// <summary>Room temperature (degC). Beans charge at this temperature.</summary>
    public double AmbientTemp { get; init; } = 22.0;

    /// <summary>Drum temperature at charge (degC) — the preheat the player sets up with.</summary>
    public double ChargeTemp { get; init; } = 210.0;

    // ---- Environment body (drum + air) -------------------------------------

    /// <summary>Burner output at full dial (W).</summary>
    public double BurnerPower { get; init; } = 5000.0;

    /// <summary>Effective heat capacity of drum and air (J/K). Sets the burner-to-drum lag.</summary>
    public double EnvHeatCapacity { get; init; } = 2500.0;

    /// <summary>Conductance from drum to room (W/K). Sets the ceiling temperature.</summary>
    public double EnvLossConductance { get; init; } = 9.0;

    // ---- Bean body ---------------------------------------------------------

    /// <summary>Conductance from drum to bean mass (W/K per kg of dry bean).</summary>
    public double BeanConductance { get; init; } = 14.0;

    /// <summary>Specific heat of dry coffee solids (J/(kg*K)).</summary>
    public double BeanSpecificHeat { get; init; } = 1700.0;

    /// <summary>Specific heat of water (J/(kg*K)).</summary>
    public double WaterSpecificHeat { get; init; } = 4186.0;

    // ---- Evaporation (the drying phase) ------------------------------------

    /// <summary>Latent heat of vaporisation of water (J/kg).</summary>
    public double LatentHeatOfVaporisation { get; init; } = 2.26e6;

    /// <summary>Bean temperature above which free moisture starts leaving (degC).</summary>
    public double DryingOnset { get; init; } = 60.0;

    /// <summary>Drying rate coefficient (1/(K*s)), applied to moisture * (BT - onset).</summary>
    public double DryingCoefficient { get; init; } = 5.0e-5;

    // ---- Exotherm ----------------------------------------------------------

    /// <summary>
    /// Peak self-heating output of the bean mass (W per kg of dry bean).
    /// </summary>
    /// <remarks>
    /// Tuned for feel, not measured. This is a net figure standing in for the whole
    /// post-crack energy balance, since the model has no separate term for pyrolysis
    /// or airflow. It was raised until cutting the gas at first crack produced a
    /// visible crash and recovery in the RoR readout rather than a plain decline —
    /// design.md #9.4 asks for that shape to be legible, and this is the parameter
    /// that decides whether it is. Lower it and the roast stops fighting back.
    /// </remarks>
    public double ExothermPower { get; init; } = 1000.0;

    /// <summary>Bean temperature at which self-heating reaches half strength (degC).</summary>
    public double ExothermOnset { get; init; } = 195.0;

    /// <summary>Width of the exotherm ramp (K). Smaller is snappier and meaner.</summary>
    public double ExothermWidth { get; init; } = 6.0;

    // ---- First crack -------------------------------------------------------

    /// <summary>Bean temperature at which first crack begins (degC).</summary>
    public double FirstCrackTemp { get; init; } = 196.0;

    /// <summary>Beans will not crack while still this wet (kg water per kg dry solids).</summary>
    public double FirstCrackMaxMoisture { get; init; } = 0.025;

    // ---- Sensor ------------------------------------------------------------

    /// <summary>
    /// Time constant of the bean probe (s). The probe is what the player sees;
    /// true bean temperature is not observable. This is a second source of lag
    /// on top of the thermal one, and it is what produces the turning point.
    /// </summary>
    public double ProbeTimeConstant { get; init; } = 25.0;

    /// <summary>
    /// Fraction of the probe reading that comes from the drum rather than the beans.
    /// </summary>
    /// <remarks>
    /// A thermocouple buried in the bean mass still sees the drum radiating at it,
    /// so it never reads true bean temperature. Keeping the bleed explicit is what
    /// puts the turning point at a plausible temperature instead of near ambient,
    /// and it is a reminder that the displayed curve is an instrument, not truth.
    /// </remarks>
    public double ProbeEnvBleed { get; init; } = 0.10;

    // ---- Rate of rise readout ----------------------------------------------

    /// <summary>
    /// Window the RoR finite difference is taken over (s).
    /// </summary>
    public double RorWindow { get; init; } = 15.0;

    /// <summary>
    /// Smoothing time constant applied to RoR (s).
    /// </summary>
    /// <remarks>
    /// This is a design knob, not an implementation detail. RoR is a derivative
    /// of a noisy signal so it must be filtered, and filtering adds display lag
    /// on top of the physical lag. How much is smoothed decides how far ahead a
    /// crash is visible — the telegraphing dial behind design.md #9.4's
    /// "punishable but telegraphed". Feel-test it; do not quietly tune it.
    /// </remarks>
    public double RorSmoothing { get; init; } = 4.0;

    /// <summary>
    /// The reference machine. These numbers came out of a parameter sweep against
    /// four targets on the reference dial trace: turning point near 00:50 at ~95C,
    /// first crack near 09:00, peak rate of rise around 35 C/min, and a roast that
    /// settles to under 10 C/min by drop. Re-run tools/RoastLab after changing any
    /// of them.
    /// </summary>
    public static RoasterConfig Default { get; } = new();
}
