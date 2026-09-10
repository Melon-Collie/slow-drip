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
    /// <remarks>
    /// Sized with <see cref="EnvLossConductance"/> against the roast this machine is
    /// supposed to be able to play: the dial should sit near half travel approaching
    /// first crack and near a third at drop, so the taught pre-crack reduction is a
    /// reduction the machine can actually survive. See sim/README.md.
    /// </remarks>
    public double BurnerPower { get; init; } = 4300.0;

    /// <summary>Effective heat capacity of drum and air (J/K). Sets the burner-to-drum lag.</summary>
    public double EnvHeatCapacity { get; init; } = 2500.0;

    /// <summary>Conductance from drum shell to room (W/K), independent of airflow.</summary>
    /// <remarks>
    /// This and <see cref="ExhaustConductance"/> together are how much of the burner
    /// is spent holding the drum hot rather than roasting, so they decide how much of
    /// the dial's travel is usable. At 250C they cost about 1.5 kW of the 4.3 kW
    /// available when the fan sits at <see cref="AirflowNominal"/>.
    /// </remarks>
    public double EnvLossConductance { get; init; } = 3.0;

    // ---- Airflow -----------------------------------------------------------

    /// <summary>
    /// Fan setting the machine's other constants are characterised at (0..1).
    /// </summary>
    /// <remarks>
    /// Airflow is expressed as a fraction of what the fan can do, and every airflow
    /// term is a ratio against this value — so at exactly this setting the model
    /// reduces to the single-body one it grew out of. It sits below mid-travel
    /// deliberately: a roaster wants room to open the damper as well as close it.
    /// </remarks>
    public double AirflowNominal { get; init; } = 0.60;

    /// <summary>
    /// Share of the bean's heat that arrives by contact with the drum rather than
    /// from the air moving past it.
    /// </summary>
    /// <remarks>
    /// Drum roasters are convection-dominated; the usual figure is two thirds or more
    /// of the heat arriving with the air. The rest is the beans tumbling against a hot
    /// steel wall, which no amount of fan changes. Splitting them is what stops the
    /// damper being a second gas dial: airflow moves the convective half only.
    /// </remarks>
    public double BeanConductionShare { get; init; } = 0.30;

    /// <summary>Exponent relating airflow to the convective heat transfer coefficient.</summary>
    /// <remarks>
    /// Forced convection scales with flow to roughly the 0.8 power across the usual
    /// correlations, so doubling the fan buys well under double the heat. That
    /// diminishing return is half of why the damper has an optimum.
    /// </remarks>
    public double AirflowExponent { get; init; } = 0.80;

    /// <summary>
    /// Heat carried out of the drum by the exhaust at <see cref="AirflowNominal"/> (W/K).
    /// </summary>
    /// <remarks>
    /// The other half of the damper's optimum, and the reason it is not a free
    /// improvement: air moving past the beans faster also leaves faster, and it leaves
    /// hot. This term scales linearly with flow while the convective gain scales at
    /// 0.8, so past some setting the fan costs the drum more than it gives the beans.
    /// </remarks>
    public double ExhaustConductance { get; init; } = 3.5;

    /// <summary>
    /// Share of evaporation that is limited by carrying vapour away rather than by
    /// heat reaching the water.
    /// </summary>
    /// <remarks>
    /// A closed drum saturates: the water is willing to leave and there is nowhere for
    /// it to go. That is the mechanism behind a stuffy roast drying slowly and baking,
    /// and it is why the damper matters most in the phase where nothing else is
    /// happening.
    /// </remarks>
    public double DryingAirflowShare { get; init; } = 0.50;

    // ---- Bean body ---------------------------------------------------------

    /// <summary>Conductance from drum to bean surface (W/K per kg of dry bean).</summary>
    public double BeanConductance { get; init; } = 8.0;

    /// <summary>
    /// Share of the bean's dry mass in the outer shell that the drum heats directly.
    /// </summary>
    /// <remarks>
    /// A bean is not isothermal. Heat arrives at its surface and has to travel
    /// inward, so the outside runs ahead of the inside all roast long. Splitting the
    /// bean is what makes that gradient a quantity rather than an assumption — and
    /// the gradient is what scorching and tipping are, and what the damper was
    /// silently changing with nothing to show for it.
    /// </remarks>
    public double BeanSurfaceFraction { get; init; } = 0.25;

    /// <summary>
    /// Conductance from bean surface to bean core (W/K per kg of dry bean).
    /// </summary>
    /// <remarks>
    /// Coffee is a poor conductor, so this is the term that decides how far the
    /// outside can run ahead of the inside. Low enough and a hard, fast roast burns
    /// the surface while the middle is still raw; high enough and the model collapses
    /// back to the single-node one it grew out of.
    /// <para>
    /// Fitted, and this value is the loose end in the split: it puts about 22C across
    /// the bean at the peak of the drying phase and 3C by first crack. A stiffer bean
    /// — 20 rather than 40 — reads closer to the gradients bean-scale models report at
    /// the crack, but moves the reference roasts enough to need re-deriving, and the
    /// sources that would settle it were not reachable. Worth revisiting alongside the
    /// scorch defect, which is the thing that will actually care.
    /// </para>
    /// </remarks>
    public double BeanInternalConductance { get; init; } = 40.0;

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
    /// <remarks>
    /// This has to be fast enough that the surface pool actually empties. The drive
    /// grows with bean temperature all roast, so if free water is still present late
    /// the evaporation term keeps growing with it and becomes a permanent drain that
    /// no burner setting can outrun — which is what stops the gas ever coming down.
    /// </remarks>
    public double DryingCoefficient { get; init; } = 6.0e-5;

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
    /// Share of a ruptured bean's core water that flashes straight to steam rather
    /// than joining the surface pool (0..1).
    /// </summary>
    /// <remarks>
    /// This is the mechanism behind the RoR crash. At first crack the beans release
    /// a great deal of moisture from their cores in a short period, and that
    /// moisture is cooler than the bean surface and the probe — so the readout drops
    /// whether or not the roaster did anything.
    /// <para>
    /// All of a ruptured bean's core water leaves the core, because a broken bean
    /// has no intact core to hold it. This fraction decides how much of it leaves
    /// <i>as steam, now</i>, paying its latent heat at the instant of rupture. The
    /// rest becomes free water on a broken bean — the surface pool — and evaporates
    /// on that pool's slower schedule. The split is what makes the crash a cliff
    /// rather than a slightly steeper part of the glide.
    /// </para>
    /// <para>
    /// Venting is tied to the rate beans are actually rupturing, not to how many
    /// have ruptured so far, because a bean lets go once. That makes the shape of
    /// the crash the shape of the crackle — and the sorted lot comes off better on
    /// both counts. It cracks later and drier, so there is less water to lose, and
    /// it gets the loss over with in under two minutes. The ragged lot starts
    /// cracking earlier and wetter and then bleeds for four, draining the roast the
    /// whole time the gas is already down. A sharp crash is cheap; a long one is
    /// what costs you the development ratio.
    /// </para>
    /// <para>
    /// Fitted, and sensitive: the latent heat of the core water is large next to
    /// everything else moving at first crack, so this is a steep dial. On the
    /// reference roast the flash supplies rather more than a quarter of the dip;
    /// the rest is the pre-crack reduction and the released water evaporating.
    /// </para>
    /// </remarks>
    public double RuptureVentFraction { get; init; } = 0.40;

    /// <summary>Time constant over which a ruptured bean finishes venting (s).</summary>
    /// <remarks>
    /// A bean does not empty in one timestep; the fracture opens and the steam leaves
    /// over a moment. Modelling it as instantaneous also quantises the vent by the
    /// integer number of beans that happen to cross their threshold in a given tick,
    /// which at a few tens of pops per second is one or zero — so the reported energy
    /// balance alternates between a spike and a hole while the bean temperature, which
    /// integrates it, is perfectly smooth. Draining a pool fixes both, and the pool is
    /// the more honest picture anyway.
    /// </remarks>
    public double RuptureVentTime { get; init; } = 1.5;

    // ---- Exotherm ----------------------------------------------------------

    /// <summary>
    /// Total heat available from the roast reactions (J per kg of dry bean).
    /// </summary>
    /// <remarks>
    /// Calorimetry of green coffee heated to 300C gives 250–420 kJ/kg. Most of
    /// that sits above normal drop temperatures, so only a fraction is released
    /// before the beans come out — the model tracks the unreacted share and spends
    /// roughly a tenth of this budget by drop, which is what "most of it sits above
    /// drop temperature" actually implies.
    /// </remarks>
    public double ExothermEnergy { get; init; } = 350_000.0;

    /// <summary>Fraction of the remaining reactant consumed per second at 200 degC (1/s).</summary>
    /// <remarks>
    /// Fitted, and the single most consequential number in the file: it decides
    /// whether the player's dial still matters after first crack. Self-heating and
    /// the drum-to-bean path are comparable in a real roaster, which is why a roaster
    /// can reduce gas across the crack at all. Set this much higher and the beans
    /// heat themselves against a drum that has gone colder than they are — the roast
    /// finishes regardless of the dial, and development stops being played.
    /// </remarks>
    public double ExothermRateAt200C { get; init; } = 4.0e-4;

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
    /// The reference machine. Derived as a whole against published targets rather
    /// than fitted knob by knob: turning point near 01:00 at 80-95C, a rate of rise
    /// gliding to roughly 11 degC/min by mid-roast and 8 at first crack, first crack
    /// near 09:20, a drop near 213C at a development ratio near 25%, and the drum
    /// staying hotter than the beans the whole way. Re-run tools/RoastLab after
    /// changing any of it.
    /// </summary>
    public static RoasterConfig Default { get; } = new();
}
