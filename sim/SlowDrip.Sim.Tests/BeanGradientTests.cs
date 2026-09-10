using SlowDrip.Sim.Roasting;
using Xunit;

namespace SlowDrip.Sim.Tests;

/// <summary>
/// The bean is not isothermal. Heat arrives at its surface and has to travel inward,
/// so the outside runs ahead of the inside — and that gradient is what scorching and
/// tipping are made of.
/// </summary>
public class BeanGradientTests
{
    private static RoastLog Run(RoasterConfig? config = null) =>
        RoastRunner.Run(ReferenceRoasts.Textbook(), config, sampleInterval: 1.0);

    [Fact]
    public void The_outside_of_the_bean_leads_the_inside_all_roast()
    {
        var log = Run();

        foreach (var s in log.Samples.Where(s => s.Time > 1.0))
        {
            Assert.True(s.SurfaceCoreGap > 0.0,
                $"Heat arrives at the surface, so it cannot trail the core: {s.SurfaceCoreGap:F2}C at t={s.Time:F0}s");
        }
    }

    [Fact]
    public void Bulk_bean_temperature_sits_between_the_two_nodes()
    {
        // What the probe reads toward is the two nodes averaged by heat capacity, so
        // it can never be outside them. This is the invariant that says the split did
        // not quietly change what BeanTemp means.
        foreach (var s in Run().Samples.Where(s => s.Time > 1.0))
        {
            Assert.InRange(s.BeanTemp, s.BeanCoreTemp, s.BeanSurfaceTemp);
        }
    }

    [Fact]
    public void The_gradient_is_widest_while_the_beans_are_being_driven_hardest()
    {
        // It tracks heat flux, not temperature: the gap peaks in drying, when the drum
        // is far above the beans and pushing hardest, and closes as the roast settles.
        // Worth pinning, because it is the opposite of the intuition that the gap
        // should be widest when the beans are hottest.
        var log = Run();
        var peak = log.Samples.Where(s => s.Time > 30).OrderByDescending(s => s.SurfaceCoreGap).First();
        var atDrop = log.Samples[^1];

        Assert.True(peak.Time < log.FirstCrackTime,
            $"The gap should peak before first crack, not at {peak.Time:F0}s");
        Assert.True(peak.SurfaceCoreGap > 10.0,
            $"and be worth measuring: {peak.SurfaceCoreGap:F1}C");
        Assert.True(atDrop.SurfaceCoreGap < peak.SurfaceCoreGap * 0.4,
            $"and close as the roast settles: {peak.SurfaceCoreGap:F1}C -> {atDrop.SurfaceCoreGap:F1}C");
    }

    [Fact]
    public void A_stiffer_bean_holds_a_wider_gradient()
    {
        // The knob does what it says: internal conductance is what decides how far the
        // outside can run ahead. This is the lever the scorch defect will be tuned on.
        static double PeakGap(double conductance) =>
            Run(RoasterConfig.Default with { BeanInternalConductance = conductance })
                .Samples.Where(s => s.Time > 30).Max(s => s.SurfaceCoreGap);

        var stiff = PeakGap(12.0);
        var nominal = PeakGap(40.0);
        var conductive = PeakGap(200.0);

        Assert.True(stiff > nominal, $"{stiff:F1}C vs {nominal:F1}C");
        Assert.True(nominal > conductive, $"{nominal:F1}C vs {conductive:F1}C");
    }

    [Fact]
    public void A_bean_that_conducts_freely_is_the_single_node_model_again()
    {
        // The split has to be a superset. Crank internal conduction and the two nodes
        // collapse onto each other, which is the check that nothing about the bulk
        // behaviour is hiding in the split itself.
        var log = Run(RoasterConfig.Default with { BeanInternalConductance = 5000.0 });

        foreach (var s in log.Samples.Where(s => s.Time > 30))
        {
            Assert.True(s.SurfaceCoreGap < 0.5,
                $"Should be near-isothermal: {s.SurfaceCoreGap:F2}C at t={s.Time:F0}s");
        }

        Assert.True(log.Succeeded);
        Assert.InRange(log.DropTemp, 210.0, 216.0);
    }

    [Fact]
    public void Moving_heat_inside_the_bean_does_not_create_or_destroy_any()
    {
        // Internal conduction cancels out of the reported energy balance, so NetBeanWatts
        // still means what design.md #9.3 says it means: what the bean mass as a whole is
        // gaining. If the split leaked here, the stall warning would be lying.
        var log = Run();

        for (var i = 2; i < log.Samples.Count; i++)
        {
            var rising = log.Samples[i].BeanTemp > log.Samples[i - 1].BeanTemp;
            var gaining = log.Samples[i - 1].NetBeanWatts > 0;
            Assert.True(rising == gaining,
                $"At t={log.Samples[i].Time}s net was {log.Samples[i - 1].NetBeanWatts:F0}W but bulk bean temp " +
                $"{(rising ? "rose" : "fell")}");
        }
    }
}
