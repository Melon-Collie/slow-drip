using SlowDrip.Sim.Roasting;
using Xunit;

namespace SlowDrip.Sim.Tests;

public class DialTraceTests
{
    [Fact]
    public void Holds_each_setting_until_the_next_move()
    {
        var trace = DialTrace.Of(new DialMove(0, 1.0), new DialMove(60, 0.5), new DialMove(120, 0.25));

        Assert.Equal(1.0, trace.At(0));
        Assert.Equal(1.0, trace.At(59.9));
        Assert.Equal(0.5, trace.At(60));
        Assert.Equal(0.5, trace.At(119.9));
        Assert.Equal(0.25, trace.At(120));
        Assert.Equal(0.25, trace.At(10_000));
    }

    [Fact]
    public void Reads_zero_before_the_first_move()
    {
        var trace = DialTrace.Of(new DialMove(30, 0.8));
        Assert.Equal(0.0, trace.At(0));
        Assert.Equal(0.0, trace.At(29.9));
        Assert.Equal(0.8, trace.At(30));
    }

    [Fact]
    public void Sorts_moves_given_out_of_order()
    {
        var trace = DialTrace.Of(new DialMove(120, 0.25), new DialMove(0, 1.0), new DialMove(60, 0.5));
        Assert.Equal(new[] { 0.0, 60.0, 120.0 }, trace.Moves.Select(m => m.Time));
        Assert.Equal(0.5, trace.At(90));
    }

    [Fact]
    public void Rejects_moves_before_charge()
    {
        Assert.Throws<ArgumentException>(() => DialTrace.Of(new DialMove(-1, 0.5)));
    }
}
