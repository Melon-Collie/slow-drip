namespace SlowDrip.Sim.Roasting;

/// <summary>The stage of the roast, derived from state rather than scheduled.</summary>
public enum RoastPhase
{
    /// <summary>Cold beans are pulling the drum down; the probe is still falling.</summary>
    Charge,

    /// <summary>Free moisture is leaving. Energy goes into phase change, not temperature.</summary>
    Drying,

    /// <summary>Dry beans climbing toward first crack.</summary>
    Maillard,

    /// <summary>First crack has begun. The exotherm is running.</summary>
    Development,
}
