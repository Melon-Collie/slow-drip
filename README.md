# Slow Drip

A farming sim about coffee, from tree to cup, set in Vietnam's Central Highlands.

You inherit a mature, healthy, thoroughly mediocre Robusta farm from its retiring
owner and spend years converting it into something worth drinking — grafting,
replanting, and learning to ferment — while running the café that turns your
harvest into money and feedback.

The hook: **you drink your own mistakes for twelve months.**

**Status:** prototyping. The roaster thermal model is in and tested; no engine
project yet.

## Systems spine

> Agronomy sets the ceiling. Craft sets the recovery. Nothing downstream ever adds quality.

## Technical direction

Godot 4, C# simulation core, 2D pixel art, PC only. The simulation compiles
without the engine — no `Godot` type in the sim namespace. See [§15](docs/design.md#15-technical-direction).

First prototype is the roaster, built in milestones:

| | | |
|---|---|---|
| **M1** | Headless thermal model, deterministic and engine-free | **done** |
| M1.5 | Model checked against roasting research; two-pool moisture, depleting exotherm | **done** |
| M1.6 | First crack as a bean population — pops emerge, sorting quality changes the cue | **done** |
| M2 | Dial and curves in Godot. Does steering this feel good? | next |
| M3 | First crack as audio, not UI | |
| M4 | Development time ratio, then scoring against the quality vector | |

M1 lives in [`sim/`](sim/). A twelve-minute roast simulates in a few
milliseconds, so the model is tuned and regression tested without playing it:

```sh
dotnet test sim/SlowDrip.sln
dotnet run --project sim/tools/RoastLab
```

## Docs

- [Design document](docs/design.md) — v0.6 (concept)
- [Sprite layer system](docs/sprite-layers.md) — tree state decomposition, palette ramps
- [Simulation core](sim/README.md) — the roaster model, what it borrows from roasting research, and where it disagrees
