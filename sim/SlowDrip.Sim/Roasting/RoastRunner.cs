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
        double? maxSeconds = null,
        Func<RoastState, bool>? dropWhen = null,
        double sampleInterval = 1.0)
    {
        ArgumentNullException.ThrowIfNull(trace);
        return Run(new DialTracePilot(trace, dropRatio: 0.20), config, charge, maxSeconds,
            dropWhen ?? (_ => false), sampleInterval, useDropWhen: dropWhen is not null);
    }

    /// <summary>Run a controller instead of a recording.</summary>
    public static RoastLog Run(
        IRoastPilot pilot,
        RoasterConfig? config = null,
        RoastCharge? charge = null,
        double? maxSeconds = null,
        double sampleInterval = 1.0)
    {
        ArgumentNullException.ThrowIfNull(pilot);
        return Run(pilot, config, charge, maxSeconds, _ => false, sampleInterval, useDropWhen: false);
    }

    private static RoastLog Run(
        IRoastPilot pilot,
        RoasterConfig? config,
        RoastCharge? charge,
        double? maxSeconds,
        Func<RoastState, bool> dropWhen,
        double sampleInterval,
        bool useDropWhen)
    {
        var cfg = config ?? RoasterConfig.Default;

        // The drum gets emptied one way or another. Left unset this is the config's
        // cap, so a dead roast costs a couple of minutes rather than running out a
        // twenty-minute clock nobody is watching.
        var limit = maxSeconds ?? cfg.MaxRoastSeconds;
        if (limit <= 0.0) throw new ArgumentOutOfRangeException(nameof(maxSeconds));
        if (sampleInterval <= 0.0) throw new ArgumentOutOfRangeException(nameof(sampleInterval));

        var sim = new RoasterSim(cfg, charge);
        var log = new RoastLog();

        var totalSteps = (int)Math.Round(limit / RoasterSim.FixedDt);
        var sampleEvery = Math.Max(1, (int)Math.Round(sampleInterval / RoasterSim.FixedDt));

        // Ask the pilot before the first sample, so the log opens with the dial
        // where the player actually set it rather than at zero.
        var state = sim.State;
        sim.SetBurner(pilot.Burner(state));
        sim.SetAirflow(pilot.Airflow(state));
        state = sim.State;
        Record(log, state);

        for (var step = 1; step <= totalSteps; step++)
        {
            sim.SetBurner(pilot.Burner(state));
            sim.SetAirflow(pilot.Airflow(state));
            sim.Step();
            state = sim.State;

            if (step % sampleEvery == 0) Record(log, state);

            if (useDropWhen ? dropWhen(state) : pilot.Drop(state))
            {
                log.Outcome = RoastOutcome.Dropped;
                break;
            }

            if (pilot.Abandon(state))
            {
                log.Outcome = RoastOutcome.Abandoned;
                break;
            }
        }

        if (log.Outcome == RoastOutcome.Running) log.Outcome = RoastOutcome.TimedOut;

        // Always record the final instant, even if it fell between samples.
        if (log.Samples.Count == 0 || log.Samples[^1].Time < state.Time) Record(log, state);

        log.FirstCrackTime = state.FirstCrackTime;
        log.DropTime = state.Time;
        log.DropTemp = state.BeanProbe;
        return log;
    }

    private static void Record(RoastLog log, in RoastState s) => log.Add(new RoastSample(
        s.Time, s.Burner, s.Airflow, s.EnvTemp, s.BeanTemp, s.BeanProbe, s.RateOfRise,
        s.SurfaceMoisture, s.CoreMoisture, s.ExothermWatts, s.NetBeanWatts,
        s.CrackedFraction, s.PopsPerSecond, s.Phase));
}
