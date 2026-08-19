using SlowDrip.Sim.Roasting;
using Xunit;

namespace SlowDrip.Sim.Tests;

public class RateOfRiseMeterTests
{
    private const double Dt = RoasterSim.FixedDt;

    [Fact]
    public void Reads_zero_for_a_flat_signal()
    {
        var meter = new RateOfRiseMeter(windowSeconds: 15, dt: Dt, smoothingTau: 0);
        for (var i = 0; i < 2000; i++) meter.Push(150.0);
        Assert.Equal(0.0, meter.Value, 6);
    }

    [Fact]
    public void Recovers_a_constant_slope_in_degrees_per_minute()
    {
        // 12 C/min = 0.2 C/s.
        var meter = new RateOfRiseMeter(windowSeconds: 15, dt: Dt, smoothingTau: 0);
        for (var i = 0; i < 6000; i++) meter.Push(20.0 + 0.2 * (i * Dt));

        Assert.Equal(12.0, meter.Value, 3);
    }

    [Fact]
    public void Does_not_spike_on_the_first_samples()
    {
        // Priming matters: an unprimed window would read the climb from zero to
        // the first sample as an enormous rate.
        var meter = new RateOfRiseMeter(windowSeconds: 15, dt: Dt, smoothingTau: 0);
        meter.Push(210.0);
        Assert.Equal(0.0, meter.Value, 6);
    }

    [Fact]
    public void Smoothing_costs_the_player_reaction_time()
    {
        // The telegraphing knob of design.md #9.4: more smoothing means the curve
        // answers a change later. Both meters see the same step in slope.
        var sharp = new RateOfRiseMeter(15, Dt, smoothingTau: 0);
        var smooth = new RateOfRiseMeter(15, Dt, smoothingTau: 8);

        var temp = 100.0;
        for (var i = 0; i < 3000; i++) { temp += 0.1 * Dt; sharp.Push(temp); smooth.Push(temp); }
        for (var i = 0; i < 900; i++) { temp += 0.4 * Dt; sharp.Push(temp); smooth.Push(temp); }

        Assert.True(sharp.Value > smooth.Value,
            $"Smoothed meter should still be catching up: sharp={sharp.Value:F2} smooth={smooth.Value:F2}");
    }
}
