namespace SlowDrip.Sim.Roasting;

/// <summary>
/// Runs a recorded roast headlessly and returns the log.
/// </summary>
/// <remarks>
/// This is the whole point of keeping the sim engine-free: a roast is a pure
/// function of config, charge, and dial trace, so it can be tuned and regression
/// tested at whatever speed the machine manages rather than at twelve minutes a
/// go.
/// </remarks>
public static class RoastRunner
{
    /// <summary>Drop when the probe reaches <paramref name="temp"/> degC.</summary>
    public static Func<RoastState, bool> DropAtTemp(double temp) => s => s.BeanProbe >= temp;

    /// <summary>Drop when development time reaches <paramref name="ratio"/> of total roast time.</summary>
    public static Func<RoastState, bool> DropAtDevelopmentRatio(double ratio) =>
        s => s.FirstCrack && s.DevelopmentTimeRatio >= ratio;

    /// <summary>
    /// Replay <paramref name="trace"/> until <paramref name="dropWhen"/> fires or
    /// <paramref name="maxSeconds"/> elapses.
    /// </summary>
    /// <param name="sampleInterval">Seconds between recorded samples.</param>
    public static RoastLog Run(
        DialTrace trace,
        RoasterConfig? config = null,
        RoastCharge? charge = null,
        double maxSeconds = 900.0,
        Func<RoastState, bool>? dropWhen = null,
        double sampleInterval = 1.0)
    {
        ArgumentNullException.ThrowIfNull(trace);
        if (maxSeconds <= 0.0) throw new ArgumentOutOfRangeException(nameof(maxSeconds));
        if (sampleInterval <= 0.0) throw new ArgumentOutOfRangeException(nameof(sampleInterval));

        var sim = new RoasterSim(config, charge);
        var log = new RoastLog();

        var totalSteps = (int)Math.Round(maxSeconds / RoasterSim.FixedDt);
        var sampleEvery = Math.Max(1, (int)Math.Round(sampleInterval / RoasterSim.FixedDt));

        // Apply the trace before the first sample, so the log opens with the dial
        // where the player actually set it rather than at zero.
        sim.SetBurner(trace.At(0.0));
        var state = sim.State;
        Record(log, state);

        for (var step = 1; step <= totalSteps; step++)
        {
            sim.SetBurner(trace.At(state.Time));
            sim.Step();
            state = sim.State;

            if (step % sampleEvery == 0) Record(log, state);

            if (dropWhen is not null && dropWhen(state)) break;
        }

        // Always record the final instant, even if it fell between samples.
        if (log.Samples.Count == 0 || log.Samples[^1].Time < state.Time) Record(log, state);

        log.FirstCrackTime = state.FirstCrackTime;
        log.DropTime = state.Time;
        log.DropTemp = state.BeanProbe;
        return log;
    }

    private static void Record(RoastLog log, in RoastState s) => log.Add(new RoastSample(
        s.Time, s.Burner, s.EnvTemp, s.BeanTemp, s.BeanProbe, s.RateOfRise, s.Moisture, s.Phase));
}
