namespace SlowDrip.Sim.Roasting;

/// <summary>
/// Machine and physics constants for the drum roaster.
/// </summary>
/// <remarks>
/// <para>
/// Every field here is a tuning knob, not a law. The structure is the part to
/// hold onto: two coupled thermal bodies, a two-pool moisture model, a depleting
/// exothermic source, and a lagged sensor. The ~20s dead time of design.md #9.4
/// is not stored anywhere in this type — it emerges from the composition.
/// </para>
/// <para>
/// Where a constant has a basis in the roasting literature it says so. Where it
/// was tuned until the curve looked right, it says that instead.
/// </para>
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
    public double BeanConductance { get; init; } = 8.0;

    /// <summary>
    /// Specific heat of dry coffee solids (J/(kg*K)).
    /// </summary>
    /// <remarks>
    /// Measurements of green coffee land between 1.0 and 1.9 kJ/(kg*K), with
    /// calorimetry putting it near 1.40 at room temperature and 1.45 once the
    /// roast reactions have finished. Held constant here; the variation across
    /// the roast is smaller than the uncertainty in the conductances.
    /// </remarks>
    public double BeanSpecificHeat { get; init; } = 1450.0;

    /// <summary>Specific heat of water (J/(kg*K)).</summary>
    public double WaterSpecificHeat { get; init; } = 4186.0;

    // ---- Moisture: two pools ------------------------------------------------

    /// <summary>Latent heat of vaporisation of water (J/kg).</summary>
    public double LatentHeatOfVaporisation { get; init; } = 2.26e6;

    /// <summary>Bean temperature above which moisture starts moving (degC).</summary>
    public double DryingOnset { get; init; } = 60.0;

    /// <summary>
    /// Evaporation rate coefficient (1/(K*s)), applied to surface moisture times
    /// the drive above <see cref="DryingOnset"/>.
    /// </summary>
    public double DryingCoefficient { get; init; } = 3.8e-5;

    /// <summary>
    /// Rate at which core moisture migrates out to the surface (1/(K*s)).
    /// </summary>
    /// <remarks>
    /// Much slower than surface evaporation, which is the point: the core dries
    /// on its own schedule, and a roast rushed through drying arrives at first
    /// crack with the core still wet.
    /// </remarks>
    public double CoreMigrationCoefficient { get; init; } = 3.0e-5;

    /// <summary>
    /// Share of its remaining core water a bean lets go at the moment it ruptures
    /// (0..1).
    /// </summary>
    /// <remarks>
    /// This is the mechanism behind the RoR crash. At first crack the beans vent a
    /// great deal of moisture from their cores in a short period, and that moisture
    /// is cooler than the bean surface and the probe — so the readout drops whether
    /// or not the roaster did anything.
    /// <para>
    /// Venting is tied to the rate beans are actually rupturing, not to how many
    /// have ruptured so far, because a bean lets go once. That makes the shape of
    /// the crash the shape of the crackle — and the sorted lot comes off better on
    /// both counts. It cracks later and drier, so there is less water to lose, and
    /// it gets the loss over with in forty seconds. The ragged lot starts cracking
    /// earlier and wetter and then bleeds for three minutes, draining the roast the
    /// whole time the gas is already down. A sharp crash is survivable; a long one
    /// is what stalls you.
    /// </para>
    /// </remarks>
    public double RuptureVentFraction { get; init; } = 0.85;

    // ---- Exotherm ----------------------------------------------------------

    /// <summary>
    /// Total heat available from the roast reactions (J per kg of dry bean).
    /// </summary>
    /// <remarks>
    /// Calorimetry of green coffee heated to 300C gives 250–420 kJ/kg. Most of
    /// that sits above normal drop temperatures, so only a fraction is released
    /// before the beans come out — the model tracks the unreacted share and
    /// typically spends a quarter to a third of this budget by drop.
    /// </remarks>
    public double ExothermEnergy { get; init; } = 350_000.0;

    /// <summary>Fraction of the remaining reactant consumed per second at 200 degC (1/s).</summary>
    public double ExothermRateAt200C { get; init; } = 1.9e-3;

    /// <summary>Activation energy for the roast reactions (J/mol).</summary>
    /// <remarks>
    /// Sets how sharply self-heating switches on with temperature. At this value
    /// the reactions are barely measurable at 150C — where the literature puts
    /// their onset — and roughly twenty times faster by 210C.
    /// </remarks>
    public double ExothermActivationEnergy { get; init; } = 100_000.0;

    /// <summary>Universal gas constant (J/(mol*K)).</summary>
    public double GasConstant { get; init; } = 8.314;

    // ---- First crack -------------------------------------------------------

    /// <summary>
    /// Bean temperature the roaster expects first crack around (degC).
    /// </summary>
    /// <remarks>
    /// A nominal, not a fact. What the batch actually does is decided per bean by
    /// <see cref="RoastCharge.CrackTempMean"/> and its spread. This is the number
    /// a pilot extrapolates against, so a lot that cracks off-nominal is a lot the
    /// roaster mistimes — which is the right way round.
    /// </remarks>
    public double FirstCrackTemp { get; init; } = 196.0;

    /// <summary>
    /// Share of the batch that has to have ruptured before a roaster would call it.
    /// </summary>
    /// <remarks>
    /// One bean popping is not first crack; it is one bean popping. Roasters call
    /// it when the pops become a sound rather than an event.
    /// <para>
    /// This threshold moves the development time ratio around, which is worth
    /// knowing before treating that ratio as an absolute. Roasters have the same
    /// problem: "when did first crack start" is a genuinely contested reading, and
    /// a lot with a ragged screen size makes it worse by popping stragglers half a
    /// minute before the batch really goes.
    /// </para>
    /// </remarks>
    public double FirstCrackAudibleFraction { get; init; } = 0.05;

    /// <summary>
    /// How much hotter a wet batch has to get before it will rupture
    /// (degC per unit of moisture above <see cref="CrackDryReference"/>).
    /// </summary>
    /// <remarks>
    /// Replaces what used to be a hard moisture gate. The gate held every bean
    /// back until the batch dried, by which point they were all well past their
    /// rupture temperature — so they all cracked in the same instant and the
    /// crackle had no duration at all. A penalty that falls as the batch dries
    /// lets the population cross its thresholds progressively, which is what
    /// gives first crack a shape.
    /// </remarks>
    public double MoistureCrackPenalty { get; init; } = 190.0;

    /// <summary>Moisture at or below which no penalty applies (dry basis).</summary>
    public double CrackDryReference { get; init; } = 0.020;

    /// <summary>
    /// Bean temperature at which the drying phase gives way to browning (degC).
    /// </summary>
    /// <remarks>
    /// The colour change roasters call yellowing. Conventionally placed at 150C.
    /// </remarks>
    public double DryingEndTemp { get; init; } = 150.0;

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

    /// <summary>Window the RoR finite difference is taken over (s).</summary>
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
    /// Longest a roast is allowed to run before the drum is emptied regardless (s).
    /// </summary>
    /// <remarks>
    /// Nobody stands and watches a dead batch for twenty minutes. A real roaster
    /// dumps it and charges the next one, so a failed roast should cost a couple of
    /// minutes of the player's patience rather than the same wall-clock time as a
    /// good one. This is the one fairness fix that costs nothing in realism —
    /// it is letting the player do what a person would already do.
    /// </remarks>
    public double MaxRoastSeconds { get; init; } = 900.0;

    /// <summary>
    /// The reference machine. Tuned on the reference dial trace against published
    /// targets: turning point near 00:50, a rate of rise gliding to roughly
    /// 10 degC/min by mid-roast and 5 degC/min at first crack, first crack near
    /// 09:00, and a drop near 210C. Re-run tools/RoastLab after changing any of it.
    /// </summary>
    public static RoasterConfig Default { get; } = new();
}
