using System.Globalization;
using System.Text;

namespace SlowDrip.Sim.Roasting;

/// <summary>One recorded instant of a roast.</summary>
public readonly record struct RoastSample(
    double Time,
    double Burner,
    double EnvTemp,
    double BeanTemp,
    double BeanProbe,
    double RateOfRise,
    double SurfaceMoisture,
    double CoreMoisture,
    double ExothermWatts,
    double CrackedFraction,
    double PopsPerSecond,
    RoastPhase Phase)
{
    /// <summary>Total remaining water, dry basis.</summary>
    public double Moisture => SurfaceMoisture + CoreMoisture;
}

/// <summary>
/// The record of one roast: what the player did, what the machine did, and the
/// landmarks worth scoring against later.
/// </summary>
public sealed class RoastLog
{
    private readonly List<RoastSample> _samples = new();

    /// <summary>Samples in time order.</summary>
    public IReadOnlyList<RoastSample> Samples => _samples;

    /// <summary>Seconds from charge to first crack, or -1 if it never cracked.</summary>
    public double FirstCrackTime { get; internal set; } = -1.0;

    /// <summary>Seconds from charge to drop.</summary>
    public double DropTime { get; internal set; }

    /// <summary>Probe temperature at drop (degC).</summary>
    public double DropTemp { get; internal set; }

    /// <summary>Whether the roast reached first crack.</summary>
    public bool ReachedFirstCrack => FirstCrackTime >= 0.0;

    /// <summary>
    /// Time from first crack to drop as a fraction of total roast time, or -1.
    /// The one number design.md #9.4 asks to surface.
    /// </summary>
    public double DevelopmentTimeRatio =>
        ReachedFirstCrack && DropTime > 0.0 ? (DropTime - FirstCrackTime) / DropTime : -1.0;

    /// <summary>The turning point: the sample where the falling probe bottoms out.</summary>
    public RoastSample? TurningPoint
    {
        get
        {
            for (var i = 1; i < _samples.Count; i++)
            {
                if (_samples[i].BeanProbe > _samples[i - 1].BeanProbe) return _samples[i - 1];
            }

            return null;
        }
    }

    internal void Add(in RoastSample sample) => _samples.Add(sample);

    /// <summary>Peak smoothed RoR after the turning point (degC/min).</summary>
    public double PeakRateOfRise => _samples.Count == 0 ? 0.0 : _samples.Max(s => s.RateOfRise);

    /// <summary>Export for plotting outside the engine.</summary>
    public string ToCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("time_s,burner,env_c,bean_c,probe_c,ror_c_per_min,surface_moisture,core_moisture,exotherm_w,cracked_fraction,pops_per_s,phase");
        foreach (var s in _samples)
        {
            sb.Append(F(s.Time)).Append(',')
              .Append(F(s.Burner)).Append(',')
              .Append(F(s.EnvTemp)).Append(',')
              .Append(F(s.BeanTemp)).Append(',')
              .Append(F(s.BeanProbe)).Append(',')
              .Append(F(s.RateOfRise)).Append(',')
              .Append(F(s.SurfaceMoisture)).Append(',')
              .Append(F(s.CoreMoisture)).Append(',')
              .Append(F(s.ExothermWatts)).Append(',')
              .Append(F(s.CrackedFraction)).Append(',')
              .Append(F(s.PopsPerSecond)).Append(',')
              .Append(s.Phase)
              .AppendLine();
        }

        return sb.ToString();

        static string F(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);
    }
}
