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

    private double _time;
    private double _burner;
    private double _envTemp;
    private double _beanTemp;
    private double _beanProbe;
    private double _surfaceMoisture;
    private double _coreMoisture;
    private double _reactantRemaining = 1.0;
    private double _exothermWatts;
    private bool _firstCrack;
    private double _firstCrackTime = -1.0;
    private bool _pastTurningPoint;

    public RoasterSim(RoasterConfig? config = null, RoastCharge? charge = null)
    {
        _cfg = config ?? RoasterConfig.Default;
        _charge = charge ?? RoastCharge.Default;
        _ror = new RateOfRiseMeter(_cfg.RorWindow, FixedDt, _cfg.RorSmoothing);

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

    /// <summary>Current state, safe to hand to presentation.</summary>
    public RoastState State => new()
    {
        Time = _time,
        Burner = _burner,
        EnvTemp = _envTemp,
        BeanTemp = _beanTemp,
        BeanProbe = _beanProbe,
        RateOfRise = _ror.Value,
        SurfaceMoisture = _surfaceMoisture,
        CoreMoisture = _coreMoisture,
        ReactantRemaining = _reactantRemaining,
        ExothermWatts = _exothermWatts,
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

        // Denser beans take heat more slowly for the same mass.
        var beanConductance = _cfg.BeanConductance * dryMass / _charge.DensityFactor;

        var qBurner = _burner * _cfg.BurnerPower;
        var qEnvToBean = beanConductance * (_envTemp - _beanTemp);
        var qEnvLoss = _cfg.EnvLossConductance * (_envTemp - _cfg.AmbientTemp);

        var drive = Math.Max(0.0, _beanTemp - _cfg.DryingOnset);

        // Core moisture works its way out to the surface. Once the bean has
        // ruptured it stops being a slow migration and becomes a vent.
        var migrated = 0.0;
        if (_coreMoisture > 0.0)
        {
            var rate = _cfg.CoreMigrationCoefficient * _coreMoisture * drive;
            if (_firstCrack) rate *= _cfg.FirstCrackMoistureRelease;
            migrated = Math.Min(_coreMoisture, rate * dt);
            _coreMoisture -= migrated;
            _surfaceMoisture += migrated;
        }

        // Only surface moisture evaporates, and only evaporation costs latent heat.
        var qEvaporation = 0.0;
        var evaporated = 0.0;
        if (_surfaceMoisture > 0.0)
        {
            var rate = _cfg.DryingCoefficient * _surfaceMoisture * drive; // per second, dry basis
            evaporated = Math.Min(_surfaceMoisture, rate * dt);
            qEvaporation = evaporated / dt * dryMass * _cfg.LatentHeatOfVaporisation;
        }

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

        var dEnv = (qBurner - qEnvToBean - qEnvLoss) / _cfg.EnvHeatCapacity;
        var dBean = (qEnvToBean + _exothermWatts - qEvaporation) / beanCapacity;

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

        if (!_firstCrack
            && _beanTemp >= _cfg.FirstCrackTemp
            && _surfaceMoisture + _coreMoisture <= _cfg.FirstCrackMaxMoisture)
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
