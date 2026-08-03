# Slow Drip

A farming sim about coffee, from tree to cup, set in Vietnam's Central Highlands.

You inherit a mature, healthy, thoroughly mediocre Robusta farm from its retiring
owner and spend years converting it into something worth drinking — grafting,
replanting, and learning to ferment — while running the café that turns your
harvest into money and feedback.

The hook: **you drink your own mistakes for twelve months.**

**Status:** pre-prototype. No code yet.

## Systems spine

> Agronomy sets the ceiling. Craft sets the recovery. Nothing downstream ever adds quality.

## Technical direction

Godot 4, C# simulation core, 2D pixel art, PC only. The simulation compiles
without the engine — no `Godot` type in the sim namespace. See [§15](docs/design.md#15-technical-direction).

First prototype is the roaster.

## Docs

- [Design document](docs/design.md) — v0.5 (concept)
