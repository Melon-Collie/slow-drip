namespace SlowDrip.Sim.Roasting;

/// <summary>
/// Turns a probe trace into the rate-of-rise readout: a finite difference over a
/// sliding window, then an exponential smoother.
/// </summary>
/// <remarks>
/// Both stages cost the player foresight. Widening the window or the smoothing
/// makes the curve calmer to read and later to react to. See
/// <see cref="RoasterConfig.RorSmoothing"/>.
/// </remarks>
public sealed class RateOfRiseMeter
{
    private readonly double[] _window;
    private readonly double _windowSeconds;
    private readonly double _alpha;
    private int _head;
    private bool _filled;
    private double _smoothed;
    private bool _primed;

    public RateOfRiseMeter(double windowSeconds, double dt, double smoothingTau)
    {
        if (windowSeconds <= 0.0) throw new ArgumentOutOfRangeException(nameof(windowSeconds));
        if (dt <= 0.0) throw new ArgumentOutOfRangeException(nameof(dt));
        if (smoothingTau < 0.0) throw new ArgumentOutOfRangeException(nameof(smoothingTau));

        var samples = Math.Max(2, (int)Math.Round(windowSeconds / dt));
        _window = new double[samples];
        // The oldest live sample sits (samples - 1) steps behind the newest.
        _windowSeconds = (samples - 1) * dt;
        _alpha = dt / (smoothingTau + dt);
    }

    /// <summary>Smoothed rate of rise in degC per minute.</summary>
    public double Value => _smoothed;

    /// <summary>Feed one probe sample. Must be called once per fixed step.</summary>
    public void Push(double probeTemp)
    {
        if (!_filled && _head == 0)
        {
            // Prime the window so the first samples do not read as a false spike.
            for (var i = 0; i < _window.Length; i++) _window[i] = probeTemp;
        }

        var oldest = _window[(_head + 1) % _window.Length];
        _window[_head] = probeTemp;
        _head = (_head + 1) % _window.Length;
        if (_head == 0) _filled = true;

        var raw = (probeTemp - oldest) / _windowSeconds * 60.0;

        if (!_primed)
        {
            _smoothed = raw;
            _primed = true;
        }
        else
        {
            _smoothed += _alpha * (raw - _smoothed);
        }
    }
}
