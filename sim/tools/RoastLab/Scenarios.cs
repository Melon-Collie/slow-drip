using SlowDrip.Sim.Roasting;

namespace SlowDrip.RoastLab;

/// <summary>A named roast: how the dial gets worked, and on what lot.</summary>
public sealed record Scenario(string Name, string Intent, Func<RoastLog> Run)
{
    public static Scenario Named(string name) =>
        All.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"No scenario '{name}'. Known: {string.Join(", ", All.Select(s => s.Name))}");

    private static Func<RoastLog> Pilot(Func<IRoastPilot> pilot, RoastCharge? charge = null) =>
        () => RoastRunner.Run(pilot(), charge: charge);

    private static Func<RoastLog> Trace(DialTrace trace, RoastCharge? charge = null) =>
        () => RoastRunner.Run(trace, charge: charge, dropWhen: RoastRunner.DropAtTemp(213.0));

    /// <summary>
    /// Each scenario exists to make one claim visible in a CSV, so the model can be
    /// checked without playing it.
    /// </summary>
    public static IReadOnlyList<Scenario> All { get; } = new[]
    {
        new Scenario(
            "textbook",
            "The published protocol played straight. The roast the others are wrong against.",
            Pilot(ReferenceRoasts.Textbook)),

        new Scenario(
            "cut-too-deep",
            "The pre-crack gas taken down to 0.33. The roast loses momentum, the rate\n   of rise sags toward zero, and it never reaches drop temperature.",
            Pilot(ReferenceRoasts.CutTooDeep)),

        new Scenario(
            "cut-too-shallow",
            "Barely any reduction at all. Nothing crashes and nothing stalls — the roast\n   carries too much heat into development and arrives at drop underdeveloped.",
            Pilot(ReferenceRoasts.CutTooShallow)),

        new Scenario(
            "baked",
            "Heat pulled hard at 00:55. The rate of rise flattens through drying and\n   the same protocol across first crack cannot buy the time back.",
            Pilot(ReferenceRoasts.Baked)),

        new Scenario(
            "scorch",
            "Full burner held down. The beans bolt through first crack and reach drop\n   temperature half a minute later: burnt outside, raw inside.",
            Trace(ReferenceRoasts.Scorch)),

        new Scenario(
            "hand-played",
            "A fixed dial trace that respects the window. Works — but see dense-lot.",
            Trace(ReferenceRoasts.HandPlayed)),

        new Scenario(
            "well-sorted",
            "A lot sorted to one screen size. First crack lands as a tight volley\n   you can time a gas reduction against.",
            Pilot(ReferenceRoasts.Textbook, ReferenceRoasts.WellSorted)),

        new Scenario(
            "mixed-screen",
            "The same beans, unsorted. The stragglers pop half a minute early, the\n   crackle never gets loud, and development runs long before drop is reached.",
            Pilot(ReferenceRoasts.Textbook, ReferenceRoasts.MixedScreen)),

        new Scenario(
            "dense-lot",
            "The same fixed trace on a denser, wetter lot. It stalls out entirely:\n   last season's profile does not transfer.",
            Trace(ReferenceRoasts.HandPlayed, ReferenceRoasts.DenseLot)),

        new Scenario(
            "dense-lot-piloted",
            "The same lot, roasted by the controller instead of the recording. It\n   watches the curve and adapts.",
            Pilot(ReferenceRoasts.Textbook, ReferenceRoasts.DenseLot)),
    };
}
