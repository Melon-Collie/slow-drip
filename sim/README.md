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
dotnet test sim/SlowDrip.sln                           # 34 tests, about a second
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
dBT/dt = (h_bean*(ET - BT)  +  exotherm(BT)  -  evaporation(BT, surface)) / C_bean
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
  when the bean ruptures vents in a rush. That vent is the RoR crash, and it is
  the mechanism roasting literature actually gives for it: at first crack the
  beans release a great deal of moisture from their cores in a short period, and
  that moisture is cooler than the bean surface and the probe.
- **The exotherm** is the teeth, and it depletes. Arrhenius kinetics on a finite
  reactant, so self-heating builds through browning, peaks after first crack, and
  fades — a bump, not an escape.
- **Thermal mass** comes from the charge, so a dial trace that suited one lot
  misses on the next. No special case implements this.

The player reads `BeanProbe`, never `BeanTemp`. The probe starts at the preheated
drum temperature while the beans are at room temperature, and the turning point
is that instrument catching up — an artefact, not an event.

## Reference roasts

`ReferenceRoasts` holds one roast per claim worth holding the model to.

| Roast | Reads |
|---|---|
| `Textbook` | Published protocol played straight. Crack 07:55, drop 09:52 at 213°C, 20% development |
| `CutTooEarly` | Pre-crack reduction made 90s out. Stalls into a negative rate of rise and never reaches drop |
| `CutTooLate` | Reduction left until 10s out. No crash — it just arrives at drop 66s after the crack, 12% development |
| `Baked` | Heat pulled at 00:55. Drying floor of 1.5°C/min, crack five minutes late, 8% development |
| `Scorch` | Full burner. The probe reaches drop temperature before the beans have cracked: burnt outside, raw inside |
| `HandPlayed` | A fixed trace that works — on the lot it was made for |
| `DenseLot` | A charge, not a roast. `HandPlayed` stalls out on it; `Textbook` adapts |

Most references are pilots rather than recordings, and that is itself a finding.
A recorded trace cuts the gas at a fixed second whether or not the roast has got
there yet, so it is brittle: moving one mid-roast dial step by two percent is the
difference between a finished roast and one that stalls at 120°C. Anything that
has to act relative to first crack has to watch the curve — which is what the
player will be doing, and what makes §9.4's automation progression land.

## What the model agrees with, and where it doesn't

The published protocol for washed coffees on a drum roaster says: make the
pre-crack gas reduction about **45 seconds** before first crack, leave the dial
alone across the crack itself, then step down against development ratio. Nothing
tells the roaster when the crack is 45 seconds away — they extrapolate from the
curve, which is what `DoctrinePilot` does and what the player will do by eye.

Sweeping that lead time is the closest thing to a validation this model has:

| Lead | Result |
|---|---|
| 120s | Never cracks |
| 90s | Stalls — rate of rise goes negative, never reaches drop |
| 60s | Crashes to 2.7°C/min, drops cool at 205°C |
| **45s** | **Clean. Rate of rise 8.2 → 6.8, drop 213°C at 20% development** |
| 30s and later | No crash, but development collapses toward 12% |

The taught number is the optimum in the model, with a different failure on each
side. That was not tuned for — the config was fitted to curve *shape* targets
before the protocol was implemented.

Two places the model does **not** reproduce the literature, recorded here rather
than papered over:

- **Late cuts don't deepen the crash.** Guidance says a reduction landing inside
  the crack window makes the crash worse. Here a late cut instead leaves too much
  heat in the drum, so the roast arrives at drop temperature underdeveloped. Same
  verdict, different mechanism.
- **The drying-to-crash link is weak.** Rushing drying should leave a wetter core
  and a deeper crash. It does, but only slightly (core moisture 0.018 versus
  0.013), and the effect is swamped by how much hotter the drum is. If that link
  is wanted as a mechanic, core migration needs to be more time-driven and less
  temperature-driven — defensible, since diffusion out of the bean is
  diffusion-limited, but it is a change to make deliberately rather than by
  accident.

## Tuning

`RoasterConfig` separates constants with a basis in the literature from ones
tuned until the curve looked right, and says which is which in the comments.

Grounded: bean specific heat 1450 J/(kg·K), against measurements of 1.0–1.9 with
calorimetry near 1.40–1.45. Exotherm budget 350 kJ/kg, in the measured 250–420
range for green coffee to 300°C — most of which sits above any drop temperature,
so a normal roast spends only a fifth of it. Exotherm onset near 150°C. Drying
phase ending at 150°C. First crack at 196°C.

Fitted: the conductances and heat capacities, against published curve targets —
turning point near a minute at 80–95°C, roughly 10°C/min through the middle of
the roast and 5°C/min at first crack, drop near 213°C.

Two knobs are design decisions wearing physics costumes:

- **`RorSmoothing`** decides how far ahead a crash is visible. It is the
  telegraphing dial behind "punishable but telegraphed". Feel-test it; do not
  quietly tune it.
- **`FirstCrackMoistureRelease`** decides how legible the crash is. The physical
  crash is partly masked by probe lag, which is realistic and works against
  §9.4's requirement that failures be visible before they are tasted.

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
