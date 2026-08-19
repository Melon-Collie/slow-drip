using SlowDrip.Sim.Roasting;
using Xunit;

namespace SlowDrip.Sim.Tests;

/// <summary>
/// The claims design.md #9.4 makes about the roaster, plus the ones published
/// roasting guidance makes about roasting, stated as tests.
/// </summary>
/// <remarks>
/// These assert shapes, not tuning. A threshold here should be loose enough that
/// re-tuning the config does not break it, and tight enough that losing the
/// behaviour does. If a change to <see cref="RoasterConfig"/> turns one of these
/// red, the model stopped making a promise the design depends on.
/// </remarks>
public class RoasterSimTests
{
    private const double MaxSeconds = 1200.0;

    private static RoastLog Run(IRoastPilot pilot, RoastCharge? charge = null) =>
        RoastRunner.Run(pilot, charge: charge, maxSeconds: MaxSeconds, sampleInterval: 1.0);

    private static RoastLog Run(DialTrace trace, RoastCharge? charge = null) =>
        RoastRunner.Run(trace, charge: charge, maxSeconds: MaxSeconds,
            dropWhen: RoastRunner.DropAtTemp(213.0), sampleInterval: 1.0);

    private static double TroughAfterCrack(RoastLog log) =>
        log.Samples.Where(s => s.Time > log.FirstCrackTime).Min(s => s.RateOfRise);

    // ---- Determinism -------------------------------------------------------

    [Fact]
    public void Same_roast_runs_bit_identically_twice()
    {
        var a = Run(ReferenceRoasts.Textbook());
        var b = Run(ReferenceRoasts.Textbook());

        Assert.Equal(a.Samples.Count, b.Samples.Count);
        for (var i = 0; i < a.Samples.Count; i++)
        {
            Assert.Equal(a.Samples[i].BeanTemp, b.Samples[i].BeanTemp);
            Assert.Equal(a.Samples[i].RateOfRise, b.Samples[i].RateOfRise);
            Assert.Equal(a.Samples[i].CoreMoisture, b.Samples[i].CoreMoisture);
        }

        Assert.Equal(a.FirstCrackTime, b.FirstCrackTime);
    }

    [Fact]
    public void Time_advances_exactly_one_fixed_step_at_a_time()
    {
        var sim = new RoasterSim();
        sim.Step(600);
        Assert.Equal(600 * RoasterSim.FixedDt, sim.State.Time, 9);
    }

    // ---- Lag: the whole skill ceiling --------------------------------------

    [Fact]
    public void Drum_answers_the_dial_immediately_but_the_readout_does_not()
    {
        // design.md #9.4: "You turn the dial and nothing happens for ~20 seconds,
        // then bean temp responds to what you did back then." Nothing in the model
        // delays anything on purpose — this is two lags in series plus the probe.
        const double StepAt = 300.0;
        var held = DialTrace.Of(new DialMove(0, 1.00), new DialMove(90, 0.45));
        var stepped = DialTrace.Of(new DialMove(0, 1.00), new DialMove(90, 0.45), new DialMove(StepAt, 0.75));

        var a = RoastRunner.Run(held, maxSeconds: 420, sampleInterval: 0.25);
        var b = RoastRunner.Run(stepped, maxSeconds: 420, sampleInterval: 0.25);
        var pairs = a.Samples.Zip(b.Samples).Where(p => p.First.Time > StepAt).ToArray();

        var envRespondsAt = pairs.First(p => p.Second.EnvTemp - p.First.EnvTemp >= 1.0).Second.Time - StepAt;
        var rorRespondsAt = pairs.First(p => p.Second.RateOfRise - p.First.RateOfRise >= 1.0).Second.Time - StepAt;

        Assert.True(envRespondsAt <= 5.0, $"Drum should answer the dial at once, took {envRespondsAt:F1}s");
        Assert.InRange(rorRespondsAt, 10.0, 45.0);
    }

    [Fact]
    public void Probe_disagrees_with_the_beans_at_charge()
    {
        // The probe was sitting in a preheated drum, so it starts hot while the
        // beans are at room temperature. The player is reading an instrument.
        var s = new RoasterSim().State;

        Assert.Equal(RoasterConfig.Default.ChargeTemp, s.BeanProbe, 6);
        Assert.Equal(RoasterConfig.Default.AmbientTemp, s.BeanTemp, 6);
    }

    [Fact]
    public void Probe_falls_then_turns_around()
    {
        // The turning point. It is a sensor artefact, and it falls out of the
        // model rather than being authored. Published guidance puts it around a
        // minute in, at 80-95C.
        var log = Run(ReferenceRoasts.Textbook());
        var tp = log.TurningPoint;

        Assert.NotNull(tp);
        Assert.InRange(tp!.Value.Time, 20.0, 120.0);
        Assert.InRange(tp.Value.BeanProbe, 70.0, 110.0);

        // True bean temperature never dips — only the reading does.
        var early = log.Samples.Where(s => s.Time <= 180).ToArray();
        for (var i = 1; i < early.Length; i++)
        {
            Assert.True(early[i].BeanTemp >= early[i - 1].BeanTemp - 1e-9,
                $"Bean temp fell at t={early[i].Time}s");
        }
    }

    // ---- The reference roast against published targets ----------------------

    [Fact]
    public void Textbook_roast_matches_the_published_shape()
    {
        // Roasting guidance puts a well-run drum roast at roughly 10 C/min through
        // the middle and 5 C/min by first crack, cracking around nine minutes and
        // dropping a couple of minutes later.
        var log = Run(ReferenceRoasts.Textbook());
        Assert.True(log.ReachedFirstCrack);

        var mid = log.Samples.First(s => s.Time >= 300).RateOfRise;
        var atCrack = log.Samples.First(s => s.Time >= log.FirstCrackTime).RateOfRise;

        Assert.InRange(mid, 7.0, 16.0);
        Assert.InRange(atCrack, 3.0, 10.0);
        Assert.True(atCrack < mid, "Rate of rise should still be falling at first crack");
        Assert.InRange(log.FirstCrackTime, 420.0, 660.0);
        Assert.InRange(log.DropTime, 520.0, 780.0);
        // Wide, because the ratio depends on where first crack is called and that
        // is a reading rather than a fact — see FirstCrackAudibleFraction.
        Assert.InRange(log.DevelopmentTimeRatio, 0.15, 0.30);
    }

    [Fact]
    public void Textbook_roast_glides_its_rate_of_rise_downward()
    {
        var log = Run(ReferenceRoasts.Textbook());
        var tp = log.TurningPoint!.Value;
        var peakAt = log.Samples.Where(s => s.Time > tp.Time).OrderByDescending(s => s.RateOfRise).First().Time;
        var glide = log.Samples.Where(s => s.Time > peakAt && s.Time < log.FirstCrackTime).ToArray();

        for (var i = 1; i < glide.Length; i++)
        {
            var rise = glide[i].RateOfRise - glide[i - 1].RateOfRise;
            Assert.True(rise <= 0.5, $"RoR climbed {rise:F2} C/min at t={glide[i].Time}s — not a glide");
        }

        Assert.True(TroughAfterCrack(log) > 0.0, "A well-run roast should not stall after first crack");
    }

    // ---- The crash: a thing that happens, not a thing you do ----------------

    [Fact]
    public void Beans_vent_their_core_moisture_at_first_crack()
    {
        // Published explanation of the crash: around the beginning of first crack
        // the beans release a great deal of moisture from their cores in a short
        // period. That is modelled here as the core pool emptying into the surface
        // pool once the bean structure has ruptured.
        var log = Run(ReferenceRoasts.Textbook());
        var atCrack = log.Samples.First(s => s.Time >= log.FirstCrackTime);
        var later = log.Samples.First(s => s.Time >= log.FirstCrackTime + 60);

        Assert.True(atCrack.CoreMoisture > 0.004, "There should be core water left to vent");
        Assert.True(later.CoreMoisture < atCrack.CoreMoisture * 0.4,
            $"Core should vent at first crack: {atCrack.CoreMoisture:F4} -> {later.CoreMoisture:F4}");
    }

    [Fact]
    public void The_vent_is_what_crashes_the_rate_of_rise()
    {
        // Isolate the mechanism: same dial, same everything, with the rupture
        // release turned off. The crash should go with it.
        var withVent = RoasterConfig.Default;
        var withoutVent = RoasterConfig.Default with { RuptureVentFraction = 0.0 };

        var a = RoastRunner.Run(ReferenceRoasts.Textbook(), withVent, maxSeconds: MaxSeconds, sampleInterval: 1.0);
        var b = RoastRunner.Run(ReferenceRoasts.Textbook(), withoutVent, maxSeconds: MaxSeconds, sampleInterval: 1.0);

        Assert.True(a.ReachedFirstCrack && b.ReachedFirstCrack);
        Assert.True(TroughAfterCrack(a) < TroughAfterCrack(b) - 1.0,
            $"Venting should deepen the post-crack dip: {TroughAfterCrack(a):F1} vs {TroughAfterCrack(b):F1} C/min");
    }

    // ---- The taught protocol -----------------------------------------------

    [Fact]
    public void Making_the_pre_crack_cut_too_early_stalls_the_roast()
    {
        // "Not so low that the roast loses momentum and the bean temperature stops
        // increasing before the end of the roast."
        var early = Run(ReferenceRoasts.CutTooEarly());

        Assert.True(early.ReachedFirstCrack);
        Assert.True(TroughAfterCrack(early) < 0.0,
            $"Cutting 90s out should stall the roast, trough was {TroughAfterCrack(early):F1} C/min");
        Assert.True(early.DropTemp < 213.0,
            "A stalled roast should never reach drop temperature");
    }

    [Fact]
    public void Making_the_pre_crack_cut_too_late_underdevelops_the_roast()
    {
        // Leave the reduction until the crack is on top of you and the roast
        // arrives at drop temperature before development has happened.
        var late = Run(ReferenceRoasts.CutTooLate());
        var textbook = Run(ReferenceRoasts.Textbook());

        Assert.True(late.ReachedFirstCrack);
        Assert.True(late.DevelopmentTimeRatio < textbook.DevelopmentTimeRatio - 0.04,
            $"Late cut should shorten development: {late.DevelopmentTimeRatio:P0} vs {textbook.DevelopmentTimeRatio:P0}");
    }

    [Fact]
    public void The_taught_lead_time_sits_between_the_two_failures()
    {
        // The published number is 45 seconds. The model should agree that it is
        // between stalling and bolting rather than at either end.
        var textbook = Run(ReferenceRoasts.Textbook());

        Assert.True(TroughAfterCrack(textbook) > 0.0, "45s lead should not stall");
        Assert.InRange(textbook.DevelopmentTimeRatio, 0.15, 0.30);
        Assert.InRange(textbook.DropTemp, 210.0, 216.0);
    }

    // ---- The exotherm ------------------------------------------------------

    [Fact]
    public void Self_heating_consumes_its_own_fuel()
    {
        // Arrhenius kinetics on a finite reactant. Without depletion the roast
        // would generate heat for ever; with it, the flick is a bump and not an
        // escape.
        var sim = new RoasterSim();
        sim.SetBurner(1.0);
        var seenPositive = false;

        for (var i = 0; i < 60 * 60 * 12; i++)
        {
            sim.Step();
            if (sim.State.ExothermWatts > 100) seenPositive = true;
        }

        var end = sim.State;
        Assert.True(seenPositive, "The exotherm should have run at some point");
        Assert.InRange(end.ReactantRemaining, 0.0, 1.0);
        Assert.True(end.ReactantRemaining < 0.5,
            $"A long hot roast should burn through its reactant, {end.ReactantRemaining:F2} left");
    }

    [Fact]
    public void A_normal_roast_leaves_most_of_the_reactant_unburnt()
    {
        // Calorimetry measures 250-420 kJ/kg up to 300C, well past any drop
        // temperature. Only a fraction of that budget should be spent by drop.
        var sim = new RoasterSim();
        var pilot = ReferenceRoasts.Textbook();
        var state = sim.State;

        while (state.Time < MaxSeconds && !pilot.Drop(state))
        {
            sim.SetBurner(pilot.Burner(state));
            sim.Step();
            state = sim.State;
        }

        Assert.InRange(state.ReactantRemaining, 0.5, 0.95);
    }

    // ---- Moisture ----------------------------------------------------------

    [Fact]
    public void Water_only_ever_leaves_and_never_goes_negative()
    {
        var log = Run(ReferenceRoasts.Textbook());

        for (var i = 1; i < log.Samples.Count; i++)
        {
            Assert.True(log.Samples[i].Moisture <= log.Samples[i - 1].Moisture + 1e-12,
                $"Total moisture rose at t={log.Samples[i].Time}s");
            Assert.True(log.Samples[i].SurfaceMoisture >= 0.0);
            Assert.True(log.Samples[i].CoreMoisture >= 0.0);
        }

        Assert.True(log.Samples[^1].Moisture < RoastCharge.Default.Moisture / 3.0,
            "A roast to first crack should have driven most of the water off");
    }

    [Fact]
    public void A_wetter_lot_cracks_later()
    {
        var normal = Run(ReferenceRoasts.Textbook());
        var wet = Run(ReferenceRoasts.Textbook(), new RoastCharge { Moisture = 0.155 });

        Assert.True(normal.ReachedFirstCrack && wet.ReachedFirstCrack);
        Assert.True(wet.FirstCrackTime > normal.FirstCrackTime,
            $"Wet lot cracked at {wet.FirstCrackTime:F0}s, normal at {normal.FirstCrackTime:F0}s");
    }

    [Fact]
    public void Phases_run_in_order_and_never_go_backwards()
    {
        var log = Run(ReferenceRoasts.Textbook());
        var seen = log.Samples.Select(s => (int)s.Phase).ToArray();

        for (var i = 1; i < seen.Length; i++)
        {
            Assert.True(seen[i] >= seen[i - 1],
                $"Phase went backwards at t={log.Samples[i].Time}s: {(RoastPhase)seen[i - 1]} -> {(RoastPhase)seen[i]}");
        }

        Assert.Contains(log.Samples, s => s.Phase == RoastPhase.Drying);
        Assert.Contains(log.Samples, s => s.Phase == RoastPhase.Maillard);
        Assert.Equal(RoastPhase.Development, log.Samples[^1].Phase);
    }

    // ---- Baked -------------------------------------------------------------

    [Fact]
    public void Pulling_heat_early_flatlines_drying_and_costs_the_rest_of_the_roast()
    {
        var baked = Run(ReferenceRoasts.Baked());
        var textbook = Run(ReferenceRoasts.Textbook());

        static double DryingFloor(RoastLog log) =>
            log.Samples.Where(s => s.Phase == RoastPhase.Drying && s.Time > 120).Min(s => s.RateOfRise);

        Assert.True(DryingFloor(baked) < 3.0,
            $"Baked roast should flatline in drying, floor was {DryingFloor(baked):F1} C/min");
        Assert.True(DryingFloor(textbook) > 4.0,
            $"Textbook roast should keep climbing, floor was {DryingFloor(textbook):F1} C/min");
        Assert.True(baked.FirstCrackTime > textbook.FirstCrackTime + 180,
            $"Baked cracked at {baked.FirstCrackTime:F0}s vs {textbook.FirstCrackTime:F0}s");
    }

    // ---- Profiles do not transfer ------------------------------------------

    [Fact]
    public void A_recorded_profile_misses_on_a_new_lot_but_a_pilot_adapts()
    {
        // design.md #9.4: "Automation handles daily volume; anything new or
        // precious pulls you back to the dial." A fixed trace cuts the gas at a
        // fixed second whether or not the roast has got there; a controller that
        // watches the curve does not.
        var recorded = Run(ReferenceRoasts.HandPlayed, ReferenceRoasts.DenseLot);
        var piloted = Run(ReferenceRoasts.Textbook(), ReferenceRoasts.DenseLot);
        var recordedOnItsOwnLot = Run(ReferenceRoasts.HandPlayed);

        Assert.True(recordedOnItsOwnLot.ReachedFirstCrack, "The trace should work on the lot it was made for");
        Assert.False(recorded.ReachedFirstCrack, "The same trace should miss on the denser, wetter lot");
        Assert.True(piloted.ReachedFirstCrack, "The controller should adapt to it");
        Assert.InRange(piloted.DropTemp, 210.0, 216.0);
    }

    [Fact]
    public void A_heavier_charge_roasts_more_slowly()
    {
        var light = Run(ReferenceRoasts.Textbook(), new RoastCharge { DryMassKg = 0.7 });
        var heavy = Run(ReferenceRoasts.Textbook(), new RoastCharge { DryMassKg = 1.3 });

        Assert.True(light.ReachedFirstCrack);
        Assert.True(!heavy.ReachedFirstCrack || heavy.FirstCrackTime > light.FirstCrackTime,
            "More mass in the drum must take longer to reach first crack");
    }

    // ---- Housekeeping ------------------------------------------------------

    [Fact]
    public void Burner_input_is_clamped()
    {
        var sim = new RoasterSim();
        sim.SetBurner(5.0);
        Assert.Equal(1.0, sim.State.Burner);
        sim.SetBurner(-3.0);
        Assert.Equal(0.0, sim.State.Burner);
    }

    [Fact]
    public void Development_time_ratio_is_unavailable_before_first_crack()
    {
        var sim = new RoasterSim();
        sim.SetBurner(0.5);
        sim.Step(60);
        Assert.Equal(-1.0, sim.State.DevelopmentTimeRatio);
    }

    [Fact]
    public void Predicted_time_to_crack_falls_as_the_crack_approaches()
    {
        var log = Run(ReferenceRoasts.Textbook());
        var early = log.Samples.First(s => s.Time >= 300);
        var late = log.Samples.First(s => s.Time >= log.FirstCrackTime - 60);

        static double Predict(RoastSample s) => DoctrinePilot.PredictedSecondsToCrack(
            new RoastState { BeanProbe = s.BeanProbe, RateOfRise = s.RateOfRise }, 196.0);

        Assert.True(Predict(late) < Predict(early),
            $"Prediction should tighten: {Predict(early):F0}s at 300s, {Predict(late):F0}s near the crack");
    }

    [Fact]
    public void Csv_export_has_a_row_per_sample()
    {
        var log = Run(ReferenceRoasts.Textbook());
        var lines = log.ToCsv().TrimEnd('\n').Split('\n');

        Assert.Equal(log.Samples.Count + 1, lines.Length);
        Assert.StartsWith("time_s,burner,env_c", lines[0]);
    }
}

/// <summary>
/// First crack as a population event: design.md #9.4 wants "scattered pops
/// building" as audio rather than a UI cue, which only works if the pops have a
/// source.
/// </summary>
public class BeanPopulationTests
{
    private static RoastLog Run(RoastCharge charge) =>
        RoastRunner.Run(ReferenceRoasts.Textbook(), charge: charge, maxSeconds: 1200, sampleInterval: 0.5);

    private static double CracklingSeconds(RoastLog log)
    {
        var audible = log.Samples.Where(s => s.PopsPerSecond >= 3.0).ToArray();
        return audible.Length == 0 ? 0.0 : audible[^1].Time - audible[0].Time;
    }

    [Fact]
    public void Thresholds_are_laid_out_without_a_random_number_generator()
    {
        // Two populations with the same parameters must be identical, or replay
        // and multiplayer both break (design.md #12).
        var a = new BeanPopulation(5000, 196.0, 3.5);
        var b = new BeanPopulation(5000, 196.0, 3.5);

        for (var t = 180.0; t < 215.0; t += 0.25)
        {
            a.CrackUpTo(t);
            b.CrackUpTo(t);
            Assert.Equal(a.Cracked, b.Cracked);
        }

        Assert.Equal(1.0, a.CrackedFraction, 6);
    }

    [Fact]
    public void A_wider_spread_puts_the_median_bean_in_the_same_place()
    {
        var tight = new BeanPopulation(4001, 196.0, 1.0);
        var wide = new BeanPopulation(4001, 196.0, 8.0);

        tight.CrackUpTo(196.0);
        wide.CrackUpTo(196.0);

        Assert.InRange(tight.CrackedFraction, 0.49, 0.51);
        Assert.InRange(wide.CrackedFraction, 0.49, 0.51);
        Assert.True(wide.FirstCrackTemp < tight.FirstCrackTemp,
            "A wider lot should have colder outliers going first");
    }

    [Fact]
    public void Cracking_is_irreversible()
    {
        var beans = new BeanPopulation(1000, 196.0, 3.0);
        beans.CrackUpTo(200.0);
        var afterHeat = beans.Cracked;

        Assert.Equal(0, beans.CrackUpTo(150.0));
        Assert.Equal(afterHeat, beans.Cracked);
    }

    [Fact]
    public void The_whole_batch_does_not_crack_at_once()
    {
        // The failure this replaced: a hard moisture gate held every bean back
        // until the batch dried, by which point all of them were past their
        // rupture temperature, so first crack had no duration at all.
        var log = Run(RoastCharge.Default);
        Assert.True(log.ReachedFirstCrack);

        Assert.InRange(CracklingSeconds(log), 45.0, 180.0);

        var atCall = log.Samples.First(s => s.Time >= log.FirstCrackTime).CrackedFraction;
        Assert.True(atCall < 0.15, $"First crack should be called on the early poppers, not {atCall:P0} of the batch");
    }

    [Fact]
    public void Sorting_buys_a_sharper_cue_at_the_roaster()
    {
        // The point of the whole feature. A single screen size cracks in a tight
        // volley; a ragged lot smears the same pops out and never gets loud.
        var sorted = Run(ReferenceRoasts.WellSorted);
        var mixed = Run(ReferenceRoasts.MixedScreen);

        Assert.True(CracklingSeconds(sorted) < CracklingSeconds(mixed) * 0.6,
            $"Sorted lot should crack faster: {CracklingSeconds(sorted):F0}s vs {CracklingSeconds(mixed):F0}s");

        var sortedPeak = sorted.Samples.Max(s => s.PopsPerSecond);
        var mixedPeak = mixed.Samples.Max(s => s.PopsPerSecond);
        Assert.True(sortedPeak > mixedPeak * 1.5,
            $"Sorted lot should crack louder: {sortedPeak:F0}/s vs {mixedPeak:F0}/s");
    }

    [Fact]
    public void A_ragged_lot_starts_popping_early_and_can_fool_the_protocol()
    {
        // Emergent, not authored. The stragglers read as first crack, the gas comes
        // down on schedule, and the roast stalls with the batch still cracking.
        var sorted = Run(ReferenceRoasts.WellSorted);
        var mixed = Run(ReferenceRoasts.MixedScreen);

        Assert.True(mixed.FirstCrackTime < sorted.FirstCrackTime,
            "A wider spread should pop its outliers sooner");
        Assert.True(sorted.DropTemp >= 212.5, "The sorted lot should finish");
        Assert.True(mixed.DropTemp < 212.5, "The unsorted lot should stall short of drop");
    }

    [Fact]
    public void Pops_stop_when_the_roast_stalls()
    {
        var log = Run(ReferenceRoasts.MixedScreen);
        Assert.True(log.Samples[^1].PopsPerSecond < 3.0);
        Assert.True(log.Samples[^1].CrackedFraction < 1.0,
            "A stalled roast should leave part of the batch uncracked");
    }

    [Fact]
    public void Bean_count_follows_the_charge_weight()
    {
        Assert.Equal(6000, RoastCharge.Default.BeanCount);
        Assert.Equal(3000, new RoastCharge { DryMassKg = 0.5 }.BeanCount);
        Assert.Equal(9000, new RoastCharge { DryMassKg = 1.0, BeansPerKg = 9000 }.BeanCount);
    }
}

/// <summary>
/// The two fairness fixes: a roast that is dying should say so while there is
/// still time to act, and should not take twenty minutes to finish dying.
/// </summary>
public class StallLegibilityTests
{
    private static RoastLog Run(RoastCharge? charge = null) =>
        RoastRunner.Run(ReferenceRoasts.Textbook(), charge: charge, sampleInterval: 1.0);

    // ---- Expose state, hide outcome ----------------------------------------

    [Fact]
    public void Net_heat_flow_agrees_with_which_way_the_beans_are_going()
    {
        // The signal has to be true before it can be fair. Net watts is the actual
        // energy balance, so its sign must match what bean temperature does next.
        var log = Run();

        // From the second interval on: the sample at t=0 is taken before any step
        // has run, so it has no balance to report yet.
        for (var i = 2; i < log.Samples.Count; i++)
        {
            var rising = log.Samples[i].BeanTemp > log.Samples[i - 1].BeanTemp;
            var gaining = log.Samples[i - 1].NetBeanWatts > 0;
            Assert.True(rising == gaining,
                $"At t={log.Samples[i].Time}s net was {log.Samples[i - 1].NetBeanWatts:F0}W but bean temp " +
                $"{(rising ? "rose" : "fell")}");
        }
    }

    [Fact]
    public void Drum_headroom_is_just_the_gauge_a_roaster_already_has()
    {
        var log = Run();
        var sim = new RoasterSim();
        sim.SetBurner(0.5);
        sim.Step(300);

        Assert.Equal(sim.State.EnvTemp - sim.State.BeanTemp, sim.State.DrumHeadroom, 9);
        Assert.NotEmpty(log.Samples);
    }

    [Fact]
    public void A_roast_that_works_never_reports_losing_heat()
    {
        // No false alarms, or the warning becomes noise the player learns to ignore.
        foreach (var log in new[] { Run(), Run(ReferenceRoasts.WellSorted), Run(ReferenceRoasts.DenseLot) })
        {
            Assert.Equal(RoastOutcome.Dropped, log.Outcome);
            Assert.DoesNotContain(log.Samples, s => s.Time > 120 && s.NetBeanWatts < 0);
        }
    }

    [Fact]
    public void A_dying_roast_says_so_with_time_left_to_act()
    {
        var log = Run(ReferenceRoasts.MixedScreen);
        Assert.Equal(RoastOutcome.Abandoned, log.Outcome);

        var wentNegative = log.Samples.First(s => s.Time > 120 && s.NetBeanWatts < 0).Time;
        Assert.True(wentNegative < log.DropTime,
            "The warning has to arrive before the roast ends, not with it");

        // The real lead is that the level sags long before the sign flips, and the
        // gap widens as it goes — so the reading is a trend to watch, not a light
        // that comes on when it is already too late. Compared at fixed times, both
        // of which land while the healthy roast is still running.
        var healthy = Run();
        double Gap(double t) =>
            log.Samples.First(s => s.Time >= t).NetBeanWatts
            - healthy.Samples.First(s => s.Time >= t).NetBeanWatts;

        var early = Gap(540);
        var later = Gap(590);

        Assert.True(early < -20,
            $"The dying roast should already read low a minute before the flip ({early:F0}W behind)");
        Assert.True(later < early * 2.0,
            $"The gap should widen: {early:F0}W at 540s, {later:F0}W at 590s");
        Assert.True(540 < wentNegative,
            "Both readings should come from before the sign actually flipped");
    }

    // ---- Nobody watches a dead batch for twenty minutes --------------------

    [Fact]
    public void A_failed_roast_is_abandoned_rather_than_run_out_the_clock()
    {
        var log = Run(ReferenceRoasts.MixedScreen);

        Assert.Equal(RoastOutcome.Abandoned, log.Outcome);
        Assert.True(log.DropTime < RoasterConfig.Default.MaxRoastSeconds - 60,
            $"Should have given up well before the cap, ended at {log.DropTime:F0}s");
    }

    [Fact]
    public void Giving_up_needs_a_sustained_loss_not_a_dip()
    {
        // The rate of rise dips across first crack in every roast. That must not
        // read as a dead roast.
        var log = Run();
        Assert.Equal(RoastOutcome.Dropped, log.Outcome);
        Assert.True(log.ReachedFirstCrack);
    }

    [Fact]
    public void The_clock_cap_is_honoured_when_nobody_calls_it()
    {
        // A recorded trace has no judgement, so the cap is what ends it.
        var cfg = RoasterConfig.Default with { MaxRoastSeconds = 300.0 };
        var log = RoastRunner.Run(DialTrace.Constant(0.05), cfg, sampleInterval: 1.0);

        Assert.Equal(RoastOutcome.TimedOut, log.Outcome);
        Assert.InRange(log.DropTime, 299.0, 301.0);
    }

    [Fact]
    public void Outcome_distinguishes_finishing_from_giving_up()
    {
        Assert.True(Run().Succeeded);
        Assert.False(Run(ReferenceRoasts.MixedScreen).Succeeded);
    }
}
