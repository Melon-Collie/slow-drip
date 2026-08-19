using System.Reflection;
using SlowDrip.Sim.Roasting;
using Xunit;

namespace SlowDrip.Sim.Tests;

/// <summary>
/// design.md #15: "the simulation compiles without the engine. No Godot type
/// anywhere in the sim namespace."
/// </summary>
/// <remarks>
/// The design document is explicit that this should be checkable in CI rather
/// than a discipline someone has to remember at 2am. This is that check. It
/// reads the compiled assembly rather than the source tree, so it cannot be
/// fooled by a using alias or a file the test forgot to scan.
/// </remarks>
public class SimBoundaryTests
{
    private static readonly Assembly Sim = typeof(RoasterSim).Assembly;

    [Fact]
    public void Sim_assembly_references_no_engine_assembly()
    {
        var engineRefs = Sim.GetReferencedAssemblies()
            .Where(a => a.Name is not null && a.Name.Contains("Godot", StringComparison.OrdinalIgnoreCase))
            .Select(a => a.Name!)
            .ToArray();

        Assert.True(
            engineRefs.Length == 0,
            $"Sim references engine assemblies: {string.Join(", ", engineRefs)}. " +
            "The simulation must compile without the engine (design.md #15).");
    }

    [Fact]
    public void Sim_assembly_exposes_no_engine_types()
    {
        var offenders = Sim.GetTypes()
            .Where(t => t.Namespace is not null && t.Namespace.Contains("Godot", StringComparison.OrdinalIgnoreCase))
            .Select(t => t.FullName!)
            .ToArray();

        Assert.True(offenders.Length == 0, $"Engine types in the sim: {string.Join(", ", offenders)}");
    }
}
