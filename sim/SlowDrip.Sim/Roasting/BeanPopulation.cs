namespace SlowDrip.Sim.Roasting;

/// <summary>
/// The beans in the drum as a population rather than an average: every bean
/// carries its own temperature at which it will rupture.
/// </summary>
/// <remarks>
/// <para>
/// design.md #9.4 wants first crack to be audio — "scattered pops building" —
/// rather than a UI event. That only works if the pops come from somewhere. Here
/// they come from the batch not being uniform: bigger beans lag the batch
/// average, denser beans hold pressure longer, wetter ones build it sooner. The
/// spread of those differences is what the player hears.
/// </para>
/// <para>
/// This is the same move sprite-layers.md makes for cherries — the atom is the
/// bean, not the batch — and it buys the same thing: what the player perceives is
/// what the simulation is actually working with, with no fudge in between.
/// </para>
/// <para>
/// Thresholds are laid out on stratified quantiles rather than drawn at random.
/// Two lots with the same parameters therefore crack identically, which is what
/// a game that has to feel repeatable wants. The scatter that makes crackle sound
/// organic belongs in the audio layer, on top of the rate this reports.
/// </para>
/// </remarks>
public sealed class BeanPopulation
{
    private readonly double[] _crackTemps;
    private int _cracked;

    /// <summary>
    /// Build a population of <paramref name="count"/> beans whose rupture
    /// temperatures follow a logistic distribution.
    /// </summary>
    /// <param name="count">Beans in the drum.</param>
    /// <param name="meanTemp">Temperature the median bean cracks at (degC).</param>
    /// <param name="spread">
    /// Standard deviation of that temperature (degC). This is the uniformity of
    /// the lot: a well-sorted single screen size is tight, a mixed lot is wide.
    /// </param>
    public BeanPopulation(int count, double meanTemp, double spread)
    {
        if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
        if (spread < 0.0) throw new ArgumentOutOfRangeException(nameof(spread));

        _crackTemps = new double[count];

        // A logistic distribution has a closed-form quantile, so the population can
        // be laid out exactly with no sampling noise and no random number generator
        // anywhere near the simulation.
        var scale = spread / 1.8137993642342178; // sd = scale * pi / sqrt(3)
        for (var i = 0; i < count; i++)
        {
            var p = (i + 0.5) / count;
            _crackTemps[i] = meanTemp + scale * Math.Log(p / (1.0 - p));
        }
    }

    /// <summary>Beans in the drum.</summary>
    public int Count => _crackTemps.Length;

    /// <summary>Beans that have ruptured so far.</summary>
    public int Cracked => _cracked;

    /// <summary>Share of the batch that has ruptured, 0..1.</summary>
    public double CrackedFraction => (double)_cracked / _crackTemps.Length;

    /// <summary>Coldest bean in the batch (degC) — the first one to go.</summary>
    public double FirstCrackTemp => _crackTemps[0];

    /// <summary>
    /// Crack every bean whose threshold this temperature has now reached, and
    /// report how many went. Cracking is irreversible, so a stalling roast simply
    /// stops producing pops rather than un-cracking anything.
    /// </summary>
    public int CrackUpTo(double beanTemp)
    {
        var before = _cracked;
        while (_cracked < _crackTemps.Length && _crackTemps[_cracked] <= beanTemp) _cracked++;
        return _cracked - before;
    }
}
