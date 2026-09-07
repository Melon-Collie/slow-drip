# CLAUDE.md

Context for Claude about **Slow Drip** — a farming sim about coffee, from tree to
cup, set in Vietnam's Central Highlands. Godot 4, C# simulation core, 2D pixel
art, PC only. Currently at M1 of the roaster prototype: the thermal model exists
headless in `sim/`, and there is no Godot project yet.

## Where the detail lives

This file is the always-loaded routing layer: invariants, workflow, conventions.
Deep detail lives next to what it describes.

| Topic | Document |
|---|---|
| The game — every system, its reasoning, and the open questions | `docs/design.md` |
| Tree state decomposition, palette ramps, the tinting exemption | `docs/sprite-layers.md` |
| The roaster model, its sources, and where it departs from them | `sim/README.md` |
| Milestone order | `README.md` |

Section references (§9.4, §15) point into `docs/design.md`.

## Source of truth

**Code and its tests are the only authority on how the system currently
behaves.** Every doc here captures intent, rationale, and invariants — never a
description of current implementation. When a doc and the code disagree, the
code wins and the doc is stale: fix it or delete it.

This matters more here than in most projects, because `sim/README.md` is full of
*numbers* — sweep tables, reference-roast outcomes, energy-balance readings at
specific times. Every one of those is a snapshot of a model that is still being
tuned, and they will drift the moment `RoasterConfig` changes. Treat them as a
record of what was observed and argued, not as a spec. Before relying on any
such figure, re-run it:

```sh
dotnet run --project sim/tools/RoastLab            # every reference roast
dotnet test sim/SlowDrip.sln                       # the claims, as assertions
```

When a table in that README goes stale, regenerate it rather than patching a
number by hand.

## Workflow

- **Run the tests freely.** The sim is pure C# with no engine dependency, so
  `dotnet test sim/SlowDrip.sln` is the primary verification loop for all model
  work — a ten-minute roast simulates in milliseconds. Use it after touching
  anything under `sim/`.
- **In Claude Code on the web**, the .NET 8 SDK is provisioned by an async
  `SessionStart` hook (`.claude/hooks/session-start.sh`). Before the *first*
  `dotnet` command in a web session, run `.claude/hooks/wait-for-dotnet.sh` — it
  blocks until the background install finishes, and no-ops once ready. Locally
  the user already has the SDK and this is unnecessary.
- **Claude cannot run the game.** There is no Godot project yet; when there is,
  engine-side behavior (rendering, input, feel) is the user's to verify. After
  touching engine code, name what to check and let the user report back.
- **Creating `.tscn` scene files is the user's job** once the engine layer
  exists — the generated half of the format (node UIDs, sub-resource refs) is
  the editor's to own. Editing an existing scene's property or script reference
  is fine. `.tres` resources are safe to author directly.
- **Push discipline.** Feature branches (`claude/*`) may be pushed after
  committing so the user can pull and test. Never push `main` without the user
  testing first, and never `git merge` into `main` directly — that's the user's
  call via PR.
- **If you spot a bug or smell while working on something else, flag it.** Don't
  silently fix it (out of scope), don't silently ignore it (it'll rot), don't
  tack it onto the current commit (muddies the diff). Surface it in a line and
  let the user decide.

## The sim boundary (§15, load-bearing)

**The simulation compiles without the engine. No `Godot` type anywhere in the
sim namespace.** Presentation is strictly downstream: it reads sim state and
never holds it. This is what makes multiplayer a transport swap rather than a
rewrite, and it is why the constraint is checkable in CI instead of remembered
at 2am — `SimBoundaryTests` asserts it against the compiled assembly and the
`sim` projects are plain `Microsoft.NET.Sdk`, so a `Godot` reference breaks the
build immediately.

That is the pattern to prefer generally: **a guard the build enforces never
lapses; a guard a human must remember does.** When a comment is the only thing
holding a load-bearing invariant, promote it to a test and thin the comment to a
pointer.

## Determinism rules

The model's value is that it can be tuned and regression-tested without playing
it, which requires runs to be reproducible:

- **No random number generator anywhere in the sim.** Bean rupture thresholds
  are laid out on stratified quantiles of a logistic distribution, which has a
  closed-form quantile — two lots with the same parameters crack identically.
  Scatter that needs to *sound* organic belongs in the audio layer, on top of
  the `PopsPerSecond` the sim reports.
- **No wall-clock, no ambient time, no I/O inside the model.** Fixed timestep;
  the sim has no clock of its own.
- **Reference roasts are the regression suite.** `ReferenceRoasts` holds one
  roast per claim worth holding the model to. A change that moves them is a
  change to the design, not an incidental diff — say so.

## Design rules the model must keep

- **Expose state, hide outcome (§9.3).** `NetBeanWatts` and `DrumHeadroom` say
  the roast is losing heat *now*; they never say it is doomed or how it will
  taste. Don't add readouts that predict the verdict.
- **What the player perceives is what the simulation is working with.** First
  crack is a population of beans crossing their own thresholds, not an event
  with a pop sound attached; cherries are atoms, not clusters. When a cue and
  the mechanism behind it drift apart, that's the bug.
- **Prefer adding mechanism over clamping.** On this model, replacing a hard
  switch with physics has fixed fairness more reliably than bounding inputs
  would have — the two-pool moisture model and the bean population each turned a
  cliff into a slope without anything being aimed at fairness directly.
- **Some constants are design decisions in physics costumes.** `RorSmoothing`,
  `RuptureVentFraction`, and `CrackTempSpread` set how legible a failure is, not
  how real it is. `RoasterConfig` says which constants are grounded and which
  were fitted; keep that distinction accurate, and feel-test the design ones
  rather than quietly tuning them.

## Code conventions

Most of this is enforced by `sim/Directory.Build.props` — `net8.0`, nullable
enabled, `TreatWarningsAsErrors`, `Deterministic`. A clean build is zero
warnings *and* zero errors; fix the cause rather than suppressing it.

- **C# naming:** `PascalCase` for types/methods/properties, `camelCase` for
  locals/params, `_camelCase` for private fields.
- **Strong typing.** Explicit types over `var` when the type isn't obvious.
- **Get the mechanic working, then tune numbers.** Tuning values live in
  `RoasterConfig`, not baked into logic.

## Comments

A comment's scope must not exceed the code beneath it. If it explains something
larger than the lines it sits on — how a subsystem thinks, why two files agree,
what the code used to be — it is in the wrong place however true it is. Ask *is
this a fact about the code directly below?*

Worth writing: why a non-obvious choice was made here, a physical justification
for a constant, units and frames of reference, a trap the next reader falls into,
an invariant this code relies on but cannot check locally.

Everything else has a home:

| What it is | Where it goes |
|---|---|
| How a subsystem thinks, cross-file rationale | this file, or the nearest README |
| "must match X", "keep in sync with Y" | a **test** that fails when they diverge |
| Deferred work, a known gap | a GitHub issue — never `TODO`/`FIXME` |
| What the code used to be, which lever was retired | **git** — delete it |

Never write status or "currently we…" comments: they describe a moment, not a
constraint, so they rot exactly like a stale doc. Narration of the past is not
documentation — git holds it, and the commit that made the change holds why.

## What goes in this file

Load-bearing constraints that prevent the wrong move: invariants, workflow
rules, conventions. Things true and persistent about the project.

Not here: status snapshots and test counts (git and the tests are authoritative),
milestone ordering (`README.md`), open design questions (`docs/design.md`), and
anything derivable from the file tree or a test run.
