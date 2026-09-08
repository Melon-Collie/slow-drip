namespace SlowDrip.Sim.Roasting;

/// <summary>
/// Something that works the dial. A recorded trace is one; a controller reacting
/// to the curve is another.
/// </summary>
/// <remarks>
/// design.md #9.4 ends with the player saving profiles and handing daily volume
/// to automation while anything new or precious pulls them back to the dial.
/// This is the seam that makes that possible: the sim does not care whether a
/// human, a replay, or a controller is turning the knob.
/// </remarks>
public interface IRoastPilot
{
    /// <summary>Dial position for this instant, given what the roaster can see.</summary>
    double Burner(in RoastState state);

    /// <summary>
    /// Damper position for this instant. Defaults to leaving it where it is, which
    /// for a fresh roast is the setting the machine was characterised at — so a pilot
    /// written before the damper existed still plays the roast it used to.
    /// </summary>
    double Airflow(in RoastState state) => state.Airflow;

    /// <summary>True when the beans should come out.</summary>
    bool Drop(in RoastState state);

    /// <summary>
    /// True when the roast is not worth finishing and the drum should be emptied.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="Drop"/> because they are different acts. Dropping
    /// is the roast working; abandoning is cutting the loss. Defaults to never,
    /// so a recorded trace simply plays out.
    /// </remarks>
    bool Abandon(in RoastState state) => false;
}

/// <summary>Replays a recorded trace and drops at a fixed development ratio.</summary>
public sealed class DialTracePilot : IRoastPilot
{
    private readonly DialTrace _trace;
    private readonly double _dropRatio;

    public DialTracePilot(DialTrace trace, double dropRatio = 0.20)
    {
        _trace = trace ?? throw new ArgumentNullException(nameof(trace));
        _dropRatio = dropRatio;
    }

    public double Burner(in RoastState state) => _trace.At(state.Time);

    public bool Drop(in RoastState state) => state.FirstCrack && state.DevelopmentTimeRatio >= _dropRatio;
}

/// <summary>
/// Plays the roast the way published guidance says to: make the pre-crack gas
/// reduction early enough, keep your hands off the dial across first crack, then
/// step the gas down against development ratio.
/// </summary>
/// <remarks>
/// <para>
/// The protocol this follows is roughly the one taught for washed coffees on drum
/// roasters:
/// </para>
/// <list type="bullet">
/// <item>Reduce the gas around 45s before first crack. Later than that and the
/// reduction lands on top of the crash instead of ahead of it.</item>
/// <item>Change nothing from 45s before to 45s after the start of first crack —
/// touching the dial inside that window deepens the crash.</item>
/// <item>From roughly 45s after first crack, step down: take about 15% off the gas
/// near 12% development, again near 14%, again near 16%.</item>
/// </list>
/// <para>
/// The interesting part is that the first rule needs a prediction. Nothing tells
/// the roaster first crack is 45 seconds away — they extrapolate from bean
/// temperature and rate of rise, which is what this does and what the player will
/// have to do by eye.
/// </para>
/// </remarks>
public sealed class DoctrinePilot : IRoastPilot
{
    private readonly double _preCrackGas;
    private readonly double _dropTemp;
    private readonly double _firstCrackTemp;
    private readonly double _leadSeconds;
    private bool _madePreCrackCut;
    private double _losingSince = -1.0;

    /// <summary>Gas steps through the drying phase, before any prediction matters.</summary>
    private readonly DialTrace _opening;

    /// <param name="leadSeconds">
    /// How far ahead of the predicted crack to make the gas reduction. The taught
    /// value is 45s. Lower it to model a roaster who leaves the cut too late.
    /// </param>
    /// <param name="dropTemp">
    /// Probe temperature to drop at. Roasters drop on colour, which they read off
    /// temperature; development ratio is a diagnostic they check afterwards, not
    /// the trigger. Dropping on ratio instead couples badly — a roast that ran
    /// long then earns a long development and comes out charcoal.
    /// </param>
    public DoctrinePilot(
        DialTrace? opening = null,
        double preCrackGas = 0.44,
        double dropTemp = 213.0,
        double firstCrackTemp = 196.0,
        double leadSeconds = 45.0)
    {
        _opening = opening ?? DefaultOpening;
        _preCrackGas = preCrackGas;
        _dropTemp = dropTemp;
        _firstCrackTemp = firstCrackTemp;
        _leadSeconds = leadSeconds;
    }

    /// <summary>Gas into the charge, then two reductions through drying.</summary>
    /// <remarks>
    /// Not full travel at charge: the drum is already preheated, and leaving headroom
    /// is what lets the later steps still be steps. The last one has to land above the
    /// pre-crack gas, or the "reduction" the protocol turns on is an increase.
    /// </remarks>
    public static DialTrace DefaultOpening { get; } = DialTrace.Of(
        new DialMove(0, 0.85),
        new DialMove(90, 0.66),
        new DialMove(240, 0.48));

    /// <summary>Seconds to first crack as the roaster would estimate it from the curve.</summary>
    public static double PredictedSecondsToCrack(in RoastState state, double firstCrackTemp)
    {
        if (state.FirstCrack) return 0.0;
        var degreesPerSecond = state.RateOfRise / 60.0;
        if (degreesPerSecond <= 1e-6) return double.PositiveInfinity;
        return (firstCrackTemp - state.BeanProbe) / degreesPerSecond;
    }

    public double Burner(in RoastState state)
    {
        // Before first crack: run the opening, then make one reduction as soon as
        // the curve says the crack is about 45 seconds out.
        if (!state.FirstCrack)
        {
            if (!_madePreCrackCut && PredictedSecondsToCrack(state, _firstCrackTemp) <= _leadSeconds)
            {
                _madePreCrackCut = true;
            }

            return _madePreCrackCut ? _preCrackGas : _opening.At(state.Time);
        }

        // Hands off across the crack itself.
        var sinceCrack = state.Time - state.FirstCrackTime;
        if (sinceCrack < 45.0) return _preCrackGas;

        // Then step down against development ratio.
        var dtr = state.DevelopmentTimeRatio;
        var gas = _preCrackGas;
        if (dtr >= 0.12) gas *= 0.85;
        if (dtr >= 0.14) gas *= 0.85;
        if (dtr >= 0.16) gas *= 0.85;
        return gas;
    }

    public bool Drop(in RoastState state) => state.FirstCrack && state.BeanProbe >= _dropTemp;

    /// <summary>
    /// Give up once the beans have been losing heat for a solid minute with the
    /// drop temperature still out of reach.
    /// </summary>
    /// <remarks>
    /// A roaster does not need a stall to be over to know it is happening — the
    /// drum reads colder than the beans and the curve is going the wrong way. The
    /// minute of patience is there so a brief dip across first crack, which is
    /// normal, does not get mistaken for a dead roast.
    /// </remarks>
    public bool Abandon(in RoastState state)
    {
        if (state.BeanProbe >= _dropTemp) return false;

        if (state.NetBeanWatts >= 0.0)
        {
            _losingSince = -1.0;
            return false;
        }

        if (_losingSince < 0.0) _losingSince = state.Time;
        return state.Time - _losingSince >= 60.0;
    }
}
