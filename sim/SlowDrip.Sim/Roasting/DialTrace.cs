namespace SlowDrip.Sim.Roasting;

/// <summary>One dial move: turn the burner to <paramref name="Burner"/> at <paramref name="Time"/> seconds.</summary>
public readonly record struct DialMove(double Time, double Burner);

/// <summary>
/// A recorded sequence of dial moves, sampled and held between them.
/// </summary>
/// <remarks>
/// This is the replay format that makes the sim tunable without the engine: a
/// trace plus a config plus a charge fully determines a roast. It is also the
/// shape a saved profile takes later (design.md #9.4) — which is why the same
/// trace landing somewhere else on a different lot needs no extra machinery.
/// </remarks>
public sealed class DialTrace
{
    private readonly DialMove[] _moves;

    private DialTrace(DialMove[] moves) => _moves = moves;

    /// <summary>The moves, in time order.</summary>
    public IReadOnlyList<DialMove> Moves => _moves;

    /// <summary>Build a trace from moves in any order. Times must not be negative.</summary>
    public static DialTrace Of(params DialMove[] moves)
    {
        ArgumentNullException.ThrowIfNull(moves);
        foreach (var m in moves)
        {
            if (m.Time < 0.0) throw new ArgumentException($"Move at t={m.Time} is before charge.", nameof(moves));
        }

        var sorted = moves.OrderBy(m => m.Time).ToArray();
        return new DialTrace(sorted);
    }

    /// <summary>Hold the burner at one setting for the whole roast.</summary>
    public static DialTrace Constant(double burner) => Of(new DialMove(0.0, burner));

    /// <summary>Dial position at <paramref name="time"/> seconds. Zero before the first move.</summary>
    public double At(double time)
    {
        var value = 0.0;
        foreach (var move in _moves)
        {
            if (move.Time > time) break;
            value = move.Burner;
        }

        return value;
    }
}
