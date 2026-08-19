# Simulation core

The engine-free half of Slow Drip. Everything here compiles and runs without
Godot, which is the rule [design.md §15](../docs/design.md#15-technical-direction)
sets so that presentation stays strictly downstream and multiplayer stays a
transport swap. `SimBoundaryTests` enforces it against the compiled assembly, and
CI runs it.

```
SlowDrip.Sim/          the simulation. No engine types, no I/O, no clock of its own
SlowDrip.Sim.Tests/    the design claims of design.md, stated as tests
tools/RoastLab/        headless runner for tuning and plotting
```

## Status

**M1 of the roaster prototype: the thermal model.** No engine project yet — the
next milestone is a dial and two curves in Godot, which reads this and holds
none of it.

## Running it

```sh
dotnet test sim/SlowDrip.sln          # 26 tests, about a second
dotnet run --project sim/tools/RoastLab               # summarise every reference roast
dotnet run --project sim/tools/RoastLab healthy       # summarise one
dotnet run --project sim/tools/RoastLab healthy --csv # dump the curve for plotting
```

A twelve-minute roast simulates in a few milliseconds, which is the practical
reason to keep the engine out: the model can be tuned and regression tested
without playing it.

## The model

Two coupled thermal bodies, an evaporation sink, an exothermic source, and a
lagged sensor.

```
dET/dt = (burner*power  -  h_bean*(ET - BT)  -  h_loss*(ET - ambient)) / C_env
dBT/dt = (h_bean*(ET - BT)  +  exotherm(BT)  -  evaporation(BT, moisture)) / C_bean
probe += (0.9*BT + 0.1*ET - probe) * dt/(tau + dt)
```

Nothing delays an input on purpose. The roughly twenty seconds of dead time that
design.md §9.4 builds the skill ceiling on is what two lags in series plus a slow
probe do — the drum answers the dial in under two seconds, the rate-of-rise
readout takes about eighteen. `Drum_answers_the_dial_immediately_but_the_readout_does_not`
pins that down.

Three terms carry the design:

- **Evaporation** is the drying phase. While free moisture remains, burner energy
  goes into phase change instead of temperature, so a flat rate of rise through
  drying is a physical outcome rather than a scripted failure.
- **The exotherm** is the teeth. Cut the gas to catch a runaway and the rate of
  rise crashes, then self-heating flicks it back above where it started.
- **Thermal mass** comes from the charge, so a dial trace that suited one lot
  misses on the next. No special case implements this.

The player reads `BeanProbe`, never `BeanTemp`. The probe starts at the preheated
drum temperature while the beans are at room temperature, and the turning point
is that instrument catching up — an artefact, not an event.

## Reference roasts

`ReferenceRoasts` holds one dial trace per curve shape design.md §9.4 names. They
live in the sim rather than in the tuning tool because the tests assert against
them too.

| Trace | Reads |
|---|---|
| `Healthy` | Rate of rise gliding down. First crack 09:07, drop 11:23 at 213°C, 20% development |
| `Baked` | Heat pulled at 00:55. Flatlines through drying, cracks at 13:18 with the roast gone |
| `CrashAndFlick` | Run hot, gas slammed at 05:30. Rate of rise halves, then the exotherm takes it back |
| `Scorch` | Full burner held down. Past the point of steering |
| `DenseLot` | A charge, not a trace. The healthy profile stalls short of first crack on it |

## Tuning

The constants in `RoasterConfig` came out of a parameter sweep against four
targets on the healthy trace: turning point near 00:50 at ~95°C, first crack near
09:00, peak rate of rise around 35°C/min, and under 10°C/min by drop. `RoastLab`
is how they were found and how to check them after a change.

Two of them are design decisions wearing physics costumes, and are commented as
such:

- **`RorSmoothing`** decides how far ahead a crash is visible. It is the
  telegraphing dial behind "punishable but telegraphed". Feel-test it; do not
  quietly tune it.
- **`ExothermPower`** decides whether the roast fights back after first crack. It
  is a net figure tuned until the crash-and-flick shape was legible, not a
  measured one.
