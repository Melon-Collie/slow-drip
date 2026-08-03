# Sprite Layer System

**Status:** proposed. Resolves the open item in [§15](design.md#15-technical-direction) — "decide the sprite layer system before drawing anything final."

Numbers below are starting points, not gospel. The *structure* is the part to hold onto.

---

## The problem

The tree has to visibly carry a lot of state. From §11, the Old Man's comments generate off readable farm state: pruning quality, canopy density, rust, which blocks are stumped or grafted, weed pressure. From §9.1, picking is a per-branch ripeness judgment. From §7, trees age, get grafted, get stumped, and swing between heavy and light years.

Drawn naively, those axes multiply:

```
species(2) × form(7) × canopy density(4) × fruit state(6) × load(2) = 672 sprites
```

And that's before per-branch ripeness, which multiplies again by however many branches a tree has.

**The whole job of this system is turning that multiplication into addition.**

---

## The decomposition

| Layer | Contents | Varies by | Cost |
|---|---|---|---|
| 0 — Ground | Soil, terrace edge, mulch, **weeds** | Weed density (4 states) | Tiles, independent of tree |
| 1 — Frame | Trunk and woody structure | Species × form | **The expensive layer.** Real distinct art |
| 2 — Foliage | Leaf clumps at anchors | Count and clump size (data) | 3–4 sprites per species, palettized |
| 3 — Fruit | Cherries at anchors | Per-cherry ripeness (data) | 2–3 sprites, palettized |
| 4 — Overlay | Rust blotch, blossom, wilt, graft union | Additive flags | ~5 sprites total |
| 5 — Foreground | Shade-tree dapple, occluders | Block-level | Shared |

Weeds never touch the tree. Rust is an overlay, not a foliage variant. Fruit and leaves both live at anchors.

---

## Anchors — the load-bearing idea

**Each frame sprite ships with a list of anchor points.** An anchor is a position on the tree where things attach:

```
anchor: { x, y, branch_id, z_order, scale }
```

Both foliage clumps and fruit clusters attach at anchors. This collapses the two worst axes at once:

- **Canopy density stops being art.** Density = how many anchors carry a leaf clump, and which clump size. A skeletal-pruned tree is the same frame with three anchors populated instead of eight. No new sprite.
- **Per-branch ripeness stops being art.** Each anchor holds its own set of cherries, each with its own palette index. A 40%-ripe branch is literally four of ten cherries at the ripe entry.

### An anchor *is* a branch *is* a §9.1 decision

The picking minigame's decision unit is the branch. The frame's anchors define the branches. So **the art defines the mechanic's granularity** — a frame with six anchors is a tree with six picking decisions.

That's a unification worth protecting: keep anchor counts consistent across mature frames (**6–8 is the starting proposal**) so picking pacing doesn't drift when someone draws a new varietal. Anchor count is a design number that happens to live in an art file.

### The atom is the cherry, not the cluster

§9.1 requires uneven ripeness *within* a branch — that's the entire judgment. So a cluster is not one sprite with a ripeness state; it's ~6–12 individual cherries at fixed offsets within the anchor, each independently palettized.

A cherry is a 3–5 pixel blob. This is cheap, and it means the mix the player reads is the *actual* mix the sim is scoring. No fudging between what's drawn and what's simulated.

---

## Art inventory

The entire tree system, expressing hundreds of state combinations:

| Asset | Count | Notes |
|---|---|---|
| Frames | ~14 | 2 species silhouettes × ~7 form states |
| Leaf clumps | ~8 | 3–4 sizes per species, palettized |
| Cherry sprites | ~3 | single / pair / small cluster, palettized |
| Blossom clump | 1 | Flowering is days long (§6), high drama |
| Overlays | ~5 | Rust, wilt, graft union, bronze new growth |
| Ground / weeds | ~6 | 4 weed densities + terrace edge pieces |

**≈37 sprites.** That's the target to beat, and the number to check yourself against if it starts creeping.

### Form states (the ~7)

Juvenile · mature well-pruned · mature neglected · skeletal-pruned · stumped · stump regrowth · grafted.

"Mature neglected" is the day-one inherited state (§7.1) — healthy, well-tended, badly pruned. Worth drawing before anything else, since it's what the player looks at for the entire first year.

**Varietals share frames.** Typica, Bourbon, Yellow Bourbon and Geisha all use the Arabica frame and differentiate by fruit palette and a leaf tint. Only the fruit color is mechanically load-bearing (§9.1), so that's where the distinction should live. Typica's bronze new growth is a cheap optional overlay if more identity is wanted.

---

## Palette ramps

Because so much is palette-driven, **the ramps are where the picking mechanic actually lives.** These aren't a color pass, they're mechanic design.

### Red varietals — ripeness

```
green → pale green → yellow-green → orange-red → deep red (RIPE) → dark maroon (OVERRIPE)
```

§9.1 states the trap explicitly: overripe reads *closer* to ripe than green does, so **the dangerous confusion is on the far side.** That means the ramp must be perceptually **non-uniform on purpose** — a wide gap between green and ripe, a narrow one between ripe and overripe. Tune it by eye, not by even interpolation in a color picker.

### Yellow varietals — ripeness

```
green → yellow-green → gold (RIPE) → amber-brown (OVERRIPE)
```

Note this ramp is *inherently* harder: the intermediate yellow-green sits near the ripe gold. That's precisely why §9.1 says Yellow Bourbon "quietly breaks the color-reading skill the player just built." **The difficulty spike falls out of the palette rather than being coded anywhere.**

### Leaf health

```
deep green → yellow-green → yellow → brown
```

Drives stress, rust, and drought. Shared across species with a per-species base tint.

### The tinting exemption — important

§15 justifies 2D partly on the grounds that dynamic lighting corrupts color reading and a controlled palette doesn't. **A global day/night or weather tint would reintroduce exactly that problem.**

So: **fruit palettes are exempt from global tinting.** Mist, dawn, and dusk shift the ground, frame, foliage, and background layers; cherries render at their true ramp entry. Slightly unphysical, entirely correct — the alternative is atmosphere quietly breaking the game's central perceptual skill.

---

## LOD

| Zoom | Rendering |
|---|---|
| Arm's-length / distant | One silhouette per tree, averaged tint |
| Traversal | Frame + foliage + one cluster sprite per anchor, averaged palette |
| Picking framing | Frame + foliage + **per-cherry** |

Per-cherry only exists at picking zoom. A 100-tree home block at 6 anchors × 10 cherries is 6,000 sprites — fine batched in Godot 2D, but there's no reason to pay it while walking past.

---

## Requirements that double as tests

Concrete pass/fail checks, worth running as soon as there's art:

1. **Can you tell a stumped block from a mature block at traversal zoom?** §11's example line is "you took the whole east block down to stumps." If that doesn't read at a glance, the dialogue doesn't land. This is a requirement on the *silhouette*, not the detail.
2. **Can a player sort ripe from overripe at picking zoom without a UI hint,** and does it stay genuinely hard? If it's easy, the ramp is too uniform. If it's impossible, too tight.
3. **Does Yellow Bourbon break a trained player?** Test on someone who's already learned the red ramp. That's the intended experience.
4. **Is a grafted tree obviously grafted?** §7.3 is a mid-game unlock the player should feel. A visible union scar on old rootstock is cheap and carries the whole story of the plot's conversion.
5. **Does the ~37-sprite inventory hold** once a second varietal is added? If adding a varietal needs new frames, the decomposition has leaked.

---

## Still open

1. **Pixel resolution and character scale** — blocks final anchor offsets, since anchor precision depends on tree sprite size. Still the first art decision (§15).
2. **Anchor count for non-mature frames.** Juvenile and stumped trees plainly have fewer branches, which means fewer picking decisions — probably correct, possibly fiddly.
3. **Whether arm's-length blocks (§8) need tree sprites at all**, or resolve to a block-level silhouette and a report. Leaning silhouette.
4. **Biennial bearing (§7)** currently rides on anchor fruit count. May want a frame or foliage cue too, so a resting block reads as resting rather than as merely unpicked.
