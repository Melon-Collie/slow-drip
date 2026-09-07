# Simulation core

The engine-free half of Slow Drip. Everything here compiles and runs without
Godot, which is the rule [design.md §15](../docs/design.md#15-technical-direction)
sets so that presentation stays strictly downstream and multiplayer stays a
transport swap. `SimBoundaryTests` enforces it against the compiled assembly, and
CI runs it.

```
SlowDrip.Sim/          the simulation. No engine types, no I/O, no clock of its own
SlowDrip.Sim.Tests/    the design claims, and the roasting claims, stated as tests
tools/RoastLab/        headless runner for tuning and plotting
```

## Status

**M1 of the roaster prototype: the thermal model.** No engine project yet — the
next milestone is a dial and two curves in Godot, which reads this and holds
none of it.

## Running it

```sh
dotnet test sim/SlowDrip.sln                           # 52 tests, about a second
dotnet run --project sim/tools/RoastLab                # every reference roast
dotnet run --project sim/tools/RoastLab textbook       # one of them
dotnet run --project sim/tools/RoastLab textbook --csv # the curve, for plotting
```

A ten-minute roast simulates in a few milliseconds, which is the practical reason
to keep the engine out: the model gets tuned and regression tested without
playing it.

## The model

Two coupled thermal bodies, moisture in two pools, a depleting exothermic source,
and a lagged sensor.

```
dET/dt = (burner*power  -  h_bean*(ET - BT)  -  h_loss*(ET - ambient)) / C_env
dBT/dt = (h_bean*(ET - BT)  +  exotherm(BT)  -  evaporation(surface) - flash(venting)) / C_bean
probe += (0.9*BT + 0.1*ET - probe) * dt/(tau + dt)
```

Nothing delays an input on purpose. The roughly twenty seconds of dead time that
design.md §9.4 builds the skill ceiling on is what two lags in series plus a slow
probe do — the drum answers the dial in under two seconds, the rate-of-rise
readout takes about eighteen.

Four terms carry the design:

- **Surface moisture** is the drying phase. While it remains, burner energy goes
  into phase change instead of temperature, so a flat rate of rise through drying
  is a physical outcome rather than a scripted failure.
- **Core moisture** is the debt. It migrates outward slowly, and whatever is left
  when the bean ruptures leaves the core all at once. Part of it flashes to steam
  and pays its latent heat *then*, which is the RoR crash; the rest becomes free
  water on a broken bean. This is the mechanism roasting literature gives for the
  crash: at first crack the beans release a great deal of moisture from their
  cores in a short period, and that moisture is cooler than the bean and probe.
  Routing all of it through the slow surface pool instead — which is what the
  model used to do — spreads the same energy over ninety seconds and the crash
  disappears into the glide.
- **The exotherm** is the teeth, and it depletes. Arrhenius kinetics on a finite
  reactant, so self-heating builds through browning, peaks after first crack, and
  fades — a bump, not an escape.
- **Thermal mass** comes from the charge, so a dial trace that suited one lot
  misses on the next. No special case implements this.

### First crack is a population, not an event

design.md §9.4 wants first crack to be audio — *"scattered pops building"* — so
the pops have to come from somewhere. They come from the batch not being uniform.
Every bean in the drum carries its own rupture temperature; as the batch heats,
beans cross their thresholds and pop. The pop rate is literally the roast's speed
multiplied by the population's density at that temperature.

This is the same move `sprite-layers.md` makes for cherries — *the atom is the
cherry, not the cluster* — and it buys the same thing: what the player perceives
is what the simulation is actually working with, with no fudge in between.

Thresholds are laid out on stratified quantiles of a logistic distribution, which
has a closed-form quantile, so there is no random number generator anywhere near
the simulation and two lots with the same parameters crack identically. The
scatter that makes crackle sound organic belongs in the audio layer, on top of the
`PopsPerSecond` the sim reports.

The consequence that makes it worth having: **a bean vents its core water at the
instant it ruptures**, so the shape of the crash is the shape of the crackle.

| Lot | Spread | Crackle | Peak | Outcome |
|---|---|---|---|---|
| Sorted to one screen | 2.0°C | 117s | 95/s | Drops at 213°C, 22% development |
| Nominal | 3.5°C | 160s | 75/s | Drops at 213°C, 25% development |
| Unsorted, mixed screen | 6.0°C | 242s | 56/s | Drops at 213°C, 30% development |
| Wild, unsortable | 13.0°C | 474s | 21/s | **Runs the clock out** at 211°C |

What a ragged lot costs is control of the number the player is asked to hit. Its
stragglers start popping half a minute early, so first crack gets called too soon
and the gas comes down on schedule; then the batch is still cracking well into
development and the roast runs long. Only a genuinely unsortable lot fails
outright. An earlier version of this model killed the roast at a 6°C spread, but
that was an over-strong exotherm leaving no margin anywhere rather than a fact
about screen size — see *What the retune changed* below.

That makes §9.2's sorting table pay off twice. It is framed there as costing yield
to protect the score; here it also buys the single piece of information the
roaster most needs — a crack sharp enough to time against.

The player reads `BeanProbe`, never `BeanTemp`. The probe starts at the preheated
drum temperature while the beans are at room temperature, and the turning point
is that instrument catching up — an artefact, not an event.

## Failing fairly

A stall used to be the worst kind of failure a game can ship: invisible, delayed,
and unrecoverable. You crossed the line minutes before anything looked wrong, and
then watched a dead batch run out a twenty-minute clock. Two changes, neither of
which costs anything in realism.

**The roast reports its own energy balance.** `NetBeanWatts` is what the drum is
giving plus what the beans are generating minus what evaporation is taking. Below
zero the roast is losing. `DrumHeadroom` is the same thing as a temperature: how
much hotter the drum is than the beans, which is exactly what a roaster reads off
the environmental gauge on a real machine. Both are **state, not prophecy** —
design.md §9.3's rule is *"expose state, hide outcome"*, and these say the roast
is losing heat right now, not that it is doomed or how it will taste. A player who
adds gas puts it back positive.

The reading is honest and it is early. Against a healthy roast at the same moment:

No successful reference roast ever reads negative, and on a dying one the level
sags a couple of minutes before the sign flips, so the reading is a trend to watch
rather than a light that comes on too late. `A_dying_roast_says_so_with_time_left_to_act`
measures that lead rather than sampling it at fixed seconds.

**The warning covers less than it used to, though, and that is worth knowing.**
With the energy balance corrected, only a *deep* cut actually drives the beans
net-negative. Just above that — around 0.31 on the pre-crack dial — a failing
roast never goes negative at all; it simply crawls, never reaching drop, and the
clock cap is what ends it. `NetBeanWatts` is still true, but it is no longer a
complete failure detector, and `Abandon` inherits that gap.

**The drum gets emptied.** `MaxRoastSeconds` caps the roast, and a pilot can
`Abandon` — give up once the beans have been losing heat for a solid minute with
drop temperature still out of reach. A brief dip across first crack is normal and
does not count. A failed roast used to cost twice a good one; now it costs about
the same.

| | Outcome | Ended | Cost vs a good roast |
|---|---|---|---|
| textbook | Dropped | 12.4 min | — |
| cut too shallow | Dropped | 11.3 min | 91% |
| cut too deep | Abandoned | 13.0 min | 105% |
| mixed screen | Dropped | 12.8 min | 103% |
| baked | Timed out | 15.0 min | 121% |
| dense lot, fixed trace | Timed out | 15.0 min | 121% |

## Is it fair?

The pre-crack gas is the dial that decides the roast, and it fails in one
direction only with no holes in the middle. `Nm` means the roast was given up on
or ran the clock out after N minutes; a percentage means it finished, at that
development ratio.

```
pre-crack gas
   0.26   0.30   0.33   0.36   0.39   0.42   0.44   0.47   0.50   0.53   0.58
  13.0m  14.5m  15.0m  15.0m   31%    27%    25%    23%    21%    20%    18%
```

Read left to right that is one story: too little heat and the roast never gets
there, more heat and development shortens, with a band in between. That is the
fairness property §9.4 wants, and nothing was aimed at it directly — it falls out
of the energy balance being right.

## The energy balance, and why it is the whole thing

Where the beans' heat comes from over a textbook roast:

| | burner | drum → bean | exotherm | evaporation | headroom |
|---|---|---|---|---|---|
| 5:00 | 2064 W | 792 W | 9 W | 477 W | +99°C |
| 8:00 | 2064 W | 539 W | 47 W | 344 W | +67°C |
| 9:18 *(crack)* | 1892 W | 448 W | 90 W | 340 W | +56°C |
| 11:40 | 1162 W | 231 W | 192 W | 190 W | +29°C |
| 12:24 *(drop)* | 1162 W | 129 W | 261 W | 137 W | +16°C |

Two things have to be true here and both were false before the retune.

**The drum stays hotter than the beans.** Headroom is positive the whole way, so
the burner is still the thing heating the coffee at drop. Previously the exotherm
reached 1100 W against a drum-to-bean path capped near 800 W: the beans heated
themselves against a drum that had gone 43°C *colder* than they were, the dial
contributed 162 W of the total, and development was on rails no matter what the
player did.

**Evaporation tapers.** It falls from 477 W to 137 W as the free water actually
leaves. Previously it sat at 400–460 W for the entire roast — the rate law scales
with (BT − 60) without bound, so as the beans heated, the growing drive cancelled
the depleting pool and the term never fell. That permanent drain is what forced
the gas high enough that it could never come down again, which is why the taught
pre-crack *reduction* was impossible on the old machine and the exotherm had to be
inflated to compensate.

## Reference roasts

`ReferenceRoasts` holds one roast per claim worth holding the model to.

| Roast | Reads |
|---|---|
| `Textbook` | Published protocol played straight. Crack 09:18, drop 12:24 at 213°C, 25% development |
| `CutTooDeep` | Pre-crack gas taken to 0.26. Loses momentum, goes net-negative, given up on at 13:00 |
| `CutTooShallow` | Barely a reduction at all. Finishes early at 11:16, 18% development — underdeveloped |
| `Baked` | Heat pulled at 00:55. Drying floor near 2°C/min, crack four minutes late, runs the clock out |
| `Scorch` | Full burner. Bolts through first crack and hits drop 28s later: burnt outside, raw inside |
| `HandPlayed` | A fixed trace that works — on the lot it was made for |
| `WellSorted` | A charge. One screen size: a tighter, louder volley at first crack |
| `MixedScreen` | A charge. Unsorted: a long quiet bleed that pushes development to 30% |
| `DenseLot` | A charge, not a roast. `HandPlayed` runs out of clock on it; `Textbook` adapts |

Most references are pilots rather than recordings, and that is itself a finding.
A recorded trace cuts the gas at a fixed second whether or not the roast has got
there yet, so it lands somewhere else on a lot that heats differently — which is
what `DenseLot` shows. Anything that has to act relative to first crack has to
watch the curve, which is what the player will be doing.

That is what makes §9.4's automation progression land.

## What the retune changed, and what it cost

The machine was re-derived as a whole rather than fitted knob by knob: burner
power and drum loss were solved from the roast the machine is supposed to be able
to play (dial near half travel approaching first crack, near a third at drop), and
the remaining constants searched against the full landmark set. Three defects went
with it — the exotherm dominating the heat path, evaporation never tapering, and
the rupture vent trickling through the surface pool instead of flashing.

Two claims the old model made did not survive, and they are worth stating plainly
because both were load-bearing.

**The taught 45-second lead time is a soft optimum, not a knife edge.** The old
model reported a different failure on each side of 45s — never cracks at 120s,
stalls at 90s, underdeveloped by 30s — and this README called that "the closest
thing to a validation this model has". It was an artefact. Sweeping the lead now:

```
lead     120s   90s   60s   45s   30s   20s   10s
dev       26%   25%   25%   25%   25%   25%   24%
```

Nothing fails, and the whole range moves the development ratio by two points. The
old sensitivity came from an exotherm strong enough to leave the roast metastable,
so any nudge tipped it. What this machine punishes is the *depth* of the reduction,
not its timing — the sweep above runs from a dead roast to an underdeveloped one.
Timing still moves development in the direction the guidance says; it just is not
the skill the minigame can be built on. **That is a design question, not a bug:**
§9.4's "punishable but telegraphed" needs a second axis if precise timing is meant
to be the skill.

**Sorting no longer decides whether a roast survives.** A 6°C spread used to stall
the roast outright; now it costs eight points of development ratio and a much worse
audio cue. §9.2's sorting table still pays off twice, but the second payoff is
"you can hit your number" rather than "the batch does not die".

Two things the model still does not reproduce:

- **Late cuts don't deepen the crash.** Guidance says a reduction landing inside
  the crack window makes the crash worse. Here a late cut instead leaves too much
  heat in the drum, so the roast arrives at drop underdeveloped. Same verdict,
  different mechanism.
- **Airflow is not modelled.** Swept as a single multiplier on convective coupling,
  drum losses, and moisture removal it is near-redundant with the gas dial. A
  meaningful airflow control needs the heat path split into convective and
  conductive halves and the bean split into surface and core nodes — which would
  also get tipping and scorching as distinct defects, and the real cause of first
  crack. This is the next piece of work.

## Tuning

`RoasterConfig` separates constants with a basis in the literature from ones
tuned until the curve looked right, and says which is which in the comments.

Grounded: bean specific heat 1450 J/(kg·K), against measurements of 1.0–1.9 with
calorimetry near 1.40–1.45. Exotherm budget 350 kJ/kg, in the measured 250–420
range for green coffee to 300°C — most of which sits above any drop temperature,
so a normal roast spends about a tenth of it. Exotherm onset near 150°C. Drying
phase ending at 150°C. First crack at 196°C.

Fitted: burner power, the conductances and heat capacities, and the moisture
coefficients, solved together against curve targets — turning point near a minute
at 80–95°C, roughly 11°C/min through the middle of the roast and 8 at first crack,
a visible dip of 3–4°C/min across the crack, drop near 213°C, and positive drum
headroom throughout.

**`ExothermRateAt200C` is the one to be careful with.** It decides whether the
dial still matters after first crack. Self-heating and the drum-to-bean path are
comparable on a real machine, which is why a roaster can reduce gas across the
crack at all; set it much higher and the model stops being a game.

Two knobs are design decisions wearing physics costumes:

- **`RorSmoothing`** decides how far ahead a crash is visible. It is the
  telegraphing dial behind "punishable but telegraphed". Feel-test it; do not
  quietly tune it.
- **`RuptureVentFraction`** decides how legible the crash is — it supplies rather
  more than a quarter of the dip, the rest being the reduction itself and the
  released water evaporating. Steep: the latent heat of the core water is large
  next to everything else moving at first crack.
- **`CrackTempSpread`** on the charge decides what first crack sounds like, and
  therefore how well the player can time the one reduction that matters. It is
  the lever connecting sorting to roasting.

## Sources

- [Scott Rao — What is Baked Coffee?](https://www.scottrao.com/blog/2018/2/24/what-is-baked-coffee-most-pros-dont-know)
- [Cropster — The Flick, updated for 2021](https://www.cropster.com/news/article/the-flick-updated-for-2021/)
- [Barista Hustle — HTR 3.03 Approaching First Crack](https://www.baristahustle.com/lesson/htr-3-03-approaching-first-crack/)
- [Barista Hustle — RS 4.05 Specific Heat and Conductivity](https://www.baristahustle.com/lesson/rs-4-05-specific-heat-and-conductivity/)
- [Barista Hustle — RS 3.10 Exothermy and Endothermy in Roasting](https://www.baristahustle.com/lesson/rs-3-10-the-roles-of-exothermy-and-endothermy-in-roasting/)
- [Schwartzberg — Batch Coffee Roasting; Roasting Energy Use](https://link.springer.com/chapter/10.1007/978-1-4614-7906-2_10)
- [Modeling and simulation of coffee bean heating during roasting: effect of heat generation](https://www.frontiersin.org/journals/food-science-and-technology/articles/10.3389/frfst.2025.1603783/full)
- [Raemy & Lambelet — A calorimetric study of self-heating in coffee and chicory](https://academic.oup.com/ijfst/article/17/4/451/7910915)
- [Royal NY — Understanding the Rate of Rise](https://www.royalny.com/blogs/rate-of-rise/)
- [Perfect Daily Grind — A Guide to Rate of Rise](https://perfectdailygrind.com/2017/08/coffee-roasting-essentials-a-guide-to-rate-of-rise-ror/)
