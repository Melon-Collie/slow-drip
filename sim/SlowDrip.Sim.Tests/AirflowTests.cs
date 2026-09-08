using SlowDrip.Sim.Roasting;
using Xunit;

namespace SlowDrip.Sim.Tests;

/// <summary>
/// The damper has to earn its place as a control. An earlier version of this model
/// swept airflow as one multiplier over everything it touches and found it
/// near-redundant with the gas dial; these are the claims that say the split
/// version is not.
/// </summary>
public class AirflowTests
{
    /// <summary>Plays the reference protocol with the damper pinned.</summary>
    private sealed class DamperPilot : IRoastPilot
    {
        private readonly DoctrinePilot _inner;
        private readonly double _airflow;

        public DamperPilot(double airflow, double gas = ReferenceRoasts.PreCrackGas)
        {
            _inner = new DoctrinePilot(preCrackGas: gas);
            _airflow = airflow;
        }

        public double Burner(in RoastState state) => _inner.Burner(state);
        public double Airflow(in RoastState state) => _airflow;
        public bool Drop(in RoastState state) => _inner.Drop(state);
        public bool Abandon(in RoastState state) => _inner.Abandon(state);
    }

    private static RoastLog Run(double airflow, double gas = ReferenceRoasts.PreCrackGas) =>
        RoastRunner.Run(new DamperPilot(airflow, gas), sampleInterval: 1.0);

    private static RoastSample AtCrack(RoastLog log) =>
        log.Samples.First(s => s.Time >= log.FirstCrackTime);

    [Fact]
    public void A_pilot_that_never_touches_the_damper_gets_the_machine_as_characterised()
    {
        // Every airflow term is a ratio against AirflowNominal, so the damper is a
        // superset of the single-body model rather than a retune of it. If this
        // fails, landing the damper silently moved every reference roast.
        var untouched = RoastRunner.Run(ReferenceRoasts.Textbook(), sampleInterval: 1.0);
        var pinned = Run(RoasterConfig.Default.AirflowNominal);

        Assert.Equal(untouched.Samples.Count, pinned.Samples.Count);
        for (var i = 0; i < untouched.Samples.Count; i++)
        {
            Assert.Equal(untouched.Samples[i].BeanTemp, pinned.Samples[i].BeanTemp, 9);
            Assert.Equal(untouched.Samples[i].EnvTemp, pinned.Samples[i].EnvTemp, 9);
        }
    }

    [Fact]
    public void The_damper_is_not_a_second_gas_dial()
    {
        // The claim: two roasts can reach first crack at the same moment and still be
        // in materially different states when they get there. If the gas could
        // reproduce what the damper does, this would be impossible.
        var shut = Run(0.30, gas: 0.295);
        var open = Run(0.60, gas: 0.455);

        Assert.True(shut.Succeeded && open.Succeeded);
        Assert.True(Math.Abs(shut.FirstCrackTime - open.FirstCrackTime) < 15.0,
            $"Matched on crack time: {shut.FirstCrackTime:F0}s vs {open.FirstCrackTime:F0}s");

        // A shut damper does the same work with a much hotter drum behind it, which is
        // what scorching will be made of once the bean has a surface node.
        var drumGap = AtCrack(shut).EnvTemp - AtCrack(open).EnvTemp;
        Assert.True(drumGap > 15.0,
            $"A shut damper should need a hotter drum for the same roast: {drumGap:F0}C");

        // And it arrives with materially more water still in the beans, which is what
        // the crash is made of.
        var moistureRatio = AtCrack(shut).Moisture / AtCrack(open).Moisture;
        Assert.True(moistureRatio > 1.15,
            $"A shut damper should arrive wetter: {AtCrack(shut).Moisture:F4} vs {AtCrack(open).Moisture:F4}");
    }

    [Fact]
    public void Opening_the_damper_trades_drum_temperature_for_airflow()
    {
        // Monotone and wide: the damper is the control that decides how hot the drum
        // has to be to move the beans at all, across more than a hundred degrees.
        var drums = new[] { 0.20, 0.40, 0.60, 0.80, 1.00 }
            .Select(a => AtCrack(Run(a)).EnvTemp)
            .ToArray();

        for (var i = 1; i < drums.Length; i++)
        {
            Assert.True(drums[i] < drums[i - 1],
                $"Opening the damper should always cool the drum: {drums[i - 1]:F0}C -> {drums[i]:F0}C");
        }

        Assert.True(drums[0] - drums[^1] > 80.0,
            $"The range should be wide enough to matter: {drums[0]:F0}C to {drums[^1]:F0}C");
    }

    [Fact]
    public void Drying_is_slowest_at_both_ends_of_the_damper()
    {
        // Shut, the vapour has nowhere to go; wide open, the heat leaves before it
        // reaches the beans. Neither end is an argument for the other, which is what
        // makes this an axis with an optimum rather than a slider with a best value.
        static double Yellowing(RoastLog log) =>
            log.Samples.First(s => s.Phase == RoastPhase.Maillard).Time;

        var shut = Yellowing(Run(0.20));
        var middle = Yellowing(Run(0.70));
        var wide = Yellowing(Run(1.00));

        Assert.True(middle < shut, $"Shut should dry slower: {shut:F0}s vs {middle:F0}s");
        Assert.True(middle < wide, $"Wide open should dry slower: {wide:F0}s vs {middle:F0}s");
    }

    [Fact]
    public void The_damper_does_not_change_the_dead_time()
    {
        // design.md #9.4 builds the skill ceiling on ~20s of dead time. A second
        // control that quietly rewrites it would be a different kind of change from
        // one that does not — the lag lives in the probe and the RoR filter, not in
        // the thermal path, and this says so.
        const double StepAt = 300.0;

        static double RorResponse(double airflow)
        {
            RoastLog Trace(bool stepped) => RoastRunner.Run(
                new StepPilot(stepped, StepAt, airflow), maxSeconds: 420, sampleInterval: 0.25);

            var held = Trace(false);
            var stepped = Trace(true);
            return held.Samples.Zip(stepped.Samples)
                .Where(p => p.First.Time > StepAt)
                .First(p => p.Second.RateOfRise - p.First.RateOfRise >= 1.0)
                .Second.Time - StepAt;
        }

        var responses = new[] { 0.20, 0.60, 1.00 }.Select(RorResponse).ToArray();

        Assert.All(responses, r => Assert.InRange(r, 10.0, 45.0));
        Assert.True(responses.Max() - responses.Min() < 5.0,
            $"Dead time should barely move: {responses.Min():F1}s to {responses.Max():F1}s");
    }

    private sealed class StepPilot : IRoastPilot
    {
        private readonly DialTrace _trace;
        private readonly double _airflow;

        public StepPilot(bool stepped, double stepAt, double airflow)
        {
            _trace = stepped
                ? DialTrace.Of(new DialMove(0, 0.85), new DialMove(90, 0.45), new DialMove(stepAt, 0.75))
                : DialTrace.Of(new DialMove(0, 0.85), new DialMove(90, 0.45));
            _airflow = airflow;
        }

        public double Burner(in RoastState state) => _trace.At(state.Time);
        public double Airflow(in RoastState state) => _airflow;
        public bool Drop(in RoastState state) => false;
    }
}
