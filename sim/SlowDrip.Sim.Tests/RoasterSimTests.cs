using SlowDrip.Sim.Roasting;
using Xunit;

namespace SlowDrip.Sim.Tests;

/// <summary>
/// The claims design.md #9.4 makes about the roaster, stated as tests.
/// </summary>
/// <remarks>
/// These assert shapes, not tuning. A threshold here should be loose enough that
/// re-tuning the config does not break it, and tight enough that losing the
/// behaviour does. If a change to <see cref="RoasterConfig"/> turns one of these
/// red, the model stopped making a promise the design depends on.
/// </remarks>
public class RoasterSimTests
{
    private static RoastLog Run(DialTrace trace, RoastCharge? charge = null, double maxSeconds = 900) =>
        RoastRunner.Run(trace, charge: charge, maxSeconds: maxSeconds,
            dropWhen: RoastRunner.DropAtDevelopmentRatio(0.20), sampleInterval: 1.0);

    // ---- Determinism -------------------------------------------------------

    [Fact]
    public void Same_trace_produces_a_bit_identical_roast()
    {
        var a = Run(ReferenceRoasts.Healthy);
        var b = Run(ReferenceRoasts.Healthy);

        Assert.Equal(a.Samples.Count, b.Samples.Count);
        for (var i = 0; i < a.Samples.Count; i++)
        {
            Assert.Equal(a.Samples[i].BeanTemp, b.Samples[i].BeanTemp);
            Assert.Equal(a.Samples[i].BeanProbe, b.Samples[i].BeanProbe);
            Assert.Equal(a.Samples[i].RateOfRise, b.Samples[i].RateOfRise);
        }

        Assert.Equal(a.FirstCrackTime, b.FirstCrackTime);
    }

    [Fact]
    public void Time_advances_exactly_one_fixed_step_at_a_time()
    {
        var sim = new RoasterSim();
        sim.Step(600);
        Assert.Equal(600 * RoasterSim.FixedDt, sim.State.Time, 9);
    }

    // ---- Lag: the whole skill ceiling --------------------------------------

    [Fact]
    public void Drum_answers_the_dial_immediately_but_the_readout_does_not()
    {
        // design.md #9.4: "You turn the dial and nothing happens for ~20 seconds,
        // then bean temp responds to what you did back then." Nothing in the model
        // delays anything on purpose — this is two lags in series plus the probe.
        const double StepAt = 300.0;
        var held = DialTrace.Of(new DialMove(0, 1.00), new DialMove(90, 0.45));
        var stepped = DialTrace.Of(new DialMove(0, 1.00), new DialMove(90, 0.45), new DialMove(StepAt, 0.75));

        var a = RoastRunner.Run(held, maxSeconds: 420, sampleInterval: 0.25);
        var b = RoastRunner.Run(stepped, maxSeconds: 420, sampleInterval: 0.25);
        var pairs = a.Samples.Zip(b.Samples).Where(p => p.First.Time > StepAt).ToArray();

        var envRespondsAt = pairs.First(p => p.Second.EnvTemp - p.First.EnvTemp >= 1.0).Second.Time - StepAt;
        var rorRespondsAt = pairs.First(p => p.Second.RateOfRise - p.First.RateOfRise >= 1.0).Second.Time - StepAt;

        Assert.True(envRespondsAt <= 5.0, $"Drum should answer the dial at once, took {envRespondsAt:F1}s");
        Assert.InRange(rorRespondsAt, 10.0, 40.0);
    }

    [Fact]
    public void Probe_disagrees_with_the_beans_at_charge()
    {
        // The probe was sitting in a preheated drum, so it starts hot while the
        // beans are at room temperature. The player is reading an instrument.
        var sim = new RoasterSim();
        var s = sim.State;

        Assert.Equal(RoasterConfig.Default.ChargeTemp, s.BeanProbe, 6);
        Assert.Equal(RoasterConfig.Default.AmbientTemp, s.BeanTemp, 6);
    }

    [Fact]
    public void Probe_falls_then_turns_around()
    {
        // The turning point. It is a sensor artefact, and it falls out of the
        // model rather than being authored.
        var log = Run(ReferenceRoasts.Healthy);
        var tp = log.TurningPoint;

        Assert.NotNull(tp);
        Assert.InRange(tp!.Value.Time, 20.0, 120.0);
        Assert.InRange(tp.Value.BeanProbe, 70.0, 115.0);

        // True bean temperature never dips — only the reading does.
        var early = log.Samples.Where(s => s.Time <= 180).ToArray();
        for (var i = 1; i < early.Length; i++)
        {
            Assert.True(early[i].BeanTemp >= early[i - 1].BeanTemp - 1e-9,
                $"Bean temp fell at t={early[i].Time}s");
        }
    }

    // ---- The three curve shapes --------------------------------------------

    [Fact]
    public void Healthy_roast_glides_its_rate_of_rise_steadily_downward()
    {
        var log = Run(ReferenceRoasts.Healthy);
        Assert.True(log.ReachedFirstCrack);

        var tp = log.TurningPoint!.Value;
        var peakAt = log.Samples.Where(s => s.Time > tp.Time).OrderByDescending(s => s.RateOfRise).First().Time;
        var glide = log.Samples.Where(s => s.Time > peakAt && s.Time < log.FirstCrackTime).ToArray();

        for (var i = 1; i < glide.Length; i++)
        {
            var rise = glide[i].RateOfRise - glide[i - 1].RateOfRise;
            Assert.True(rise <= 0.5, $"RoR climbed {rise:F2} C/min at t={glide[i].Time}s — not a glide");
        }

        Assert.True(glide[^1].RateOfRise < glide[0].RateOfRise / 2.0,
            "RoR should be well down by first crack");
        Assert.InRange(log.DevelopmentTimeRatio, 0.18, 0.22);
    }

    [Fact]
    public void Baked_roast_flatlines_through_drying_and_cracks_late()
    {
        // Pull the heat too early and the drying phase stops progressing: energy
        // goes into evaporating water instead of raising temperature.
        var baked = Run(ReferenceRoasts.Baked);
        var healthy = Run(ReferenceRoasts.Healthy);

        static double MinRorDuringDrying(RoastLog log) =>
            log.Samples.Where(s => s.Time is > 150 and < 380).Min(s => s.RateOfRise);

        Assert.True(MinRorDuringDrying(baked) < 3.0,
            $"Baked roast should flatline, floor was {MinRorDuringDrying(baked):F1} C/min");
        Assert.True(MinRorDuringDrying(healthy) > 4.0,
            $"Healthy roast should keep climbing, floor was {MinRorDuringDrying(healthy):F1} C/min");

        Assert.True(baked.FirstCrackTime > healthy.FirstCrackTime + 120,
            $"Baked crack at {baked.FirstCrackTime:F0}s vs healthy {healthy.FirstCrackTime:F0}s");
    }

    [Fact]
    public void Cutting_the_gas_crashes_the_rate_of_rise_and_the_exotherm_flicks_it_back()
    {
        // The teeth of the momentum problem. Correcting a runaway does not end it;
        // the beans are generating their own heat by then.
        const double CutAt = 330.0;
        var log = Run(ReferenceRoasts.CrashAndFlick);
        Assert.True(log.ReachedFirstCrack);

        var before = log.Samples.Last(s => s.Time <= CutAt).RateOfRise;
        var post = log.Samples.Where(s => s.Time > CutAt).ToArray();
        var trough = post.Min(s => s.RateOfRise);
        var troughAt = post.First(s => s.RateOfRise <= trough + 1e-9).Time;
        var recovery = post.Where(s => s.Time > troughAt).Max(s => s.RateOfRise);

        Assert.True(trough < before * 0.75,
            $"Expected a crash: RoR went {before:F1} -> {trough:F1} C/min");
        Assert.True(recovery - trough > 4.0,
            $"Expected a flick: RoR recovered only {recovery - trough:F1} C/min from {trough:F1}");
    }

    // ---- Charge properties -------------------------------------------------

    [Fact]
    public void A_profile_dialled_in_on_one_lot_misses_on_another()
    {
        // design.md #9.4: "saved profiles don't transfer cleanly between lots."
        // No special case implements this — the dense lot simply has more thermal
        // mass and a lower conductance, so the same dial trace lands elsewhere.
        var nominal = Run(ReferenceRoasts.Healthy);
        var dense = Run(ReferenceRoasts.Healthy, ReferenceRoasts.DenseLot);

        Assert.True(nominal.ReachedFirstCrack);
        Assert.False(dense.ReachedFirstCrack,
            "The denser, wetter lot should stall short of first crack on last season's profile");
        Assert.True(dense.Samples[^1].BeanProbe < nominal.DropTemp - 30);
    }

    [Fact]
    public void A_heavier_charge_roasts_more_slowly_on_the_same_trace()
    {
        var light = Run(ReferenceRoasts.Healthy, new RoastCharge { DryMassKg = 0.7 });
        var heavy = Run(ReferenceRoasts.Healthy, new RoastCharge { DryMassKg = 1.3 });

        Assert.True(light.ReachedFirstCrack);
        Assert.True(!heavy.ReachedFirstCrack || heavy.FirstCrackTime > light.FirstCrackTime,
            "More mass in the drum must take longer to reach first crack");
    }

    // ---- Moisture and first crack ------------------------------------------

    [Fact]
    public void Moisture_only_ever_leaves_and_never_goes_negative()
    {
        var log = Run(ReferenceRoasts.Scorch);

        for (var i = 1; i < log.Samples.Count; i++)
        {
            Assert.True(log.Samples[i].Moisture <= log.Samples[i - 1].Moisture + 1e-12,
                $"Moisture rose at t={log.Samples[i].Time}s");
            Assert.True(log.Samples[i].Moisture >= 0.0, $"Moisture went negative at t={log.Samples[i].Time}s");
        }

        Assert.True(log.Samples[^1].Moisture < RoastCharge.Default.Moisture / 4.0,
            "A roast to first crack should have driven most of the water off");
    }

    [Fact]
    public void Beans_cannot_crack_while_still_wet()
    {
        var log = Run(ReferenceRoasts.Healthy);
        Assert.True(log.ReachedFirstCrack);

        var atCrack = log.Samples.First(s => s.Time >= log.FirstCrackTime);
        Assert.True(atCrack.Moisture <= RoasterConfig.Default.FirstCrackMaxMoisture + 1e-6,
            $"Cracked at {atCrack.Moisture:F4} moisture");
    }

    [Fact]
    public void Phases_run_in_order_and_never_go_backwards()
    {
        var log = Run(ReferenceRoasts.Healthy);
        var seen = log.Samples.Select(s => (int)s.Phase).ToArray();

        for (var i = 1; i < seen.Length; i++)
        {
            Assert.True(seen[i] >= seen[i - 1],
                $"Phase went backwards at t={log.Samples[i].Time}s: {(RoastPhase)seen[i - 1]} -> {(RoastPhase)seen[i]}");
        }

        Assert.Equal(RoastPhase.Development, log.Samples[^1].Phase);
    }

    // ---- Housekeeping ------------------------------------------------------

    [Fact]
    public void Burner_input_is_clamped()
    {
        var sim = new RoasterSim();
        sim.SetBurner(5.0);
        Assert.Equal(1.0, sim.State.Burner);
        sim.SetBurner(-3.0);
        Assert.Equal(0.0, sim.State.Burner);
    }

    [Fact]
    public void Development_time_ratio_is_unavailable_before_first_crack()
    {
        var sim = new RoasterSim();
        sim.SetBurner(0.5);
        sim.Step(60);
        Assert.Equal(-1.0, sim.State.DevelopmentTimeRatio);
    }

    [Fact]
    public void Csv_export_has_a_row_per_sample()
    {
        var log = Run(ReferenceRoasts.Healthy);
        var lines = log.ToCsv().TrimEnd('\n').Split('\n');

        Assert.Equal(log.Samples.Count + 1, lines.Length);
        Assert.StartsWith("time_s,burner,env_c", lines[0]);
    }
}
