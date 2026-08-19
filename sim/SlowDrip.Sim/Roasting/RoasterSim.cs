namespace SlowDrip.Sim.Roasting;

/// <summary>
/// The drum roaster: two coupled thermal bodies, an evaporation sink, an
/// exothermic source, and a lagged probe. Deterministic, fixed timestep, and
/// free of any engine type (design.md #15).
/// </summary>
/// <remarks>
/// <para>
/// Nothing in here delays an input on purpose. The dead time of design.md #9.4
/// — turn the dial, wait, then watch the curve answer — is what two lags in
/// series do. One body would give a slow response that still starts
/// immediately, which reads as sluggish rather than as momentum.
/// </para>
/// <para>
/// The three terms that carry the design:
/// <list type="bullet">
/// <item>Evaporation is the drying phase. While free moisture remains, burner
/// energy goes into phase change instead of temperature, so a flat RoR through
/// drying is a physical outcome and not a scripted failure.</item>
/// <item>The exotherm is the teeth. Cut the burner to stop a runaway and RoR
/// crashes, then self-heating flicks it back up.</item>
/// <item>Thermal mass comes from the charge, so a profile that suited one lot
/// misses on the next.</item>
/// </list>
/// </para>
/// </remarks>
public sealed class RoasterSim
{
    /// <summary>The one true timestep. Determinism depends on every step being this long.</summary>
    public const double FixedDt = 1.0 / 60.0;

    private readonly RoasterConfig _cfg;
    private readonly RoastCharge _charge;
    private readonly RateOfRiseMeter _ror;

    private double _time;
    private double _burner;
    private double _envTemp;
    private double _beanTemp;
    private double _beanProbe;
    private double _moisture;
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
        _moisture = _charge.Moisture;

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
        Moisture = _moisture,
        FirstCrack = _firstCrack,
        FirstCrackTime = _firstCrackTime,
        Phase = CurrentPhase(),
    };

    /// <summary>Advance exactly one <see cref="FixedDt"/>.</summary>
    public void Step()
    {
        var dt = FixedDt;
        var dryMass = _charge.DryMassKg;

        // Bean thermal mass, water included. Wetter beans are heavier to move.
        var beanCapacity = dryMass * (_cfg.BeanSpecificHeat + _moisture * _cfg.WaterSpecificHeat);

        // Denser beans take heat more slowly for the same mass.
        var beanConductance = _cfg.BeanConductance * dryMass / _charge.DensityFactor;

        var qBurner = _burner * _cfg.BurnerPower;
        var qEnvToBean = beanConductance * (_envTemp - _beanTemp);
        var qEnvLoss = _cfg.EnvLossConductance * (_envTemp - _cfg.AmbientTemp);

        // Evaporation. Rate scales with how much water is left and how far the
        // beans are past the drying onset, so drying tapers instead of stopping.
        var qEvaporation = 0.0;
        var evaporated = 0.0;
        if (_moisture > 0.0)
        {
            var drive = Math.Max(0.0, _beanTemp - _cfg.DryingOnset);
            var rate = _cfg.DryingCoefficient * _moisture * drive; // per second, dry basis
            evaporated = Math.Min(_moisture, rate * dt);
            qEvaporation = evaporated / dt * dryMass * _cfg.LatentHeatOfVaporisation;
        }

        var qExotherm = Exotherm(_beanTemp, dryMass);

        var dEnv = (qBurner - qEnvToBean - qEnvLoss) / _cfg.EnvHeatCapacity;
        var dBean = (qEnvToBean + qExotherm - qEvaporation) / beanCapacity;

        _envTemp += dEnv * dt;
        _beanTemp += dBean * dt;
        _moisture -= evaporated;

        // Sensor lag, against a reading that is mostly bean and partly drum.
        // Semi-implicit so the probe cannot overshoot at large dt.
        var probeTarget = (1.0 - _cfg.ProbeEnvBleed) * _beanTemp + _cfg.ProbeEnvBleed * _envTemp;
        var previousProbe = _beanProbe;
        _beanProbe += (probeTarget - _beanProbe) * (dt / (_cfg.ProbeTimeConstant + dt));

        // The turning point latches. Late in the roast the drum bleed can lift the
        // probe back above true bean temperature, which is not a second charge.
        if (!_pastTurningPoint && _beanProbe > previousProbe) _pastTurningPoint = true;

        if (!_firstCrack && _beanTemp >= _cfg.FirstCrackTemp && _moisture <= _cfg.FirstCrackMaxMoisture)
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

    private double Exotherm(double beanTemp, double dryMass)
    {
        var x = (beanTemp - _cfg.ExothermOnset) / _cfg.ExothermWidth;
        // Guard the exponential so a cold start cannot overflow.
        if (x < -40.0) return 0.0;
        return _cfg.ExothermPower * dryMass / (1.0 + Math.Exp(-x));
    }

    private RoastPhase CurrentPhase()
    {
        if (_firstCrack) return RoastPhase.Development;
        if (!_pastTurningPoint) return RoastPhase.Charge;
        return _moisture > _cfg.FirstCrackMaxMoisture ? RoastPhase.Drying : RoastPhase.Maillard;
    }
}
