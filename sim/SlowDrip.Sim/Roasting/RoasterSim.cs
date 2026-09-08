namespace SlowDrip.Sim.Roasting;

/// <summary>
/// The drum roaster: two coupled thermal bodies, moisture in two pools, a
/// depleting exothermic source, and a lagged probe. Deterministic, fixed
/// timestep, and free of any engine type (design.md #15).
/// </summary>
/// <remarks>
/// <para>
/// Nothing in here delays an input on purpose. The dead time of design.md #9.4
/// — turn the dial, wait, then watch the curve answer — is what two lags in
/// series do. One body would give a slow response that still starts
/// immediately, which reads as sluggish rather than as momentum.
/// </para>
/// <para>
/// The terms that carry the design:
/// <list type="bullet">
/// <item><b>Surface moisture</b> is the drying phase. While it remains, burner
/// energy goes into phase change instead of temperature, so a flat RoR through
/// drying is a physical outcome and not a scripted failure.</item>
/// <item><b>Core moisture</b> is the debt. It migrates out slowly, and whatever
/// is left when the bean ruptures vents in a rush and crashes the RoR. Rush the
/// drying phase and you pay for it at first crack, minutes later.</item>
/// <item><b>The exotherm</b> is the teeth, and it depletes. Cut the gas to catch
/// a runaway and the RoR crashes, then self-heating flicks it back up — but the
/// reactant is finite, so the flick is a bump and not an escape.</item>
/// <item><b>Thermal mass</b> comes from the charge, so a profile that suited one
/// lot misses on the next.</item>
/// </list>
/// </para>
/// </remarks>
public sealed class RoasterSim
{
    /// <summary>The one true timestep. Determinism depends on every step being this long.</summary>
    public const double FixedDt = 1.0 / 60.0;

    private const double KelvinOffset = 273.15;
    private const double ReferenceTempKelvin = 200.0 + KelvinOffset;

    private readonly RoasterConfig _cfg;
    private readonly RoastCharge _charge;
    private readonly RateOfRiseMeter _ror;
    private readonly BeanPopulation _beans;

    private double _time;
    private double _burner;
    private double _airflow;
    private double _envTemp;
    private double _beanTemp;
    private double _beanProbe;
    private double _surfaceMoisture;
    private double _coreMoisture;
    private double _ventingMoisture;
    private double _reactantRemaining = 1.0;
    private double _exothermWatts;
    private double _netBeanWatts;
    private double _popRate;
    private bool _firstCrack;
    private double _firstCrackTime = -1.0;
    private bool _pastTurningPoint;

    public RoasterSim(RoasterConfig? config = null, RoastCharge? charge = null)
    {
        _cfg = config ?? RoasterConfig.Default;
        _charge = charge ?? RoastCharge.Default;
        _ror = new RateOfRiseMeter(_cfg.RorWindow, FixedDt, _cfg.RorSmoothing);
        _beans = new BeanPopulation(_charge.BeanCount, _charge.CrackTempMean, _charge.CrackTempSpread);

        // Start the fan where the machine was characterised, so a pilot that never
        // touches the damper gets the single-body model this grew out of.
        _airflow = _cfg.AirflowNominal;

        _envTemp = _cfg.ChargeTemp;
        _beanTemp = _cfg.AmbientTemp;
        _coreMoisture = _charge.Moisture * _charge.CoreMoistureFraction;
        _surfaceMoisture = _charge.Moisture - _coreMoisture;

        // The probe was sitting in a preheated empty drum, so it starts hot and
        // falls as the beans reach it. That artefact is the turning point.
        _beanProbe = _cfg.ChargeTemp;
        _ror.Push(_beanProbe);
    }

    /// <summary>Set the dial. Clamped to 0..1. Takes effect on the next step.</summary>
    public void SetBurner(double value) => _burner = Math.Clamp(value, 0.0, 1.0);

    /// <summary>Set the damper, as a fraction of what the fan can do. Clamped to 0..1.</summary>
    /// <remarks>
    /// Opening it moves more heat into the beans and more heat out of the exhaust at
    /// the same time. The two do not scale together, which is what makes this a
    /// separate control rather than a second way to spell "more gas".
    /// </remarks>
    public void SetAirflow(double value) => _airflow = Math.Clamp(value, 0.0, 1.0);

    /// <summary>Current state, safe to hand to presentation.</summary>
    public RoastState State => new()
    {
        Time = _time,
        Burner = _burner,
        Airflow = _airflow,
        EnvTemp = _envTemp,
        BeanTemp = _beanTemp,
        BeanProbe = _beanProbe,
        RateOfRise = _ror.Value,
        SurfaceMoisture = _surfaceMoisture + _ventingMoisture,
        CoreMoisture = _coreMoisture,
        ReactantRemaining = _reactantRemaining,
        ExothermWatts = _exothermWatts,
        NetBeanWatts = _netBeanWatts,
        CrackedFraction = _beans.CrackedFraction,
        PopsPerSecond = _popRate,
        FirstCrack = _firstCrack,
        FirstCrackTime = _firstCrackTime,
        Phase = CurrentPhase(),
    };

    /// <summary>Advance exactly one <see cref="FixedDt"/>.</summary>
    public void Step()
    {
        var dt = FixedDt;
        var dryMass = _charge.DryMassKg;
        var moisture = _surfaceMoisture + _coreMoisture;

        // Bean thermal mass, water included. Wetter beans are heavier to move.
        var beanCapacity = dryMass * (_cfg.BeanSpecificHeat + moisture * _cfg.WaterSpecificHeat);

        // Airflow, as a ratio against the setting the machine was characterised at.
        // Convection scales sublinearly with it; the exhaust it drives scales linearly.
        // Everything below is 1.0 at nominal, which is what keeps this a superset of
        // the single-body model rather than a retune of it.
        var flowRatio = _cfg.AirflowNominal <= 0.0 ? 1.0 : _airflow / _cfg.AirflowNominal;
        var convectionScale = Math.Pow(flowRatio, _cfg.AirflowExponent);

        // Denser beans take heat more slowly for the same mass. The contact half of
        // that path is the beans against the drum wall and does not care about the fan.
        var beanConductance = _cfg.BeanConductance * dryMass / _charge.DensityFactor
            * (_cfg.BeanConductionShare + (1.0 - _cfg.BeanConductionShare) * convectionScale);

        var qBurner = _burner * _cfg.BurnerPower;
        var qEnvToBean = beanConductance * (_envTemp - _beanTemp);
        var qEnvLoss = (_cfg.EnvLossConductance + _cfg.ExhaustConductance * flowRatio)
            * (_envTemp - _cfg.AmbientTemp);

        var drive = Math.Max(0.0, _beanTemp - _cfg.DryingOnset);

        // Beans rupture individually. A wet batch resists: the structure is not
        // brittle yet and the extra water is extra mass to heat, so the whole
        // population's thresholds sit higher and come down as it dries.
        var uncrackedBefore = _beans.Count - _beans.Cracked;
        var wetness = Math.Max(0.0, _surfaceMoisture + _coreMoisture - _cfg.CrackDryReference);
        var popped = _beans.CrackUpTo(_beanTemp - _cfg.MoistureCrackPenalty * wetness);

        // Core moisture leaves two ways, and they are different events.
        //
        // An intact bean migrates it outward slowly, where it joins the surface
        // pool and evaporates on that pool's schedule. A bean that ruptures flashes
        // its share straight to steam: the water was superheated and above
        // atmospheric pressure inside the bean, so the instant the structure fails
        // it leaves, taking its latent heat with it right then. That immediacy is
        // the crash. Routing the vent through the surface pool instead spreads the
        // same energy over the following minute and a half, and what should be a
        // cliff arrives as a slightly steeper part of the glide.
        var migrated = 0.0;
        var flashed = 0.0;
        if (_coreMoisture > 0.0)
        {
            var rate = _cfg.CoreMigrationCoefficient * _coreMoisture * drive;
            migrated = Math.Min(_coreMoisture, rate * dt);
            _coreMoisture -= migrated;
            _surfaceMoisture += migrated;

            if (popped > 0 && uncrackedBefore > 0)
            {
                // The water still in the core belongs to the beans still intact, so
                // each one that goes takes its share of it with it. All of that share
                // leaves the core pool — a ruptured bean has no intact core left to
                // hold it — and only the vented part is on its way out as steam; the
                // rest is now free water on a broken bean, which is the surface pool.
                var share = (double)popped / uncrackedBefore;
                var released = Math.Min(_coreMoisture, _coreMoisture * share);
                var venting = released * _cfg.RuptureVentFraction;
                _coreMoisture -= released;
                _surfaceMoisture += released - venting;
                _ventingMoisture += venting;
            }
        }

        // The venting pool drains rather than emptying in the tick the bean popped.
        if (_ventingMoisture > 0.0)
        {
            flashed = _ventingMoisture * (dt / (_cfg.RuptureVentTime + dt));
            _ventingMoisture -= flashed;
        }

        // Water leaving costs latent heat whichever way it goes: slowly off the
        // surface all roast long, and in a rush out of every bean that ruptures.
        var evaporated = 0.0;
        if (_surfaceMoisture > 0.0)
        {
            // Part of drying is heat reaching the water and part is carrying the vapour
            // away; only the second half answers the damper. A shut drum saturates.
            var rate = _cfg.DryingCoefficient * _surfaceMoisture * drive
                * (1.0 - _cfg.DryingAirflowShare + _cfg.DryingAirflowShare * convectionScale);
            evaporated = Math.Min(_surfaceMoisture, rate * dt);
        }

        var qEvaporation = (evaporated + flashed) / dt * dryMass * _cfg.LatentHeatOfVaporisation;

        // Self-heating, with the reactant it consumes tracked so the roast cannot
        // generate heat for ever.
        var reacted = 0.0;
        _exothermWatts = 0.0;
        if (_reactantRemaining > 0.0)
        {
            var k = ExothermRateConstant(_beanTemp);
            reacted = Math.Min(_reactantRemaining, k * _reactantRemaining * dt);
            _exothermWatts = reacted / dt * _cfg.ExothermEnergy * dryMass;
            _reactantRemaining -= reacted;
        }

        _netBeanWatts = qEnvToBean + _exothermWatts - qEvaporation;

        var dEnv = (qBurner - qEnvToBean - qEnvLoss) / _cfg.EnvHeatCapacity;
        var dBean = _netBeanWatts / beanCapacity;

        _envTemp += dEnv * dt;
        _beanTemp += dBean * dt;
        _surfaceMoisture -= evaporated;

        // Sensor lag, against a reading that is mostly bean and partly drum.
        // Semi-implicit so the probe cannot overshoot at large dt.
        var probeTarget = (1.0 - _cfg.ProbeEnvBleed) * _beanTemp + _cfg.ProbeEnvBleed * _envTemp;
        var previousProbe = _beanProbe;
        _beanProbe += (probeTarget - _beanProbe) * (dt / (_cfg.ProbeTimeConstant + dt));

        // The turning point latches. Late in the roast the drum bleed can lift the
        // probe back above true bean temperature, which is not a second charge.
        if (!_pastTurningPoint && _beanProbe > previousProbe) _pastTurningPoint = true;

        // Smoothed a little so the readout is a rate rather than a per-tick count.
        // Presentation turns this into scattered pops; the scatter belongs there.
        var instantRate = popped / dt;
        _popRate += (instantRate - _popRate) * (dt / (0.5 + dt));

        if (!_firstCrack && _beans.CrackedFraction >= _cfg.FirstCrackAudibleFraction)
        {
            _firstCrack = true;
            _firstCrackTime = _time;
        }

        _time += dt;
        _ror.Push(_beanProbe);
    }

    /// <summary>Advance a whole number of fixed steps.</summary>
    public void Step(int steps)
    {
        if (steps < 0) throw new ArgumentOutOfRangeException(nameof(steps));
        for (var i = 0; i < steps; i++) Step();
    }

    /// <summary>
    /// Arrhenius rate constant for the roast reactions, referenced at 200 degC.
    /// </summary>
    private double ExothermRateConstant(double beanTempC)
    {
        var t = beanTempC + KelvinOffset;
        if (t < 1.0) return 0.0;

        var exponent = -_cfg.ExothermActivationEnergy / _cfg.GasConstant * (1.0 / t - 1.0 / ReferenceTempKelvin);
        if (exponent < -40.0) return 0.0;
        if (exponent > 40.0) exponent = 40.0;
        return _cfg.ExothermRateAt200C * Math.Exp(exponent);
    }

    private RoastPhase CurrentPhase()
    {
        if (_firstCrack) return RoastPhase.Development;
        if (!_pastTurningPoint) return RoastPhase.Charge;
        return _beanTemp < _cfg.DryingEndTemp ? RoastPhase.Drying : RoastPhase.Maillard;
    }
}
